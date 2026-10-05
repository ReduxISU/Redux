using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
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

    [Fact]
    public async Task ProblemTemplate_IncludesGeneratedTestFile() {
        var entries = await GetZipEntries("/ProblemTemplate?problemName=Traveling%20Sales%20Person");

        // The path tells students where the file goes in the repo.
        var key = "redux-tests/Problems/NPC_TRAVELINGSALESPERSON/TRAVELINGSALESPERSON_Tests.cs";
        Assert.Contains(key, entries.Keys);

        var tests = entries[key];
        AssertNoPlaceholders(tests);
        Assert.Contains("public class TRAVELINGSALESPERSON_Tests", tests);
        Assert.Contains("NPC_TRAVELINGSALESPERSON.Solvers", tests);
        Assert.Contains("TravelingSalesPersonSolver", tests);
        Assert.Contains("ProblemParseException", tests);
        Assert.Contains("CertificateParseException", tests);
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

    // ── Generated-code regression tests ───────────────────────────────────────
    // The templates declare every required metadata member explicitly (as Unclassified / "")
    // and throw NotImplementedException from unfinished stubs, so students must decide each
    // value. These tests keep the templates from drifting back to hidden or silently-wrong code.

    private const string ProblemUrl = "/ProblemTemplate?problemName=Traveling%20Sales%20Person";
    private const string ReductionUrl =
        "/ProblemTemplate/reduction?problemFrom=SAT3&problemTo=CLIQUE&reductionName=Sat3%20To%20Clique";

    private static bool Declares(string source, string declaration) {
        return Regex.IsMatch(source, @"^\s*public " + Regex.Escape(declaration), RegexOptions.Multiline);
    }

    [Fact]
    public async Task ProblemTemplate_VerifierUsesDefaultVerifierName() {
        var entries = await GetZipEntries(ProblemUrl);
        var verifier = entries["NPC_TRAVELINGSALESPERSON/Verifiers/TravelingSalesPersonVerifier.cs"];

        Assert.Contains("\"Default Traveling Sales Person Verifier\"", verifier);
        Assert.Contains("class TravelingSalesPersonVerifier", verifier);
    }

    [Fact]
    public async Task GeneratedCode_NeverReturnsTrueFromAStub() {
        var generated = (await GetZipEntries(ProblemUrl))
            .Concat(await GetZipEntries(ReductionUrl))
            .Where(e => e.Key.EndsWith(".cs"));

        foreach (var (name, content) in generated) {
            Assert.False(content.Contains("return true;"), $"{name} contains 'return true;'");
        }
    }

    [Fact]
    public async Task ProblemTemplate_VisualizationDoesNotNullTheSolver() {
        var entries = await GetZipEntries(ProblemUrl);
        var visualization = entries["NPC_TRAVELINGSALESPERSON/Visualizations/TravelingSalesPersonVisualization.cs"];

        Assert.DoesNotContain("= null", visualization);
        Assert.Contains("new TRAVELINGSALESPERSON().defaultSolver", visualization);
    }

    [Fact]
    public async Task ProblemTemplate_ClassDeclaresMetadataExplicitly() {
        var entries = await GetZipEntries(ProblemUrl);
        var classFile = entries["NPC_TRAVELINGSALESPERSON/TRAVELINGSALESPERSON_Class.cs"];

        Assert.True(Declares(classFile, "ComplexityClass complexityClass"), "complexityClass must be declared on an uncommented line");
        Assert.True(Declares(classFile, "ProblemType problemType"), "problemType must be declared on an uncommented line");
    }

    [Fact]
    public async Task ProblemTemplate_SolverDeclaresMetadataExplicitly() {
        var entries = await GetZipEntries(ProblemUrl);
        var solver = entries["NPC_TRAVELINGSALESPERSON/Solvers/TravelingSalesPersonSolver.cs"];

        Assert.True(Declares(solver, "SolverType solverType"), "solverType must be declared");
        Assert.True(Declares(solver, "SolverComplexityBucket complexityBucket"), "complexityBucket must be declared");
        Assert.True(Declares(solver, "string complexity"), "complexity must be declared");
        Assert.Contains("throw new NotImplementedException", solver);
    }

    [Fact]
    public async Task Reduction_DeclaresMetadataExplicitly() {
        var entries = await GetZipEntries(ReductionUrl);
        var reduction = Assert.Single(entries.Values);

        Assert.True(Declares(reduction, "ReductionCost cost"), "cost must be declared");
        Assert.True(Declares(reduction, "ReductionType reductionType"), "reductionType must be declared");
        Assert.True(Declares(reduction, "ReductionComplexityBucket complexityBucket"), "complexityBucket must be declared");
        Assert.True(Declares(reduction, "string? complexity"), "complexity must be declared");
        Assert.Contains("problemFromSolution", reduction);
    }
}
