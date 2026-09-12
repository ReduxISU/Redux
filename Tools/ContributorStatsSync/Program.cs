using System.Net;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Octokit;

namespace ContributorStatsSync;

// Weekly sync for wwwroot/contributorInfo.json's reduxStats/reduxGuiStats (see issue #565).
// Recomputes commit/PR/review counts for every contributor who already has a githubUsername,
// and auto-detects new GitHub identities committing to either repo that aren't tracked yet.
//
// Identity resolution stays human-curated on purpose (see #564's audit): this tool never
// merges two GitHub logins into one contributor, and never overwrites an existing entry's
// name, email, education, major, or bio -- only its stats objects, plus githubUsername for a
// brand new entry. Updates are applied node-by-node on the parsed JSON tree (not by
// round-tripping the whole file through a POCO) specifically so untouched entries keep their
// exact original key order and formatting -- a run that only changes a handful of contributors
// produces a diff touching only those contributors, not a full-file reformat.
internal static class Program {
    private const string Owner = "ReduxISU";
    private const string ReduxRepo = "Redux";
    private const string GuiRepo = "Redux_GUI";

    // Bots and the bulk-import/admin account from repo setup -- not real contributors to track.
    private static readonly HashSet<string> ExcludedLogins = new(StringComparer.OrdinalIgnoreCase) {
        "dependabot[bot]",
        "github-actions[bot]",
        "copilot-swe-agent[bot]",
        "copilot-pull-request-reviewer[bot]",
        "copilot-pull-request-reviewer",
        "Copilot",
        "ReduxISU",
        "ReduxISU-archived",
    };

    private static bool IsExcluded(string login) =>
        ExcludedLogins.Contains(login) || login.EndsWith("[bot]", StringComparison.OrdinalIgnoreCase);

