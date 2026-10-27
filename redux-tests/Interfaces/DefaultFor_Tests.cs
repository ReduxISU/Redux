using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using API.DummyClasses;
using API.Interfaces;
using redux_tests.Fixtures;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Guards for [DefaultFor] (Interfaces/DefaultFor.cs, #919): the attribute must name a real problem, at
// most one class per problem and kind, consistent with the problem's own IProblem<T, U, V> if it has one;
// and a problem on plain IProblem must work through the real endpoints with or without declared defaults
// (fixtures in DefaultFor_Fixtures.cs).
public class DefaultFor_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public DefaultFor_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    // Every class carrying the attribute, from the API and this test assembly, including any the
    // registry ignores because they are not a solver, verifier or visualization.
    private static IEnumerable<(Type Class, DefaultForAttribute Attribute)> AttributedClasses() =>
        new[] { typeof(IProblem).Assembly, typeof(DefaultFor_Tests).Assembly }
            .SelectMany(a => a.GetTypes())
            .SelectMany(t => t.GetCustomAttributes<DefaultForAttribute>(false).Select(a => (t, a)));

    private static string Kind(Type kind) => kind.Name.TrimStart('I');

    // ── The attribute ────────────────────────────────────────────────────────

    [Fact]
    public void DefaultFor_IsOnlyOnSolversVerifiersAndVisualizations() {
        var misplaced = AttributedClasses()
            .Where(x => !ProblemDefaults.Kinds.Any(k => k.IsAssignableFrom(x.Class)) || !x.Class.IsClass || x.Class.IsAbstract)
            .Select(x => x.Class.Name)
            .ToList();

        Assert.True(misplaced.Count == 0,
            "[DefaultFor] must be on a concrete solver, verifier or visualization class: " + string.Join(", ", misplaced));
    }

    [Fact]
    public void DefaultFor_NamesAConcreteProblem() {
        var wrong = AttributedClasses()
            .Where(x => !typeof(IProblem).IsAssignableFrom(x.Attribute.Problem) || !x.Attribute.Problem.IsClass || x.Attribute.Problem.IsAbstract)
            .Select(x => $"{x.Class.Name} -> {x.Attribute.Problem.Name}")
            .ToList();

        Assert.True(wrong.Count == 0,
            "[DefaultFor] must name a concrete IProblem class: " + string.Join(", ", wrong));
    }

    [Fact]
    public void AtMostOneDefault_PerProblemAndKind() {
        var duplicates = ProblemDefaults.Declarations
            .GroupBy(d => (d.Problem, d.Kind))
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.Problem.Name} has {g.Count()} default {Kind(g.Key.Kind)}s: " +
                string.Join(", ", g.Select(d => d.Class.Name)))
            .ToList();

        Assert.True(duplicates.Count == 0, string.Join("\n", duplicates));
    }

    // A class built for one problem (ISolver<CLIQUE>) can't be the default for another.
    [Fact]
    public void DefaultFor_NamesAProblemTheClassHandles() {
        var mismatched = new List<string>();
        foreach (var d in ProblemDefaults.Declarations) {
            Type open = d.Kind == typeof(ISolver) ? typeof(ISolver<>)
                : d.Kind == typeof(IVerifier) ? typeof(IVerifier<>)
                : typeof(IVisualization<>);
            Type? handles = ProblemProvider.ProblemTypeOf(d.Class, open);
            if (handles != null && !handles.IsAssignableFrom(d.Problem))
                mismatched.Add($"{d.Class.Name} handles {handles.Name} but is [DefaultFor({d.Problem.Name})]");
        }

        Assert.True(mismatched.Count == 0, string.Join("\n", mismatched));
    }

    // A problem on IProblem<T, U, V> names its defaults in the type; an attribute claiming otherwise would
    // be silently ignored, so it must agree.
    [Fact]
    public void GenericProblems_AgreeWithTheirDefaultFor() {
        var disagreements = new List<string>();
        foreach (var d in ProblemDefaults.Declarations) {
            Type? generic = d.Problem.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IProblem<,,>));
            if (generic == null) continue;
            Type declared = generic.GetGenericArguments()[Array.IndexOf(ProblemDefaults.Kinds, d.Kind)];
            if (declared != d.Class)
                disagreements.Add($"{d.Problem.Name}'s default {Kind(d.Kind)} is {declared.Name}, but {d.Class.Name} is [DefaultFor({d.Problem.Name})]");
        }

        Assert.True(disagreements.Count == 0, string.Join("\n", disagreements));
    }

    [Fact]
    public void Scan_RecordsOneDeclarationPerKindTheClassImplements() {
        var found = ProblemDefaults.Scan(new[] { typeof(AttributedFixtureVerifier), typeof(DummySolver) });

        var d = Assert.Single(found);
        Assert.Equal(new ProblemDefaults.Declaration(typeof(ATTRIBUTEDFIXTURE), typeof(IVerifier), typeof(AttributedFixtureVerifier)), d);
    }

    // ── Resolution ───────────────────────────────────────────────────────────

    [Fact]
    public void ProblemWithNoDefaults_GetsThePlaceholders() {
        IProblem problem = new NODEFAULTSFIXTURE();

        Assert.IsType<DummySolver>(problem.defaultSolver);
        Assert.IsType<DummyVerifier>(problem.defaultVerifier);
        Assert.IsType<DummyVisualization>(problem.defaultVisualization);
    }

    [Fact]
    public void ProblemWithADefaultFor_GetsThatClass_AndPlaceholdersForTheRest() {
        IProblem problem = new ATTRIBUTEDFIXTURE();

        Assert.IsType<AttributedFixtureVerifier>(problem.defaultVerifier);
        Assert.IsType<DummySolver>(problem.defaultSolver);
        Assert.IsType<DummyVisualization>(problem.defaultVisualization);
    }

    private class SubclassedFixture : ATTRIBUTEDFIXTURE { }

    [Fact]
    public void Subclass_InheritsTheBaseProblemsDefaults() {
        Assert.IsType<AttributedFixtureVerifier>(((IProblem)new SubclassedFixture()).defaultVerifier);
    }

    // ── Through the endpoints ────────────────────────────────────────────────

    private async Task<(HttpStatusCode Status, string Body)> Get(string url) {
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task<(HttpStatusCode Status, string Body)> Post(string url, string json) {
        var response = await _client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private Task<(HttpStatusCode Status, string Body)> Verify(string verifier, string instance, string certificate) =>
        Post($"/ProblemProvider/verify?verifier={verifier}",
            JsonSerializer.Serialize(new { ProblemInstance = instance, Certificate = certificate }));

    // System.Text.Json leaves default interface members out, so these three are added for problems
    // that inherit them (ProblemDefaultsJson); both front ends read them from /info.
    [Fact]
    public async Task Info_ShowsThePlaceholderDefaults() {
        var (status, body) = await Get("/ProblemProvider/info?interface=NODEFAULTSFIXTURE");

        Assert.Equal(HttpStatusCode.OK, status);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.Equal("No Defaults Fixture", root.GetProperty("problemName").GetString());
        Assert.Contains("placeholder", root.GetProperty("defaultSolver").GetProperty("solverDefinition").GetString());
        Assert.Contains("placeholder", root.GetProperty("defaultVerifier").GetProperty("verifierDefinition").GetString());
        Assert.Equal("Unimplemented", root.GetProperty("defaultVisualization").GetProperty("visualizationType").GetString());
    }

    [Fact]
    public async Task Info_ShowsTheDefaultForVerifier() {
        var (status, body) = await Get("/ProblemProvider/info?interface=ATTRIBUTEDFIXTURE");

        Assert.Equal(HttpStatusCode.OK, status);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Attributed Fixture Solution Verifier",
            doc.RootElement.GetProperty("defaultVerifier").GetProperty("verifierName").GetString());
        Assert.Equal("", doc.RootElement.GetProperty("defaultSolver").GetProperty("solverName").GetString());
    }

    [Fact]
    public async Task AllInfo_ShowsTheDefaults() {
        var (status, body) = await Get("/Navigation/Batch/allInfo");

        Assert.Equal(HttpStatusCode.OK, status);
        using var doc = JsonDocument.Parse(body);
        var fixture = doc.RootElement.GetProperty(nameof(ATTRIBUTEDFIXTURE));
        Assert.Equal("Attributed Fixture Solution Verifier",
            fixture.GetProperty("defaultVerifier").GetProperty("verifierName").GetString());
        Assert.True(fixture.TryGetProperty("defaultSolver", out _));
        Assert.True(fixture.TryGetProperty("defaultVisualization", out _));
    }

    [Fact]
    public async Task ProblemInstance_Parses() {
        var (status, body) = await Post("/ProblemProvider/problemInstance?problem=NODEFAULTSFIXTURE", "\"{4,5}\"");

        Assert.Equal(HttpStatusCode.OK, status);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(2, doc.RootElement.GetProperty("numbers").GetArrayLength());
    }

    [Fact]
    public async Task Solve_WithThePlaceholderSolver_AnswersEmpty() {
        var (status, body) = await Post("/ProblemProvider/solve?solver=DummySolver", "\"{1,2,3}\"");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("\"\"", body);
    }

    [Fact]
    public async Task Visualize_WithThePlaceholderVisualization_DrawsNothing() {
        var (status, body) = await Post("/ProblemProvider/visualize?visualization=DummyVisualization", "\"{1,2,3}\"");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("[]", body);

        (status, body) = await Post("/ProblemProvider/visualize?visualization=DummyVisualization&format=frames", "\"{1,2,3}\"");
        Assert.Equal(HttpStatusCode.OK, status);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Unimplemented", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("payload").ValueKind);
    }

    [Fact]
    public async Task Verify_WithThePlaceholderVerifier_Returns400NoVerifier() {
        var (status, body) = await Verify("DummyVerifier", "{1,2,3}", "2");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("no_verifier", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Verify_WithTheDefaultForVerifier_Works() {
        Assert.Equal((HttpStatusCode.OK, "\"True\""), await Verify(nameof(AttributedFixtureVerifier), "{1,2,3}", "2"));
        Assert.Equal((HttpStatusCode.OK, "\"False\""), await Verify(nameof(AttributedFixtureVerifier), "{1,2,3}", "9"));

        var (status, body) = await Verify(nameof(AttributedFixtureVerifier), "{1,2,3}", "two");
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("certificate_parse_error", body);
    }

    [Fact]
    public async Task Navigation_ListsOnlyTheDeclaredClasses() {
        var (_, verifiers) = await Get("/Navigation/Problem_VerifiersRefactor?chosenProblem=ATTRIBUTEDFIXTURE");
        Assert.Equal(new[] { nameof(AttributedFixtureVerifier) }, JsonSerializer.Deserialize<string[]>(verifiers));

        var (_, solvers) = await Get("/Navigation/Problem_SolversRefactor?chosenProblem=NODEFAULTSFIXTURE");
        Assert.Equal("entered a solver that does not exist", JsonSerializer.Deserialize<string>(solvers));

        var (_, visualizations) = await Get("/Navigation/Problem_VisualizationsRefactor?chosenProblem=NODEFAULTSFIXTURE");
        Assert.Empty(JsonSerializer.Deserialize<string[]>(visualizations)!);

        var (_, allVerifiers) = await Get("/Navigation/Batch/allVerifiers");
        var map = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(allVerifiers)!;
        Assert.Equal(new[] { nameof(AttributedFixtureVerifier) }, map[nameof(ATTRIBUTEDFIXTURE)]);
    }

    // The fixtures are outside API.Problems.<Folder>.<Problem>, so the problem lists leave them out; a real
    // problem under Problems/ is listed whether or not it declares defaults.
    [Fact]
    public async Task ProblemLists_LeaveTheFixturesOut() {
        var (_, all) = await Get("/Navigation/Batch/allProblems");
        var names = JsonSerializer.Deserialize<string[]>(all)!;
        Assert.DoesNotContain(nameof(NODEFAULTSFIXTURE), names);
        Assert.DoesNotContain(nameof(ATTRIBUTEDFIXTURE), names);
    }
}
