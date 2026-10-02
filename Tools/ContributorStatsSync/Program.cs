using System.Net;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Octokit;

namespace ContributorStatsSync;

// Keeps wwwroot/contributorInfo.json's reduxStats/reduxGuiStats in sync with GitHub (see issues
// #565 and #613). Two explicit modes, exactly one required:
//
//   --scan-only    (weekly job) auto-detects new GitHub identities committing to either repo that
//                  aren't tracked yet and writes them as stub entries with zeroed counts, for a
//                  human to review. No counting happens here.
//   --counts-only  (release build) fills prsMerged/reviews for every contributor who already has
//                  a githubUsername, by scanning every PR of each repo once through the GraphQL
//                  API -- a handful of requests total. (Per-contributor Search API calls took
//                  ~9 minutes because of Search's rate limit, far too slow for a release build.)
//
// Counts are kept at zero in the committed file on purpose: they go stale the moment they're
// written and would make the weekly PR noisy, so they are only filled in at release time by the
// docker workflow, and a test fails if non-zero counts get committed. Commit counts and
// PRs-opened counts are deliberately not tracked -- PRs merged and PRs reviewed are what the
// About Us page displays.
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
        var countsOnly = args.Contains("--counts-only");
        if (scanOnly == countsOnly) {
            Console.Error.WriteLine("""
                Specify exactly one mode:
                  --scan-only    find new contributors and add them as stub entries with zeroed counts
                  --counts-only  fill PRs-merged/reviewed counts for existing contributors (GraphQL)
                Optional: --dry-run (compute and print, write nothing), --only=login1,login2 (with
                --counts-only), and a path to contributorInfo.json (default wwwroot/contributorInfo.json).
                """);
            return 1;
        }

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

        if (scanOnly) {
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
                        ["reduxStats"] = StatsToNode(default),
                        ["reduxGuiStats"] = StatsToNode(default),
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
            return await FinishAsync(jsonPath, root, newEntryNames, dryRun);
        }

        var loginsToProcess = onlyLogins ?? new HashSet<string>(knownLoginToName.Keys, StringComparer.OrdinalIgnoreCase);

        Console.WriteLine($"Computing stats for {loginsToProcess.Count} contributor(s)...");
        // Both scans finish before any entry is touched, so a failed scan throws here and
        // never leaves a half-filled file behind.
        var reduxScan = await ScanRepoAsync(http, ReduxRepo);
        var guiScan = await ScanRepoAsync(http, GuiRepo);
        Console.WriteLine($"  Scanned {reduxScan.PullRequestCount + guiScan.PullRequestCount} pull request(s) "
            + $"in {GraphQlRequestCount} GraphQL request(s).");

        foreach (var login in loginsToProcess) {
            if (!knownLoginToName.TryGetValue(login, out var name)) {
                Console.WriteLine($"  Skipping {login} -- not found in contributorInfo.json.");
                continue;
            }

            Console.WriteLine($"  {name} ({login})");
            var entry = root[name]!.AsObject();
            entry["reduxStats"] = StatsToNode(reduxScan.StatsFor(login));
            entry["reduxGuiStats"] = StatsToNode(guiScan.StatsFor(login));
        }

        return await FinishAsync(jsonPath, root, newEntryNames, dryRun);
    }

    private static async Task<int> FinishAsync(string jsonPath, JsonObject root, List<string> newEntryNames, bool dryRun) {
        var entries = $"{newEntryNames.Count} new entr{(newEntryNames.Count == 1 ? "y" : "ies")}";
        if (dryRun) {
            Console.WriteLine($"Dry run -- not writing the file. {entries} would be added:");
            foreach (var name in newEntryNames) {
                Console.WriteLine($"  {name}");
            }
            return 0;
        }

        await SaveAsync(jsonPath, root);
        Console.WriteLine($"Wrote {jsonPath}. {entries} added.");
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

    private readonly record struct RepoStats(int PrsMerged, int Reviews);

    private static JsonObject StatsToNode(RepoStats stats) => new() {
        ["prsMerged"] = stats.PrsMerged,
        ["reviews"] = stats.Reviews,
    };

    private sealed class RepoScan {
        public int PullRequestCount { get; set; }
        public Dictionary<string, int> PrsMerged { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> Reviews { get; } = new(StringComparer.OrdinalIgnoreCase);

        public RepoStats StatsFor(string login) =>
            new(PrsMerged.GetValueOrDefault(login), Reviews.GetValueOrDefault(login));
    }

    private static int GraphQlRequestCount;

    private const string PullRequestsQuery = """
        query($owner: String!, $repo: String!, $cursor: String) {
          repository(owner: $owner, name: $repo) {
            pullRequests(first: 100, after: $cursor, states: [OPEN, CLOSED, MERGED]) {
              pageInfo { hasNextPage endCursor }
              nodes {
                number
                merged
                author { login }
                reviews(first: 100) {
                  pageInfo { hasNextPage endCursor }
                  nodes { author { login } }
                }
              }
            }
          }
        }
        """;

    private const string MoreReviewsQuery = """
        query($owner: String!, $repo: String!, $number: Int!, $cursor: String) {
          repository(owner: $owner, name: $repo) {
            pullRequest(number: $number) {
              reviews(first: 100, after: $cursor) {
                pageInfo { hasNextPage endCursor }
                nodes { author { login } }
              }
            }
          }
        }
        """;

    // One pass over every PR of the repo. A PR counts as "reviewed" by a login once, no matter how
    // many reviews that login left on it -- same as the Search API's reviewed-by: qualifier, which
    // returns PRs rather than individual reviews.
    private static async Task<RepoScan> ScanRepoAsync(HttpClient http, string repo) {
        var scan = new RepoScan();
        string? cursor = null;
        while (true) {
            var data = await PostGraphQlAsync(http, PullRequestsQuery, new JsonObject {
                ["owner"] = Owner,
                ["repo"] = repo,
                ["cursor"] = cursor,
            });
            var connection = data["repository"]!["pullRequests"]!;

            foreach (var pr in connection["nodes"]!.AsArray()) {
                scan.PullRequestCount++;
                var number = pr!["number"]!.GetValue<int>();
                var author = pr["author"]?["login"]?.GetValue<string>();

                if (author is not null && pr["merged"]!.GetValue<bool>()) {
                    scan.PrsMerged[author] = scan.PrsMerged.GetValueOrDefault(author) + 1;
                }

                var reviewers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var reviews = pr["reviews"]!;
                CollectReviewers(reviews["nodes"]!.AsArray(), reviewers);

                // Past 100 reviews on one PR: keep paging so a heavily-reviewed PR isn't undercounted.
                var more = reviews["pageInfo"]!["hasNextPage"]!.GetValue<bool>();
                var reviewCursor = reviews["pageInfo"]!["endCursor"]?.GetValue<string>();
                while (more) {
                    var extra = (await PostGraphQlAsync(http, MoreReviewsQuery, new JsonObject {
                        ["owner"] = Owner,
                        ["repo"] = repo,
                        ["number"] = number,
                        ["cursor"] = reviewCursor,
                    }))["repository"]!["pullRequest"]!["reviews"]!;
                    CollectReviewers(extra["nodes"]!.AsArray(), reviewers);
                    more = extra["pageInfo"]!["hasNextPage"]!.GetValue<bool>();
                    reviewCursor = extra["pageInfo"]!["endCursor"]?.GetValue<string>();
                }

                foreach (var reviewer in reviewers) {
                    scan.Reviews[reviewer] = scan.Reviews.GetValueOrDefault(reviewer) + 1;
                }
            }

            if (!connection["pageInfo"]!["hasNextPage"]!.GetValue<bool>()) {
                return scan;
            }
            cursor = connection["pageInfo"]!["endCursor"]!.GetValue<string>();
        }
    }

    // Reviews by deleted ("ghost") accounts come back with a null author -- no login to credit.
    private static void CollectReviewers(JsonArray reviews, HashSet<string> reviewers) {
        foreach (var review in reviews) {
            var login = review?["author"]?["login"]?.GetValue<string>();
            if (login is not null) {
                reviewers.Add(login);
            }
        }
    }

    // Throws on any HTTP failure, rate limit, or GraphQL "errors" entry (which GitHub returns
    // inside a 200) so the caller can never write a file built from partial data.
    private static async Task<JsonNode> PostGraphQlAsync(HttpClient http, string query, JsonObject variables) {
        GraphQlRequestCount++;
        var body = new JsonObject { ["query"] = query, ["variables"] = variables };
        using var content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        using var response = await http.PostAsync("graphql", content);
        var text = await response.Content.ReadAsStringAsync();

        if (response.StatusCode is HttpStatusCode.Forbidden or (HttpStatusCode)429) {
            throw new InvalidOperationException(
                $"GitHub GraphQL request was rate limited (HTTP {(int)response.StatusCode}); "
                + $"retry in about {GetRetryDelay(response).TotalSeconds:F0}s. Response: {text}");
        }
        if (!response.IsSuccessStatusCode) {
            throw new InvalidOperationException($"GitHub GraphQL request failed (HTTP {(int)response.StatusCode}): {text}");
        }

        var json = JsonNode.Parse(text)!;
        if (json["errors"] is JsonArray errors && errors.Count > 0) {
            throw new InvalidOperationException($"GitHub GraphQL returned errors: {errors.ToJsonString()}");
        }
        return json["data"] ?? throw new InvalidOperationException("GitHub GraphQL response had no data.");
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
        new(@"\{(\s*\n\s*""prsMerged"":.*?)\n\s*\}", RegexOptions.Singleline | RegexOptions.Compiled);

    private static async Task SaveAsync(string path, JsonObject root) {
        var json = root.ToJsonString(SerializerOptions);
        json = StatsObjectPattern.Replace(json, m => {
            var parts = m.Groups[1].Value.Trim('\n', '\r').Split('\n').Select(p => p.Trim().TrimEnd(','));
            return "{ " + string.Join(", ", parts) + " }";
        });
        await File.WriteAllTextAsync(path, json + "\n");
    }
}