    private static async Task<int> Main(string[] args) {
        var dryRun = args.Contains("--dry-run");
        var scanOnly = args.Contains("--scan-only");
        var onlyArg = args.FirstOrDefault(a => a.StartsWith("--only=", StringComparison.Ordinal));
        var onlyLogins = onlyArg is null
            ? null
            : onlyArg["--only=".Length..]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var jsonPath = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal))
            ?? Path.Combine("wwwroot", "contributorInfo.json");

        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? Environment.GetEnvironmentVariable("GH_TOKEN");
        if (string.IsNullOrWhiteSpace(token)) {
            Console.Error.WriteLine("No GITHUB_TOKEN or GH_TOKEN environment variable set.");
            return 1;
        }

        var github = new GitHubClient(new Octokit.ProductHeaderValue("Redux-ContributorStatsSync")) {
            Credentials = new Credentials(token),
        };

        using var http = new HttpClient { BaseAddress = new Uri("https://api.github.com/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Redux-ContributorStatsSync");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        var throttle = new SearchThrottle();

        Console.WriteLine($"Loading {jsonPath}...");
        var root = JsonNode.Parse(await File.ReadAllTextAsync(jsonPath))!.AsObject();

        var knownLoginToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, entry) in root) {
            var login = entry?["githubUsername"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(login)) {
                knownLoginToName[login] = name;
            }
        }

        var newEntryNames = new List<string>();

        if (onlyLogins is null) {
            Console.WriteLine("Scanning commit history for contributors not yet tracked...");
            var unresolvedEmails = new List<(string Repo, string Email)>();

            foreach (var repo in new[] { ReduxRepo, GuiRepo }) {
                var seenThisRepo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                await foreach (var commit in EnumerateCommitsAsync(github, repo)) {
                    var login = commit.Author?.Login;
                    if (login is null) {
                        var email = commit.Commit?.Author?.Email;
                        if (!string.IsNullOrWhiteSpace(email)) {
                            unresolvedEmails.Add((repo, email));
                        }
                        continue;
                    }

                    if (IsExcluded(login) || knownLoginToName.ContainsKey(login) || !seenThisRepo.Add(login)) {
                        continue;
                    }

                    Console.WriteLine($"  New contributor found: {login} (via {repo})");
                    var displayName = await ResolveDisplayNameAsync(github, login);
                    var entryKey = displayName ?? login;

                    if (root.ContainsKey(entryKey)) {
                        Console.WriteLine(
                            $"    Skipping -- an entry named \"{entryKey}\" already exists (possible name "
                            + "collision with a different GitHub account; needs a human to sort out).");
                        continue;
                    }

                    root[entryKey] = new JsonObject {
                        ["email"] = "",
                        ["education"] = "",
                        ["major"] = "",
                        ["bio"] = "Auto-detected by the contributor stats sync workflow (#565) -- "
                            + "bio, education, and major not yet filled in.",
                        ["githubUsername"] = login,
                    };
                    knownLoginToName[login] = entryKey;
                    newEntryNames.Add(entryKey);
                }
            }

            if (unresolvedEmails.Count > 0) {
                Console.WriteLine(
                    $"  {unresolvedEmails.Count} commit(s) have an author email that doesn't resolve to a "
                    + "GitHub account -- skipped, since stats can't be computed without a login:");
                foreach (var group in unresolvedEmails.Distinct().GroupBy(x => x.Email)) {
                    Console.WriteLine($"    {group.Key} ({string.Join(", ", group.Select(g => g.Repo).Distinct())})");
                }
            }
        }

        if (scanOnly) {
            Console.WriteLine($"Scan-only -- not computing stats or writing the file. "
                + $"{newEntryNames.Count} new contributor(s) found.");
            return 0;
        }

        var loginsToProcess = onlyLogins ?? new HashSet<string>(knownLoginToName.Keys, StringComparer.OrdinalIgnoreCase);

        Console.WriteLine($"Computing stats for {loginsToProcess.Count} contributor(s)...");
        foreach (var login in loginsToProcess) {
            if (!knownLoginToName.TryGetValue(login, out var name)) {
                Console.WriteLine($"  Skipping {login} -- not found in contributorInfo.json.");
                continue;
            }

            Console.WriteLine($"  {name} ({login})");
            var reduxStats = await ComputeRepoStatsAsync(http, throttle, ReduxRepo, login);
            var guiStats = await ComputeRepoStatsAsync(http, throttle, GuiRepo, login);

            var entry = root[name]!.AsObject();
            entry["reduxStats"] = StatsToNode(reduxStats);
            entry["reduxGuiStats"] = StatsToNode(guiStats);
        }

        if (dryRun) {
            Console.WriteLine($"Dry run -- not writing the file. {newEntryNames.Count} new entr"
                + $"{(newEntryNames.Count == 1 ? "y" : "ies")} would be added:");
            foreach (var name in newEntryNames) {
                Console.WriteLine($"  {name}");
            }
            return 0;
        }

        await SaveAsync(jsonPath, root);
        Console.WriteLine($"Wrote {jsonPath}. {newEntryNames.Count} new entr"
            + $"{(newEntryNames.Count == 1 ? "y" : "ies")} added.");
        return 0;
    }

    private static async IAsyncEnumerable<GitHubCommit> EnumerateCommitsAsync(GitHubClient github, string repo) {
        const int pageSize = 100;
        var page = 1;
        while (true) {
            var options = new ApiOptions { PageSize = pageSize, PageCount = 1, StartPage = page };
            var commits = await github.Repository.Commit.GetAll(Owner, repo, options);
            foreach (var commit in commits) {
                yield return commit;
            }
            if (commits.Count < pageSize) {
                yield break;
            }
            page++;
        }
    }

    private static async Task<string?> ResolveDisplayNameAsync(GitHubClient github, string login) {
        try {
            var user = await github.User.Get(login);
            return string.IsNullOrWhiteSpace(user.Name) ? null : user.Name;
        } catch (NotFoundException) {
            return null;
        }
    }

    private readonly record struct RepoStats(int Commits, int PrsOpened, int PrsMerged, int Reviews);

    private static JsonObject StatsToNode(RepoStats stats) => new() {
        ["commits"] = stats.Commits,
        ["prsOpened"] = stats.PrsOpened,
        ["prsMerged"] = stats.PrsMerged,
        ["reviews"] = stats.Reviews,
    };

    private static async Task<RepoStats> ComputeRepoStatsAsync(
        HttpClient http, SearchThrottle throttle, string repo, string login) {
        var commits = await SearchTotalCountAsync(http, throttle, "search/commits", $"repo:{Owner}/{repo} author:{login}");
        var prsOpened = await SearchTotalCountAsync(http, throttle, "search/issues", $"repo:{Owner}/{repo} type:pr author:{login}");
        var prsMerged = await SearchTotalCountAsync(http, throttle, "search/issues", $"repo:{Owner}/{repo} type:pr author:{login} is:merged");
        var reviews = await SearchTotalCountAsync(http, throttle, "search/issues", $"repo:{Owner}/{repo} type:pr reviewed-by:{login}");

        return new RepoStats(commits, prsOpened, prsMerged, reviews);
    }

    // GitHub's Search API (commits and issues/PRs) isn't wrapped by Octokit's typed Search client,
    // so this hits it directly -- same endpoints/qualifiers used to build the #564 audit by hand.
    private static async Task<int> SearchTotalCountAsync(HttpClient http, SearchThrottle throttle, string endpoint, string query) {
        for (var attempt = 0; attempt < 6; attempt++) {
            await throttle.WaitAsync();

            var uri = $"{endpoint}?q={Uri.EscapeDataString(query)}&per_page=1";
            using var response = await http.GetAsync(uri);

            if (response.StatusCode is HttpStatusCode.Forbidden or (HttpStatusCode)429) {
                var wait = GetRetryDelay(response);
                Console.WriteLine($"    Rate limited on \"{query}\", waiting {wait.TotalSeconds:F0}s...");
                await Task.Delay(wait);
                continue;
            }

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            return doc.RootElement.GetProperty("total_count").GetInt32();
        }

        throw new InvalidOperationException($"Search request kept getting rate-limited: {endpoint}?q={query}");
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response) {
        if (response.Headers.TryGetValues("Retry-After", out var retryValues)
            && int.TryParse(retryValues.FirstOrDefault(), out var retrySeconds)) {
            return TimeSpan.FromSeconds(retrySeconds + 1);
        }

        if (response.Headers.TryGetValues("X-RateLimit-Reset", out var resetValues)
            && long.TryParse(resetValues.FirstOrDefault(), out var resetEpoch)) {
            var delay = DateTimeOffset.FromUnixTimeSeconds(resetEpoch) - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2);
            return delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(5);
        }

        return TimeSpan.FromSeconds(30);
    }

    // GitHub's Search API allows ~30 authenticated requests/minute; this keeps calls spaced out
    // rather than relying entirely on reactive 403/429 backoff.
    private sealed class SearchThrottle {
        private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(2200);
        private readonly SemaphoreSlim _lock = new(1, 1);
        private DateTimeOffset _lastCall = DateTimeOffset.MinValue;

        public async Task WaitAsync() {
            await _lock.WaitAsync();
            try {
                var elapsed = DateTimeOffset.UtcNow - _lastCall;
                if (elapsed < MinInterval) {
                    await Task.Delay(MinInterval - elapsed);
                }
                _lastCall = DateTimeOffset.UtcNow;
            } finally {
                _lock.Release();
            }
        }
    }

    // Default encoder hex-escapes anything outside plain ASCII (accented names, em dashes,
    // even embedded quotes) -- UnsafeRelaxedJsonEscaping keeps the file readable/editable by
    // a human, matching how it already reads before this tool ever touches it.
    private static readonly JsonSerializerOptions SerializerOptions = new() {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // Stats sub-objects are written compact/single-line to match the hand-curated style
    // established in #564/#566, so a diff shows one line per changed stat block, not four.
    private static readonly Regex StatsObjectPattern =
        new(@"\{(\s*\n\s*""commits"":.*?)\n\s*\}", RegexOptions.Singleline | RegexOptions.Compiled);

    private static async Task SaveAsync(string path, JsonObject root) {
        var json = root.ToJsonString(SerializerOptions);
        json = StatsObjectPattern.Replace(json, m => {
            var parts = m.Groups[1].Value.Trim('\n', '\r').Split('\n').Select(p => p.Trim().TrimEnd(','));
            return "{ " + string.Join(", ", parts) + " }";
        });
        await File.WriteAllTextAsync(path, json + "\n");
    }
}
