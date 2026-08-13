using System.IO.Compression;
using System.Net;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Endpoint tests for the /ProblemTemplate controller (issue #389).
//
// The controller reads its template files from AppContext.BaseDirectory, and
// API.csproj copies ProblemTemplate/Templates/** to the output/publish dir. That
// content flows transitively into the redux-tests output dir, so these tests
// exercise the real packaging + path-resolution fix rather than the build-time
// source tree — i.e. they would fail the same way a deployed container did.
public class ProblemTemplate_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public ProblemTemplate_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    // Placeholder tokens that must never survive substitution in generated files.
    private static readonly string[] Placeholders =
    {
        "{NAME", "{PROBLEM", "{SOLVER", "{VERIFIER", "{VISUALIZATION", "{REDUCE", "{REDUCTION",
    };

    private async Task<Dictionary<string, string>> GetZipEntries(string url) {
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(bytes);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var entries = new Dictionary<string, string>();
        foreach (var entry in archive.Entries) {
            using var reader = new StreamReader(entry.Open());
            entries[entry.FullName] = reader.ReadToEnd();
        }
        return entries;
    }

    private static void AssertNoPlaceholders(string content) {
        foreach (var token in Placeholders) {
            Assert.DoesNotContain(token, content);
        }
    }

    // ── GET /ProblemTemplate ──────────────────────────────────────────────────

    [Fact]
    public async Task ProblemTemplate_Returns200WithExpectedEntries() {
        var entries = await GetZipEntries("/ProblemTemplate?problemName=Traveling%20Sales%20Person");

        Assert.Contains("README.md", entries.Keys);
        Assert.Contains("NPC_TRAVELINGSALESPERSON/TRAVELINGSALESPERSON_Class.cs", entries.Keys);
        Assert.Contains("NPC_TRAVELINGSALESPERSON/Solvers/TravelingSalesPersonSolver.cs", entries.Keys);
        Assert.Contains("NPC_TRAVELINGSALESPERSON/Verifiers/TravelingSalesPersonVerifier.cs", entries.Keys);
        Assert.Contains("NPC_TRAVELINGSALESPERSON/Visualizations/TravelingSalesPersonVisualization.cs", entries.Keys);
    }

    [Fact]
    public async Task ProblemTemplate_SubstitutesAllPlaceholders() {
        var entries = await GetZipEntries("/ProblemTemplate?problemName=Traveling%20Sales%20Person");
        var classFile = entries["NPC_TRAVELINGSALESPERSON/TRAVELINGSALESPERSON_Class.cs"];

        AssertNoPlaceholders(classFile);
        Assert.Contains("TravelingSalesPerson", classFile); // pascal case substituted
        Assert.Contains("TRAVELINGSALESPERSON", classFile); // upper case substituted
    }

    // ── GET /ProblemTemplate/reduction ────────────────────────────────────────

    [Fact]
    public async Task Reduction_Returns200WithSubstitutedFile() {
        var entries = await GetZipEntries(
            "/ProblemTemplate/reduction?problemFrom=SAT3&problemTo=CLIQUE&reductionName=Sat3%20To%20Clique");

        var key = Assert.Single(entries.Keys);
        Assert.StartsWith("NPC_SAT3/ReduceTo/NPC_CLIQUE/", key);
        Assert.EndsWith(".cs", key);
        AssertNoPlaceholders(entries[key]);
        Assert.Contains("SAT3", entries[key]);
        Assert.Contains("CLIQUE", entries[key]);
    }

    // ── GET /ProblemTemplate/solver ───────────────────────────────────────────

    [Fact]
    public async Task Solver_Returns200WithExpectedEntries() {
        var entries = await GetZipEntries(
            "/ProblemTemplate/solver?problemName=CLIQUE&solverName=My%20Clique%20Solver");

        Assert.Contains("README.md", entries.Keys);
        Assert.Contains("NPC_CLIQUE/Solvers/MyCliqueSolver.cs", entries.Keys);
        AssertNoPlaceholders(entries["NPC_CLIQUE/Solvers/MyCliqueSolver.cs"]);
    }

    // ── GET /ProblemTemplate/verifier ─────────────────────────────────────────

    [Fact]
    public async Task Verifier_Returns200WithExpectedEntries() {
        var entries = await GetZipEntries(
            "/ProblemTemplate/verifier?problemName=CLIQUE&verifierName=My%20Clique%20Verifier");

        Assert.Contains("README.md", entries.Keys);
        Assert.Contains("NPC_CLIQUE/Verifiers/MyCliqueVerifier.cs", entries.Keys);
        AssertNoPlaceholders(entries["NPC_CLIQUE/Verifiers/MyCliqueVerifier.cs"]);
    }

    // ── GET /ProblemTemplate/visualization ────────────────────────────────────
    // Regression for the PROBLEMVisualization.txt casing bug: this endpoint 500'd
    // on any case-sensitive filesystem before the fix.

    [Fact]
    public async Task Visualization_Returns200WithExpectedEntries() {
        var entries = await GetZipEntries(
            "/ProblemTemplate/visualization?problemName=CLIQUE&visualizationName=My%20Clique%20Visualization");

        Assert.Contains("README.md", entries.Keys);
        Assert.Contains("NPC_CLIQUE/Visualizations/MyCliqueVisualization.cs", entries.Keys);
        AssertNoPlaceholders(entries["NPC_CLIQUE/Visualizations/MyCliqueVisualization.cs"]);
    }
}
