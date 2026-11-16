using System.Text.Json.Nodes;

namespace ContributorStatsSync;

// otherContributions: a summary of everything a person did that the profile doesn't already list
// from code `contributors` lists (problems, solvers, verifiers, reductions, visualizations): CI,
// tests, docs, API/core code, frontend work, and work in the org's other repos. Built from each
// person's merged PRs in every non-fork, non-archived ReduxISU repo, during the --counts-only pass,
// and like the counts it is only filled in at release time (the committed file has none).
internal static partial class Program {
    // Area names, exactly as the frontends display them.
    internal const string ReduxCodeArea = "Redux API and core code";
    internal const string GuiCodeArea = "Redux_GUI interface";
    internal const string FrontendCodeArea = "Redux_Frontend interface";
    internal const string TestsArea = "Tests";
    internal const string CiArea = "CI and build tooling";
    internal const string DocsArea = "Documentation";

    // Files that change in many unrelated PRs and say nothing about what the PR was for.
    private static readonly HashSet<string> IgnoredFileNames = new(StringComparer.OrdinalIgnoreCase) {
        "package-lock.json",
    };
    private const string ContributorInfoPath = "wwwroot/contributorInfo.json";

    private static readonly HashSet<string> CiFileNames = new(StringComparer.OrdinalIgnoreCase) {
        "rbs.toml", "mise.toml", "package.json", "biome.json",
    };
    private static readonly string[] CiExtensions = [".sh", ".ps1", ".csproj", ".slnx", ".sln"];
    private static readonly string[] CiFilePrefixes = ["Dockerfile", "docker-compose", "eslint.config."];
    private static readonly string[] CiRootFolders = [".github/", "Tools/", ".devcontainer/"];

    // The area one changed file belongs to. null means the file is ignored (not counted at all);
    // ProblemWorkArea means problem work, which the profile already lists from code. Rules are
    // checked top to bottom and the first match wins, so a test script under .github/ counts as CI
    // and a README under Tools/ counts as CI too.
    //   1. Problem work: Redux Problems/**
    //   2. Tests: in any repo a test/, tests/, e2e/, integration/ or __tests__/ folder at any depth,
    //      a folder whose name ends in "Tests" or "-tests"/".tests"/"_tests" (redux-tests/,
    //      SPADE.Tests/), and *.test.*, *.spec.*, test_*.py, *_test.*, conftest.py files
    //   3. CI and build tooling: .github/**, Tools/**, .devcontainer/**, *.sh, *.ps1, *.csproj,
    //      *.slnx, *.sln, Dockerfile*, docker-compose*, eslint.config.*, rbs.toml, mise.toml,
    //      package.json, biome.json
    //   4. Documentation: *.md, docs/**
    //   5. Everything else: the repo's own code area (see CodeAreaOf)
    internal const string ProblemWorkArea = "Problem work";

