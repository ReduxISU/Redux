using System.Net;
using System.Text.Json;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Guards for the SolverType / SolverComplexityBucket / complexity vocabularies (plan: Redux
// Tag System, solver-organization phase). Same "declared, not derived" pattern and same
// ratchet-pair structure as ComplexityClass_Tests.cs — see that file's header for the
// general rationale.
//
// *** These declarations are public correctness claims the API begins serving the moment
// this merges. Which solvers are Unclassified — on any axis — needs advisor sign-off
// before merge, same as prior classification passes. ***
public class SolverType_Tests : IClassFixture<AppFactory> {
        private readonly HttpClient _client;

        // ── Risk 1: enums must serialize as strings, not integers ─────────────────
        //
        // Same regression class ReductionCost_Tests.cs / VisualizationType_Tests.cs guard
        // against: Newtonsoft serializes enums as integers by default. The
        // [JsonConverter]/[StringEnumConverter] attributes pinned on SolverType and
        // SolverComplexityBucket (Interfaces/SolverType.cs, Interfaces/SolverComplexityBucket.cs)
        // are the actual fix; these are the regression guard.

        [Fact]
        public async Task Info_SerializesSolverTypeAndComplexityBucketAsString() {
                var response = await _client.GetAsync(
                    "/ProblemProvider/info?interface=KnapsackDP",
                    TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                using var doc = JsonDocument.Parse(body);

                Assert.True(doc.RootElement.TryGetProperty("solverType", out var typeProp),
                    $"Expected a solverType property in the /ProblemProvider/info response. Body:\n{body}");
                Assert.Equal(JsonValueKind.String, typeProp.ValueKind);
                Assert.Equal(nameof(SolverType.DynamicProgramming), typeProp.GetString());

                Assert.True(doc.RootElement.TryGetProperty("complexityBucket", out var bucketProp),
                    $"Expected a complexityBucket property in the /ProblemProvider/info response. Body:\n{body}");
                Assert.Equal(JsonValueKind.String, bucketProp.ValueKind);
                Assert.Equal(nameof(SolverComplexityBucket.Polynomial), bucketProp.GetString());
        }

        [Fact]
        public async Task AllInfo_SerializesEverySolverTypeAndComplexityBucketAsString() {
                var response = await _client.GetAsync("/Navigation/Batch/allInfo", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
                Assert.NotNull(map);

                int checkedSolverType = 0;
                int checkedComplexityBucket = 0;
                foreach (var (className, element) in map!) {
                        if (element.ValueKind != JsonValueKind.Object) continue;

                        if (element.TryGetProperty("solverType", out var typeProp)) {
                                checkedSolverType++;
                                Assert.True(typeProp.ValueKind == JsonValueKind.String,
                                    $"{className}.solverType serialized as {typeProp.ValueKind}, expected String. This is " +
                                    "the Risk-1 regression: Newtonsoft defaults to serializing enums as integers unless " +
                                    "StringEnumConverter is pinned on SolverType.");
                        }

                        if (element.TryGetProperty("complexityBucket", out var bucketProp)) {
                                checkedComplexityBucket++;
                                Assert.True(bucketProp.ValueKind == JsonValueKind.String,
                                    $"{className}.complexityBucket serialized as {bucketProp.ValueKind}, expected String. " +
                                    "This is the Risk-1 regression: Newtonsoft defaults to serializing enums as integers " +
                                    "unless StringEnumConverter is pinned on SolverComplexityBucket.");
                        }
                }

                Assert.True(checkedSolverType > 0, "Found no solverType properties in /Navigation/Batch/allInfo.");
                Assert.True(checkedComplexityBucket > 0, "Found no complexityBucket properties in /Navigation/Batch/allInfo.");
        }

        // ── Ratchet pair #1: solverType != Unclassified ─────────────────────────────
        //
        // NoNewUndeclaredSolverType:            actualUndeclared ⊆ Allowlist
        // AllowlistHasNoStaleSolverTypeEntries:  Allowlist ⊆ actualUndeclared
        //
        // The "classic poly-time exact graph algorithm" cluster that used to live here (Dijkstra,
        // Prim, Kruskal, Kahn's, Kosaraju, Stoer-Wagner, ConvexHull, DFA, SPSP, SSSP, MinSTCut)
        // has since been resolved: the SolverType vocabulary was extended with StateTransition/
        // DivideAndConquer/BreadthFirstSearch/DepthFirstSearch, and each of those solvers now
        // carries a real, well-fitting value (see each file's own "Declared, not derived" comment).
        // What remains below genuinely has no fitting SolverType value.
        private static readonly string[] SolverTypeUnclassifiedAllowlist =
        {
        // Classical baselines paired with a quantum solver for the same problem (Deutsch,
        // Deutsch-Jozsa, Bernstein-Vazirani, Simon). SolverType has no "classical control
        // group" value; complexityBucket (Polynomial or Exponential, matching each solver's
        // real classical query cost) carries the actual growth class instead.
        "BernsteinVaziraniClassicalSolver",
        "DeutschJozsaClassicalSolver",
        "DeutschClassicalSolver",
        "SimonSolver",

        // Placeholder/test-scaffolding class (Interfaces/DummyClasses/DummySolver.cs), not one
        // of the 65 real solvers in scope for this classification pass — same precedent as
        // "DummyVisualization" in VisualizationType_Tests.cs's allowlist.
        "DummySolver",
    };

        private static HashSet<string> ActualSolverTypeUndeclared() =>
            SolverTypeCatalog.SolverTypeByClassName.Value
                .Where(kv => kv.Value == nameof(SolverType.Unclassified))
                .Select(kv => kv.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        [Fact]
        public void NoNewUndeclaredSolverType() {
                var actual = ActualSolverTypeUndeclared();
                var allowlist = new HashSet<string>(SolverTypeUnclassifiedAllowlist, StringComparer.OrdinalIgnoreCase);
                var unexpected = actual.Where(c => !allowlist.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();

                Assert.True(unexpected.Count == 0,
                    $"Solver(s) declared SolverType.Unclassified without being added to the allowlist: " +
                    $"{string.Join(", ", unexpected)}. Either declare a real SolverType (needs advisor " +
                    "sign-off — see the header of this file), or add the class to " +
                    "SolverTypeUnclassifiedAllowlist in SolverType_Tests.cs.");
        }

        [Fact]
        public void AllowlistHasNoStaleSolverTypeEntries() {
                var actual = ActualSolverTypeUndeclared();
                var stale = SolverTypeUnclassifiedAllowlist.Where(c => !actual.Contains(c)).ToList();

                Assert.True(stale.Count == 0,
                    $"Allowlist entry no longer SolverType.Unclassified (already classified, or " +
                    $"renamed/removed/failed to instantiate) — delete from SolverTypeUnclassifiedAllowlist " +
                    $"in SolverType_Tests.cs: {string.Join(", ", stale)}.");
        }

        // ── Ratchet pair #2: complexityBucket != Unclassified ───────────────────────
        //
        // NoNewUndeclaredComplexityBucket:            actualUndeclared ⊆ Allowlist
        // AllowlistHasNoStaleComplexityBucketEntries:  Allowlist ⊆ actualUndeclared
        private static readonly string[] ComplexityBucketUnclassifiedAllowlist =
        {
        // Thin HTTP clients to an external quantum-simulator service — no local classical
        // loop, so there is no classical growth story to bucket. For ShorsQuantumSolver
        // specifically this is not just an unfilled gap: tagging it with any classical
        // bucket would be a category error, since Shor's algorithm's whole point is a
        // quantum-vs-classical complexity separation.
        "ShorsQuantumSolver",
        "SATGroverSolver",
        "UnstructuredGroverSolver",
        "DeutschQuantumSolver",
        "DeutschJozsaQuantumSolver",
        "BernsteinVaziraniQuantumSolver",

        // Placeholder/test-scaffolding class (Interfaces/DummyClasses/DummySolver.cs), not one
        // of the 65 real solvers in scope for this classification pass — same precedent as
        // "DummyVisualization" in VisualizationType_Tests.cs's allowlist.
        "DummySolver",
    };

        private static HashSet<string> ActualComplexityBucketUndeclared() =>
            SolverTypeCatalog.ComplexityBucketByClassName.Value
                .Where(kv => kv.Value == nameof(SolverComplexityBucket.Unclassified))
                .Select(kv => kv.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        [Fact]
        public void NoNewUndeclaredComplexityBucket() {
                var actual = ActualComplexityBucketUndeclared();
                var allowlist = new HashSet<string>(ComplexityBucketUnclassifiedAllowlist, StringComparer.OrdinalIgnoreCase);
                var unexpected = actual.Where(c => !allowlist.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();

                Assert.True(unexpected.Count == 0,
                    $"Solver(s) declared SolverComplexityBucket.Unclassified without being added to the " +
                    $"allowlist: {string.Join(", ", unexpected)}. Either declare a real " +
                    "SolverComplexityBucket (needs advisor sign-off — see the header of this file), or add " +
                    "the class to ComplexityBucketUnclassifiedAllowlist in SolverType_Tests.cs.");
        }

        [Fact]
        public void AllowlistHasNoStaleComplexityBucketEntries() {
                var actual = ActualComplexityBucketUndeclared();
                var stale = ComplexityBucketUnclassifiedAllowlist.Where(c => !actual.Contains(c)).ToList();

                Assert.True(stale.Count == 0,
                    $"Allowlist entry no longer SolverComplexityBucket.Unclassified (already classified, or " +
                    $"renamed/removed/failed to instantiate) — delete from " +
                    $"ComplexityBucketUnclassifiedAllowlist in SolverType_Tests.cs: {string.Join(", ", stale)}.");
        }

        // ── Ratchet pair #3: complexity != "" ────────────────────────────────────────
        //
        // NoNewUndeclaredComplexity:            actualUndeclared ⊆ Allowlist
        // AllowlistHasNoStaleComplexityEntries:  Allowlist ⊆ actualUndeclared
        //
        // complexity is a free-text Big-O string, not an enum -- "undeclared" here means
        // still defaulting to "" via the ISolver interface default. Every real solver was
        // read directly and given a confidently-known Big-O string as part of the solver
        // Big-O backfill pass (see each solver's own "Declared, not derived" comment) —
        // this allowlist should stay empty except for non-classification placeholder classes.
        private static readonly string[] ComplexityUnclassifiedAllowlist =
        {
        // Placeholder/test-scaffolding class (Interfaces/DummyClasses/DummySolver.cs), not one
        // of the 65 real solvers in scope for this classification pass — same precedent as
        // "DummyVisualization" in VisualizationType_Tests.cs's allowlist.
        "DummySolver",
    };

        private static HashSet<string> ActualComplexityUndeclared() =>
            SolverTypeCatalog.ComplexityByClassName.Value
                .Where(kv => string.IsNullOrEmpty(kv.Value))
                .Select(kv => kv.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        [Fact]
        public void NoNewUndeclaredComplexity() {
                var actual = ActualComplexityUndeclared();
                var allowlist = new HashSet<string>(ComplexityUnclassifiedAllowlist, StringComparer.OrdinalIgnoreCase);
                var unexpected = actual.Where(c => !allowlist.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();

                Assert.True(unexpected.Count == 0,
                    $"Solver(s) have an empty complexity string without being added to the allowlist: " +
                    $"{string.Join(", ", unexpected)}. Either declare a confidently-known Big-O string by reading " +
                    "solve() (needs advisor sign-off — see the header of this file), or add the class to " +
                    "ComplexityUnclassifiedAllowlist in SolverType_Tests.cs.");
        }

        [Fact]
        public void AllowlistHasNoStaleComplexityEntries() {
                var actual = ActualComplexityUndeclared();
                var stale = ComplexityUnclassifiedAllowlist.Where(c => !actual.Contains(c)).ToList();

                Assert.True(stale.Count == 0,
                    $"Allowlist entry no longer has an empty complexity string (already classified, or " +
                    $"renamed/removed/failed to instantiate) — delete from ComplexityUnclassifiedAllowlist " +
                    $"in SolverType_Tests.cs: {string.Join(", ", stale)}.");
        }

        // ── Characterization report ─────────────────────────────────────────────────
        //
        // Always-passing gap-analysis dump, not an enforced check — "characterize rather than
        // count". Visible via `dotnet test -v n` and in CI logs. Deliberately NOT written to a
        // committed file: a committed report rots the moment anyone changes a tag without
        // regenerating it; this regenerates itself on every test run.

        private readonly Xunit.ITestOutputHelper _output;

        public SolverType_Tests(AppFactory factory, Xunit.ITestOutputHelper output) {
                _client = factory.CreateClient();
                _output = output;
        }

        [Fact]
        public void CharacterizationReport_DumpsPerSolverMetadata() {
                var solverTypeByClassName = SolverTypeCatalog.SolverTypeByClassName.Value;
                var complexityBucketByClassName = SolverTypeCatalog.ComplexityBucketByClassName.Value;
                var complexityByClassName = SolverTypeCatalog.ComplexityByClassName.Value;

                var allClassNames = solverTypeByClassName.Keys
                    .Union(complexityBucketByClassName.Keys, StringComparer.OrdinalIgnoreCase)
                    .Union(complexityByClassName.Keys, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToList();

                _output.WriteLine("== Redux Tag System — solver classification characterization report ==");
                _output.WriteLine($"Solvers reflected: {allClassNames.Count}");
                _output.WriteLine("");

                foreach (var className in allClassNames) {
                        string solverType = solverTypeByClassName.TryGetValue(className, out var st) ? st : "<not instantiated>";
                        string complexityBucket = complexityBucketByClassName.TryGetValue(className, out var cb) ? cb : "<not instantiated>";
                        string complexity = complexityByClassName.TryGetValue(className, out var c)
                            ? (string.IsNullOrEmpty(c) ? "<empty>" : c)
                            : "<not instantiated>";

                        _output.WriteLine($"--- {className} ---");
                        _output.WriteLine($"  solverType:        {solverType}");
                        _output.WriteLine($"  complexity:        {complexity}");
                        _output.WriteLine($"  complexityBucket:  {complexityBucket}");
                }

                // Always passes — this Fact exists to produce the dump above, not to assert.
                Assert.True(true);
        }
}
