using System.Net;
using System.Text.Json;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Guards for the `sourceFile` property on problems, verifiers, visualizations, solvers,
// and reductions (Interfaces/SourceFile.cs). The value comes from [CallerFilePath], so it's right by
// construction for a class that declares it — but a subclass that inherits the property
// reports its base class's file, and these tests catch that.
public class SourceFile_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public SourceFile_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    // Restricted to the API assembly: test-only stand-ins (e.g. ReductionType_Tests.FakeReduction)
    // also land in ProblemProvider's maps.
    private static IEnumerable<(string kind, Type type)> AllTypes() =>
        ProblemProvider.Problems.Values.Select(t => ("Problem", t))
            .Concat(ProblemProvider.Verifiers.Values.Select(t => ("Verifier", t)))
            .Concat(ProblemProvider.Visualizers.Values.Select(t => ("Visualization", t)))
            .Concat(ProblemProvider.Solvers.Values.Select(t => ("Solver", t)))
            .Concat(ProblemProvider.Reductions.Values.Select(t => ("Reduction", t)))
            .Where(x => x.Item2.Assembly == typeof(IProblem).Assembly);

    private static string? SourceFileOf(object instance) => instance switch {
        IProblem p => p.sourceFile,
        IVerifier v => v.sourceFile,
        IVisualization v => v.sourceFile,
        ISolver s => s.sourceFile,
        IReduction r => r.sourceFile,
        _ => null,
    };

    [Fact]
    public void EverySourceFile_IsRepoRelativeAndDeclaresTheClass() {
        var failures = new List<string>();
        int checkedCount = 0;

        foreach (var (kind, type) in AllTypes()) {
            object? instance;
            try {
                instance = Activator.CreateInstance(type);
            } catch (Exception) {
                // Some classes can't be default-constructed (e.g. SipserReduceToSAT3, whose
                // default ctor reduces a non-Sipser-shaped CLIQUE). Without an instance we
                // can't read the value, but we can still rule out the inherited-property case.
                if (type.GetProperty("sourceFile", System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.DeclaredOnly) is null)
                    failures.Add($"{kind} {type.Name}: does not declare its own sourceFile");
                continue;
            }

            string? path = instance is null ? null : SourceFileOf(instance);
            if (string.IsNullOrEmpty(path)) {
                failures.Add($"{kind} {type.Name}: sourceFile is empty");
                continue;
            }
            if (Path.IsPathRooted(path) || path.Contains('\\')) {
                failures.Add($"{kind} {type.Name}: sourceFile '{path}' is not a repo-relative '/'-separated path");
                continue;
            }

            string fullPath = Path.Combine(ProjectSourcePath.Value, path);
            if (!File.Exists(fullPath)) {
                failures.Add($"{kind} {type.Name}: sourceFile '{path}' does not exist");
                continue;
            }
            // Generic types are named e.g. "Foo`1" by reflection; the declaration is "class Foo<".
            string className = type.Name.Split('`')[0];
            if (!System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(fullPath), $@"\bclass\s+{className}\b")) {
                failures.Add($"{kind} {type.Name}: sourceFile '{path}' does not declare class {className} (inherited sourceFile?)");
                continue;
            }
            checkedCount++;
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
        Assert.True(checkedCount > 0, "No problem/verifier/visualization/solver/reduction types were checked.");
    }

    [Fact]
    public async Task Info_IncludesSourceFile() {
        var response = await _client.GetAsync(
            "/ProblemProvider/info?interface=CliqueBruteForce",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);

        Assert.True(doc.RootElement.TryGetProperty("sourceFile", out var prop),
            $"Expected a sourceFile property in the /ProblemProvider/info response. Body:\n{body}");
        Assert.Equal("Problems/NPComplete/NPC_CLIQUE/Solvers/CliqueBruteForce.cs", prop.GetString());
    }
}