    internal static string? AreaOfFile(string repo, string path) {
        path = path.Replace('\\', '/');
        var fileName = path[(path.LastIndexOf('/') + 1)..];
        var folders = path.Split('/')[..^1];

        if (IgnoredFileNames.Contains(fileName) || (repo == ReduxRepo && path == ContributorInfoPath)) {
            return null;
        }

        if (repo == ReduxRepo && path.StartsWith("Problems/", StringComparison.Ordinal)) {
            return ProblemWorkArea;
        }

        if (folders.Any(IsTestFolder)
            || fileName.Contains(".test.", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains(".spec.", StringComparison.OrdinalIgnoreCase)
            || (fileName.StartsWith("test_", StringComparison.OrdinalIgnoreCase) && fileName.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
            || Path.GetFileNameWithoutExtension(fileName).EndsWith("_test", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("conftest.py", StringComparison.OrdinalIgnoreCase)) {
            return TestsArea;
        }

        if (CiRootFolders.Any(folder => path.StartsWith(folder, StringComparison.Ordinal))
            || CiFileNames.Contains(fileName)
            || CiExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            || CiFilePrefixes.Any(prefix => fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) {
            return CiArea;
        }

        if (fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("docs/", StringComparison.OrdinalIgnoreCase)) {
            return DocsArea;
        }

        return CodeAreaOf(repo);
    }

    private static bool IsTestFolder(string folder) =>
        folder.ToLowerInvariant() is "test" or "tests" or "e2e" or "integration" or "__tests__"
        || folder.EndsWith("Tests", StringComparison.Ordinal)
        || folder.EndsWith("-tests", StringComparison.OrdinalIgnoreCase)
        || folder.EndsWith(".tests", StringComparison.OrdinalIgnoreCase)
        || folder.EndsWith("_tests", StringComparison.OrdinalIgnoreCase);

    // The area for a repo's own code: the three main repos have named areas, any other org repo
    // gets "<RepoName> code" (e.g. "SPADE code").
    internal static string CodeAreaOf(string repo) => repo switch {
        ReduxRepo => ReduxCodeArea,
        GuiRepo => GuiCodeArea,
        FrontendRepo => FrontendCodeArea,
        _ => $"{repo} code",
    };

    // The area a PR goes in: the one holding the most of its changed files. A tie goes to problem
    // work, then the repo's code, then tests, CI, docs: a fix that adds as many test files as it
    // changes code files (Redux_GUI #421: two of each) is code work that came with its tests, not
    // test work. Returns null
    // when the PR is skipped: problem work, or nothing left to count once ignored files are
    // dropped (e.g. a PR that only edits contributorInfo.json). GitHub's files(first: 100) is all
    // the scan fetches, which is plenty to see where a PR's weight is.
    internal static string? AreaOfPullRequest(string repo, IEnumerable<string> paths) {
        var counts = paths.Select(path => AreaOfFile(repo, path)).OfType<string>()
            .GroupBy(area => area).ToDictionary(group => group.Key, group => group.Count());
        if (counts.Count == 0) {
            return null;
        }

        string[] tieOrder = [ProblemWorkArea, CodeAreaOf(repo), TestsArea, CiArea, DocsArea];
        var winner = tieOrder.Where(counts.ContainsKey).MaxBy(area => counts[area])!;
        return winner == ProblemWorkArea ? null : winner;
    }

    // Display order: the three main repos' code, other repos' code alphabetically by repo name,
    // then Tests, CI and build tooling, Documentation.
    internal static (int Group, string Name) AreaDisplayKey(string area) => area switch {
        ReduxCodeArea => (0, ""),
        GuiCodeArea => (1, ""),
        FrontendCodeArea => (2, ""),
        TestsArea => (4, ""),
        CiArea => (5, ""),
        DocsArea => (6, ""),
        _ => (3, area.ToLowerInvariant()),
    };

    internal sealed record MergedPullRequest(
        string Repo, int Number, string Title, string Url, DateTimeOffset MergedAt, string Author, string Area);

    // The otherContributions array for one person: their merged PRs grouped by area (areas with
    // none left out), areas in display order, PRs in each area newest merge first.
    internal static JsonArray OtherContributionsNode(IEnumerable<MergedPullRequest> pullRequests) {
        var areas = pullRequests
            .GroupBy(pr => pr.Area)
            .OrderBy(group => AreaDisplayKey(group.Key).Group)
            .ThenBy(group => AreaDisplayKey(group.Key).Name, StringComparer.Ordinal)
            .Select(group => (JsonNode)new JsonObject {
                ["area"] = group.Key,
                ["pullRequests"] = new JsonArray(group
                    .OrderByDescending(pr => pr.MergedAt)
                    .ThenBy(pr => pr.Repo, StringComparer.Ordinal)
                    .ThenByDescending(pr => pr.Number)
                    .Select(pr => (JsonNode)new JsonObject {
                        ["repo"] = pr.Repo,
                        ["number"] = pr.Number,
                        ["title"] = pr.Title,
                        ["url"] = pr.Url,
                    })
                    .ToArray()),
            });
        return new JsonArray(areas.ToArray());
    }
}
