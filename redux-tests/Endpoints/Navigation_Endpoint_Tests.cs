using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class Navigation_Endpoint_Tests : IClassFixture<AppFactory> {
        private readonly HttpClient _client;

        public Navigation_Endpoint_Tests(AppFactory factory) {
                _client = factory.CreateClient();
        }

        // ── ALL_ProblemsRefactor ──────────────────────────────────────────────────

        [Fact]
        public async Task AllProblems_Returns200() {
                var response = await _client.GetAsync("/Navigation/ALL_ProblemsRefactor", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task AllProblems_ReturnsNonEmptyJsonArray() {
                var response = await _client.GetAsync("/Navigation/ALL_ProblemsRefactor", TestContext.Current.CancellationToken);
                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var arr = JsonSerializer.Deserialize<JsonElement[]>(body);
                Assert.NotNull(arr);
                Assert.NotEmpty(arr);
        }

        // ── NPC_ProblemsRefactor ──────────────────────────────────────────────────

        [Fact]
        public async Task NpcProblems_Returns200() {
                var response = await _client.GetAsync("/Navigation/NPC_ProblemsRefactor", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task NpcProblems_ReturnsNonEmptyJsonArray() {
                var response = await _client.GetAsync("/Navigation/NPC_ProblemsRefactor", TestContext.Current.CancellationToken);
                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var arr = JsonSerializer.Deserialize<JsonElement[]>(body);
                Assert.NotNull(arr);
                Assert.NotEmpty(arr);
        }

        // ── {NPC,P,NPHard}_ProblemsRefactor membership (plan: Redux Tag System, Phase 4.4,
        // Risk 4) ───────────────────────────────────────────────────────────────────────
        //
        // These three endpoints used to derive complexity class from the C# namespace
        // (API.Problems.<Folder>.<ProblemFolder>), so membership equaled the on-disk
        // Problems/<Folder>/ layout. Phase 4 flips that to the DECLARED complexityClass
        // (Nav_Problems.cs, ComplexityClassCatalog), and Problems/NPComplete/ misfiled at
        // least a dozen entries — so membership genuinely changes:
        //   - NPC_ProblemsRefactor: 42 (namespace-derived) -> 4 (declared NPComplete,
        //     Phase 4) -> 26 (Phase 4.3/Step 1 declares the 22 textbook Karp/BINPACKING/
        //     SUDOKU NP-complete problems). 4 problems remain on the Unclassified
        //     allowlist (CUT, WEIGHTEDCUT, NQUEENS, LOSSLESSDATACOMPRESSION — each needs
        //     someone to read what the code models before it can be classified; see
        //     ComplexityClass_Tests.UnclassifiedAllowlist) and so appear on no per-class
        //     endpoint, only on ALL_ProblemsRefactor.
        //   - P_ProblemsRefactor: 6 -> 12 (gains the 6 misfiled-as-NPComplete P problems).
        //   - NPHard_ProblemsRefactor: 2 -> 2 (already filed correctly; unchanged).
        // This was verified safe pre-merge: the GUI only calls ALL_ProblemsRefactor
        // (redux/index.js:328) and never these three per-class endpoints. These exact-set
        // assertions exist so that fact can't silently stop being true.

        private static async Task<HashSet<string>> GetStringSet(HttpClient client, string path) {
                var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var arr = JsonSerializer.Deserialize<string[]>(body);
                Assert.NotNull(arr);
                return new HashSet<string>(arr!, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task NpcProblems_MembershipIsExactlyDeclaredNPComplete() {
                var actual = await GetStringSet(_client, "/Navigation/NPC_ProblemsRefactor");
                var expected = new HashSet<string>(
                    new[]
                    {
                "ARCSET", "BINPACKING", "CLIQUE", "CLIQUECOVER", "CUT", "DIRECTEDHAMILTONIAN",
                "DM3", "DOMINATINGSET", "EXACTCOVER", "GRAPHCOLORING", "HAMILTONIAN",
                "HITTINGSET", "INDEPENDENTSET", "INTPROGRAMMING01", "JOBSEQ", "KNAPSACK",
                "NODESET", "PARTITION", "SAT", "SAT3", "SETCOVER", "STEINERTREE",
                "SUBSETSUM", "SUDOKU", "TSP", "VERTEXCOVER", "WEIGHTEDCUT",
                    },
                    StringComparer.OrdinalIgnoreCase);

                Assert.True(expected.SetEquals(actual),
                    $"NPC_ProblemsRefactor membership changed.\nExpected: {string.Join(", ", expected.OrderBy(s => s))}\n" +
                    $"Actual:   {string.Join(", ", actual.OrderBy(s => s))}");

                // The six problems Problems/NPComplete/ misfiles as P must NOT be here.
                foreach (var shouldBeGone in new[] { "MINIMUMSPANNINGTREE", "SHORTESTPATH", "TOPOLOGICALSORT", "STRONGLYCONNECTEDCOMPONENTS", "EDITDISTANCE", "CONVEXHULL" })
                        Assert.DoesNotContain(shouldBeGone, actual);

                // PRIMEFACTOR (NPIntermediate) and the quantum-oracle problems must NOT be here.
                foreach (var shouldBeGone in new[] { "PRIMEFACTOR", "DEUTSCH", "DEUTSCHJOZSA", "BERNSTEINVAZIRANI", "SIMON", "UNSTRUCTUREDSEARCH" })
                        Assert.DoesNotContain(shouldBeGone, actual);

                // NQUEENS and LOSSLESSDATACOMPRESSION resolved to P, not NPComplete -- must NOT be here.
                foreach (var shouldBeGone in new[] { "NQUEENS", "LOSSLESSDATACOMPRESSION" })
                        Assert.DoesNotContain(shouldBeGone, actual);
        }

        [Fact]
        public async Task PProblems_MembershipIsExactlyDeclaredP() {
                var actual = await GetStringSet(_client, "/Navigation/P_ProblemsRefactor");
                var expected = new HashSet<string>(
                    new[] { "CONVEXHULL", "DFA", "EDITDISTANCE", "LOSSLESSDATACOMPRESSION", "MINCUT", "MINIMUMSPANNINGTREE", "MINSTCUT", "NFA", "NQUEENS", "SPSP", "SSSP", "STRONGLYCONNECTEDCOMPONENTS", "TOPOLOGICALSORT" },
                    StringComparer.OrdinalIgnoreCase);

                Assert.True(expected.SetEquals(actual),
                    $"P_ProblemsRefactor membership changed.\nExpected: {string.Join(", ", expected.OrderBy(s => s))}\n" +
                    $"Actual:   {string.Join(", ", actual.OrderBy(s => s))}");
        }

        [Fact]
        public async Task NpHardProblems_MembershipIsExactlyDeclaredNPHard() {
                var actual = await GetStringSet(_client, "/Navigation/NPHard_ProblemsRefactor");
                var expected = new HashSet<string>(
                    new[] { "MAXCUT", "PUMPSCHEDULINGCM", "PUMPSCHEDULINGEM" },
                    StringComparer.OrdinalIgnoreCase);

                Assert.True(expected.SetEquals(actual),
                    $"NPHard_ProblemsRefactor membership changed.\nExpected: {string.Join(", ", expected.OrderBy(s => s))}\n" +
                    $"Actual:   {string.Join(", ", actual.OrderBy(s => s))}");
        }

        // ── Problem_VerifiersRefactor: lookup must not depend on problemType ──────
        // Regression for #317/#318: the GUI pins problemType to "NPC" and never updates
        // it, so a P / NP-Hard problem arrives with the wrong prefix. The verifier lookup
        // must still find the verifier by problem name regardless of the prefix sent.

        [Fact]
        public async Task ProblemVerifiersRefactor_PProblem_WithWrongNpcProblemType_FindsVerifier() {
                // DFA lives under Problems/P, but mirror the GUI sending problemType=NPC.
                var response = await _client.GetAsync("/Navigation/Problem_VerifiersRefactor?chosenProblem=DFA&problemType=NPC", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var arr = JsonSerializer.Deserialize<string[]>(body);
                Assert.NotNull(arr);
                Assert.Contains("DFAVerifier", arr);
        }

        [Fact]
        public async Task ProblemVerifiersRefactor_PProblem_WithCorrectProblemType_FindsVerifier() {
                var response = await _client.GetAsync("/Navigation/Problem_VerifiersRefactor?chosenProblem=DFA&problemType=P", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var arr = JsonSerializer.Deserialize<string[]>(body);
                Assert.NotNull(arr);
                Assert.Contains("DFAVerifier", arr);
        }

        [Fact]
        public async Task ProblemVerifiersRefactor_UnknownProblem_ReturnsNotFoundString() {
                var response = await _client.GetAsync("/Navigation/Problem_VerifiersRefactor?chosenProblem=NoSuchProblem&problemType=NPC", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                // Ignoring problemType must not turn a genuinely missing problem into a match.
                var message = JsonSerializer.Deserialize<string>(body);
                Assert.Equal("entered a verifier that does not exist", message);
        }

        [Fact]
        public async Task ProblemVerifiersRefactor_OmittedProblemType_FindsVerifier() {
                // problemType is optional (#330); omitting it entirely must still resolve by name.
                var response = await _client.GetAsync("/Navigation/Problem_VerifiersRefactor?chosenProblem=DFA", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var arr = JsonSerializer.Deserialize<string[]>(body);
                Assert.NotNull(arr);
                Assert.Contains("DFAVerifier", arr);
        }

        [Fact]
        public async Task ProblemVerifiersRefactor_CaseInsensitiveInputs_ReturnsNonEmptyJsonArray() {
                var response = await _client.GetAsync("/Navigation/Problem_VerifiersRefactor?chosenProblem=sat3&problemType=npc", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var arr = JsonSerializer.Deserialize<JsonElement[]>(body);
                Assert.NotNull(arr);
                Assert.NotEmpty(arr);
        }

        // ── Problem_SolversRefactor: reflection-based, lookup ignores problemType ──
        // Mirrors the verifier navigation: the GUI pins problemType to "NPC", so the
        // solver lookup must find solvers by problem name regardless of the prefix sent.

        [Fact]
        public async Task ProblemSolversRefactor_NpcProblem_FindsSolver() {
                var response = await _client.GetAsync("/Navigation/Problem_SolversRefactor?chosenProblem=SAT3&problemType=NPC", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var arr = JsonSerializer.Deserialize<string[]>(body);
                Assert.NotNull(arr);
                Assert.NotEmpty(arr);
        }

        [Fact]
        public async Task ProblemSolversRefactor_SAT_ListsGroverSolver() {
                // SATGroverSolver declares ISolver<SAT>, so it resolves to SAT like any
                // other solver — guards against it regressing out of the listing.
                var response = await _client.GetAsync("/Navigation/Problem_SolversRefactor?chosenProblem=SAT&problemType=NPC", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var arr = JsonSerializer.Deserialize<string[]>(body);
                Assert.NotNull(arr);
                Assert.Contains("SATGroverSolver", arr);
        }

        [Fact]
        public async Task ProblemSolversRefactor_PProblem_WithWrongNpcProblemType_FindsSolver() {
                // CLIQUE solver lookup must succeed even when the GUI sends a mismatched prefix.
                var response = await _client.GetAsync("/Navigation/Problem_SolversRefactor?chosenProblem=Clique&problemType=P", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var arr = JsonSerializer.Deserialize<string[]>(body);
                Assert.NotNull(arr);
                Assert.Contains("CliqueBruteForce", arr);
        }

        [Fact]
        public async Task ProblemSolversRefactor_OmittedProblemType_FindsSolver() {
                // problemType is optional; omitting it entirely must still resolve by name.
                var response = await _client.GetAsync("/Navigation/Problem_SolversRefactor?chosenProblem=SAT3", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var arr = JsonSerializer.Deserialize<string[]>(body);
                Assert.NotNull(arr);
                Assert.NotEmpty(arr);
        }

        [Fact]
        public async Task ProblemSolversRefactor_UnknownProblem_ReturnsNotFoundString() {
                var response = await _client.GetAsync("/Navigation/Problem_SolversRefactor?chosenProblem=NoSuchProblem&problemType=NPC", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var message = JsonSerializer.Deserialize<string>(body);
                Assert.Equal("entered a solver that does not exist", message);
        }

        [Fact]
        public async Task ProblemSolversRefactor_CaseInsensitiveInputs_ReturnsNonEmptyJsonArray() {
                var response = await _client.GetAsync("/Navigation/Problem_SolversRefactor?chosenProblem=sat3&problemType=npc", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var arr = JsonSerializer.Deserialize<JsonElement[]>(body);
                Assert.NotNull(arr);
                Assert.NotEmpty(arr);
        }

        // ── Reductions ────────────────────────────────────────────────────────────

        [Fact]
        public async Task Reductions_Returns200() {
                var response = await _client.GetAsync("/Navigation/Reductions", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Reductions_ReturnsNonEmptyGraph() {
                var response = await _client.GetAsync("/Navigation/Reductions", TestContext.Current.CancellationToken);
                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                // Returns a nested adjacency map, not an array
                var graph = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
                Assert.NotNull(graph);
                Assert.NotEmpty(graph);
        }

        // ── Problem_VisualizationsRefactor: reflection-based, ignores problemType (#331) ──
        // Lookup resolves by problem name; the GUI pins problemType to "NPC", so matching
        // on the prefix would drop P / NP-Hard problems.

        [Fact]
        public async Task ProblemVisualizationsRefactor_NpcProblem_FindsVisualization() {
                var response = await _client.GetAsync("/Navigation/Problem_VisualizationsRefactor?chosenProblem=SAT3&problemType=NPC", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var arr = JsonSerializer.Deserialize<string[]>(body);
                Assert.NotNull(arr);
                Assert.Contains("Sat3DefaultVisualization", arr);
        }

        [Fact]
        public async Task ProblemVisualizationsRefactor_PProblem_WithWrongNpcProblemType_FindsVisualization() {
                // DFA lives under Problems/P, but mirror the GUI sending problemType=NPC.
                var response = await _client.GetAsync("/Navigation/Problem_VisualizationsRefactor?chosenProblem=DFA&problemType=NPC", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var arr = JsonSerializer.Deserialize<string[]>(body);
                Assert.NotNull(arr);
                Assert.Contains("DFAVisualization", arr);
        }

        [Fact]
        public async Task ProblemVisualizationsRefactor_OmittedProblemType_FindsVisualization() {
                // problemType is optional (#331); omitting it entirely must still resolve by name.
                var response = await _client.GetAsync("/Navigation/Problem_VisualizationsRefactor?chosenProblem=SAT3", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var arr = JsonSerializer.Deserialize<string[]>(body);
                Assert.NotNull(arr);
                Assert.Contains("Sat3DefaultVisualization", arr);
        }

        [Fact]
        public async Task ProblemVisualizationsRefactor_UnknownProblem_ReturnsEmptyJsonArray() {
                var response = await _client.GetAsync("/Navigation/Problem_VisualizationsRefactor?chosenProblem=NoSuchProblem&problemType=NPC", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var arr = JsonSerializer.Deserialize<JsonElement[]>(body);
                Assert.NotNull(arr);
                Assert.Empty(arr);
        }
}
