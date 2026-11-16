using System.Text.Json.Nodes;
using Xunit;
using Tool = ContributorStatsSync.Program;

namespace redux_tests;
#pragma warning disable CS1591

// The rules that sort a merged PR into one otherContributions area (Tools/ContributorStatsSync).
[Trait("Category", "Metadata")]
public class OtherContributions_Tests {

    [Theory]
    [InlineData("Redux", "Problems/NPComplete/NPC_SAT/SAT_Class.cs", Tool.ProblemWorkArea)]
    [InlineData("Redux", "redux-tests/Navigation/ContributorProfile_Tests.cs", Tool.TestsArea)]
    [InlineData("Redux", "integration/run.py", Tool.TestsArea)]
    [InlineData("Redux_GUI", "tests/unit/search-bar.test.mjs", Tool.TestsArea)]
    [InlineData("Redux_Frontend", "e2e/board.spec.ts", Tool.TestsArea)]
    [InlineData("Redux_Frontend", "src/components/Card.test.tsx", Tool.TestsArea)]
    [InlineData("SPADE", "SPADE.Tests/ParserTests.cs", Tool.TestsArea)]
    [InlineData("Redux_Build_System", "test/report.rs", Tool.TestsArea)]
    [InlineData("mcpredux", "integration/test_mcp.py", Tool.TestsArea)]
    [InlineData("mcpredux", "conftest.py", Tool.TestsArea)]
    [InlineData("mcpredux", "server/test_tools.py", Tool.TestsArea)]
    [InlineData("Redux_VR", "pkg/scene_test.go", Tool.TestsArea)]
    [InlineData("Redux", ".github/workflows/docker.yml", Tool.CiArea)]
    [InlineData("Redux", "Tools/fill-contributor-counts.sh", Tool.CiArea)]
    [InlineData("Redux", "Tools/ContributorStatsSync/Program.cs", Tool.CiArea)]
    [InlineData("Redux", "API.csproj", Tool.CiArea)]
    [InlineData("Redux", "rbs.toml", Tool.CiArea)]
    [InlineData("Redux", "Dockerfile", Tool.CiArea)]
    [InlineData("Redux_GUI", "docker-compose.yml", Tool.CiArea)]
    [InlineData("Redux_GUI", "package.json", Tool.CiArea)]
    [InlineData("Redux_Frontend", "eslint.config.mjs", Tool.CiArea)]
    [InlineData("Redux_Frontend", "biome.json", Tool.CiArea)]
    [InlineData("SPADE", "SPADE.slnx", Tool.CiArea)]
    [InlineData("Redux", ".devcontainer/devcontainer.json", Tool.CiArea)]
    [InlineData("Redux", "README.md", Tool.DocsArea)]
    [InlineData("Redux", "docs/visualization-guide.html", Tool.DocsArea)]
    [InlineData("Redux_Frontend", "src/notes/README.md", Tool.DocsArea)]
    [InlineData("Redux", "AdditionalControllers/Navigation/ContributerProfile.cs", Tool.ReduxCodeArea)]
    [InlineData("Redux", "Interfaces/IProblem.cs", Tool.ReduxCodeArea)]
    [InlineData("Redux_GUI", "components/widgets/SearchBarExtensible.js", Tool.GuiCodeArea)]
    [InlineData("Redux_Frontend", "src/app/page.tsx", Tool.FrontendCodeArea)]
    [InlineData("SPADE", "SPADE/Parser.cs", "SPADE code")]
    [InlineData("Redux_Build_System", "src/main.rs", "Redux_Build_System code")]
    // Problems/** only means problem work in Redux; elsewhere it is that repo's code.
    [InlineData("Redux_GUI", "Problems/list.js", Tool.GuiCodeArea)]
    public void AreaOfFile_FollowsTheRules(string repo, string path, string expected) {
        Assert.Equal(expected, Tool.AreaOfFile(repo, path));
    }

    [Theory]
    [InlineData("Redux", "wwwroot/contributorInfo.json")]
    [InlineData("Redux_GUI", "package-lock.json")]
    [InlineData("Redux_Frontend", "app/package-lock.json")]
    public void AreaOfFile_IgnoresBookkeepingFiles(string repo, string path) {
        Assert.Null(Tool.AreaOfFile(repo, path));
    }

    [Fact]
    public void AreaOfPullRequest_PicksTheAreaWithTheMostFiles() {
        Assert.Equal(Tool.TestsArea, Tool.AreaOfPullRequest("Redux",
            ["redux-tests/A_Tests.cs", "redux-tests/B_Tests.cs", "Interfaces/IProblem.cs"]));
    }

    [Fact]
    public void AreaOfPullRequest_TieGoesToCodeOverTests() {
        // Redux_GUI #421: two component files and two test files is interface work with its tests.
        Assert.Equal(Tool.GuiCodeArea, Tool.AreaOfPullRequest("Redux_GUI", [
            "components/widgets/SearchBarExtensible.js", "components/widgets/optionKeys.js",
            "tests/unit/search-bar-call-sites.test.mjs", "tests/unit/search-bar-option-keys.test.mjs"]));
    }

    [Fact]
    public void AreaOfPullRequest_TieOrderAfterCodeIsTestsCiDocs() {
        Assert.Equal(Tool.TestsArea, Tool.AreaOfPullRequest("Redux", ["redux-tests/A.cs", ".github/x.yml", "README.md"]));
        Assert.Equal(Tool.CiArea, Tool.AreaOfPullRequest("Redux", [".github/x.yml", "README.md"]));
    }

    [Fact]
    public void AreaOfPullRequest_ProblemWorkIsSkipped() {
        Assert.Null(Tool.AreaOfPullRequest("Redux", ["Problems/NPComplete/NPC_SAT/SAT_Class.cs", "redux-tests/SAT_Tests.cs"]));
    }

    [Fact]
    public void AreaOfPullRequest_OnlyIgnoredFiles_IsSkipped() {
        Assert.Null(Tool.AreaOfPullRequest("Redux", ["wwwroot/contributorInfo.json"]));
        Assert.Null(Tool.AreaOfPullRequest("Redux", []));
    }

    [Fact]
    public void AreaOfPullRequest_IgnoredFilesDontOutvoteRealWork() {
        Assert.Equal(Tool.GuiCodeArea, Tool.AreaOfPullRequest("Redux_GUI",
            ["package-lock.json", "package-lock.json", "components/A.js"]));
    }

    [Fact]
    public void OtherContributionsNode_GroupsAndOrdersAsDocumented() {
        var t0 = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        Tool.MergedPullRequest Pr(string repo, int number, string area, int day) =>
            new(repo, number, $"PR {number}", $"https://github.com/ReduxISU/{repo}/pull/{number}", t0.AddDays(day), "ada", area);

        var node = Tool.OtherContributionsNode([
            Pr("Redux", 10, Tool.DocsArea, 1),
            Pr("Redux", 11, Tool.CiArea, 1),
            Pr("SPADE", 3, "SPADE code", 1),
            Pr("Redux_Build_System", 4, "Redux_Build_System code", 1),
            Pr("Redux_GUI", 5, Tool.TestsArea, 1),
            Pr("Redux_Frontend", 6, Tool.FrontendCodeArea, 1),
            Pr("Redux_GUI", 7, Tool.GuiCodeArea, 1),
            Pr("Redux", 8, Tool.ReduxCodeArea, 1),
            Pr("Redux", 9, Tool.ReduxCodeArea, 3),
            Pr("Redux", 12, Tool.ReduxCodeArea, 2),
        ]);

        Assert.Equal(new[] {
            Tool.ReduxCodeArea, Tool.GuiCodeArea, Tool.FrontendCodeArea, "Redux_Build_System code", "SPADE code",
            Tool.TestsArea, Tool.CiArea, Tool.DocsArea,
        }, node.Select(area => area!["area"]!.GetValue<string>()));

        // Newest merge first, whatever the PR numbers.
        var redux = node[0]!["pullRequests"]!.AsArray();
        Assert.Equal(new[] { 9, 12, 8 }, redux.Select(pr => pr!["number"]!.GetValue<int>()));
        var first = redux[0]!.AsObject();
        Assert.Equal(new[] { "repo", "number", "title", "url" }, first.Select(kv => kv.Key));
        Assert.Equal("https://github.com/ReduxISU/Redux/pull/9", first["url"]!.GetValue<string>());
    }

    [Fact]
    public void OtherContributionsNode_NothingIsAnEmptyArray() {
        Assert.Equal("[]", Tool.OtherContributionsNode([]).ToJsonString());
    }
}
