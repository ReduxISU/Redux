using System.Globalization;
using System.Text.RegularExpressions;
using API.Interfaces;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Solvers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class WeightedMaxCliqueSolver_Tests {
    private static ISolver<WEIGHTEDMAXCLIQUE>[] Solvers() => [
        new WeightedMaxCliqueBruteForceSolver(),
        new WeightedMaxCliqueBranchAndBoundSolver(),
        new WeightedMaxCliqueGreedyLocalSearchSolver()
    ];

    private static WEIGHTEDMAXCLIQUE Instance(long[] weights, params (int A, int B)[] edges) {
        string nodes = string.Join(",", Enumerable.Range(0, weights.Length).Select(i => $"v{i}"));
        string pairs = string.Join(",", edges.Select(e => $"{{v{e.A},v{e.B}}}"));
        string assignments = string.Join(",", weights.Select((w, i) => $"(v{i},{w.ToString(CultureInfo.InvariantCulture)})"));
        return new WEIGHTEDMAXCLIQUE($"(({{{nodes}}},{{{pairs}}}),({assignments}))");
    }

    private static (int, int)[] CompleteEdges(int n) => Enumerable.Range(0, n)
        .SelectMany(a => Enumerable.Range(a + 1, n - a - 1).Select(b => (a, b))).ToArray();

    // Independent test assertions, deliberately using model identifiers and edges
    // rather than the solvers' adjacency, feasibility, weight, or formatting helpers.
    private static (string[] Vertices, long Weight) AssertCertificate(WEIGHTEDMAXCLIQUE problem, string certificate) {
        Match match = Regex.Match(certificate, @"^\(\{([^{}]*)\},([0-9]+)\)$", RegexOptions.None, TimeSpan.FromSeconds(1));
        Assert.True(match.Success, certificate);
        string[] selected = match.Groups[1].Value.Length == 0 ? [] : match.Groups[1].Value.Split(',');
        long weight = long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        Assert.Equal(selected.Length, selected.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(selected.Order(StringComparer.Ordinal), selected);
        long recomputed = 0;
        for (int i = 0; i < selected.Length; i++) {
            Assert.Contains(selected[i], problem.nodes);
            recomputed = checked(recomputed + problem.weights[selected[i]]);
            for (int j = 0; j < i; j++) {
                Assert.Contains(problem.edges, e =>
                    (e.Key == selected[i] && e.Value == selected[j]) ||
                    (e.Key == selected[j] && e.Value == selected[i]));
            }
        }
        Assert.Equal(recomputed, weight);
        return (selected, weight);
    }

    public static IEnumerable<object[]> KnownInstances() {
        yield return [WEIGHTEDMAXCLIQUE.DefaultInstance, 12L];
        yield return [Instance([], []).instance, 0L];
        yield return [Instance([0, 0, 0], CompleteEdges(3)).instance, 0L];
        yield return [Instance([0, 8, 0], CompleteEdges(3)).instance, 8L];
        yield return [Instance([4, 3, 5, 7], CompleteEdges(4)).instance, 19L];
        yield return [Instance([4, 3, 5, 7]).instance, 7L];
        yield return [Instance([5, 5, 2, 8], (0, 1), (2, 3)).instance, 10L];
        yield return [Instance([1, 1, 1, 1, 1], (0, 1), (0, 2), (1, 2), (3, 4)).instance, 3L];
        // Greedy starts with the heavy incompatible vertex; the three lighter vertices win.
        yield return [Instance([9, 6, 6, 6], (1, 2), (1, 3), (2, 3)).instance, 18L];
        // Candidate intersections must preserve earlier adjacency, not just the last edge.
        yield return [Instance([7, 6, 5, 4], (0, 1), (1, 2), (2, 3), (0, 3)).instance, 13L];
        yield return [Instance([long.MaxValue - 10, 10], (0, 1)).instance, long.MaxValue];
        yield return [Instance([long.MaxValue - 10, 10]).instance, long.MaxValue - 10];
    }

    [Theory]
    [MemberData(nameof(KnownInstances))]
    public void Solvers_ReturnExpectedFeasibleWeightOnKnownExamples(string input, long optimum) {
        var problem = new WEIGHTEDMAXCLIQUE(input);
        foreach (var solver in Solvers()) {
            var result = AssertCertificate(problem, solver.solve(problem));
            Assert.Equal(optimum, result.Weight);
        }
    }

    [Theory]
    [InlineData(17)]
    [InlineData(860)]
    [InlineData(2026)]
    [InlineData(71237)]
    [InlineData(314159)]
    public void ExactSolvers_AgreeOnFortyRandomGraphsAndHeuristicIsFeasible(int seed) {
        var random = new Random(seed);
        double[] densities = [0, 0.15, 0.5, 0.85, 1];
        for (int trial = 0; trial < 40; trial++) {
            int n = trial % 11;
            long[] weights = Enumerable.Range(0, n).Select(_ => (long)random.Next(0, 16)).ToArray();
            if (trial % 7 == 0) Array.Fill(weights, 1L);
            var edges = CompleteEdges(n).Where(_ => random.NextDouble() < densities[trial % densities.Length]).ToArray();
            var problem = Instance(weights, edges);
            var brute = AssertCertificate(problem, new WeightedMaxCliqueBruteForceSolver().solve(problem));
            var bound = AssertCertificate(problem, new WeightedMaxCliqueBranchAndBoundSolver().solve(problem));
            var heuristic = AssertCertificate(problem, new WeightedMaxCliqueGreedyLocalSearchSolver().solve(problem));
            Assert.True(brute.Weight == bound.Weight, $"seed={seed}, trial={trial}, instance={problem.instance}");
            Assert.True(heuristic.Weight <= brute.Weight, $"seed={seed}, trial={trial}");
        }
    }

    [Fact]
    public void StrongGreedyIncumbent_PrunesCompleteGraphWithoutLosingOptimum() {
        var problem = Instance([8, 2, 7, 0], CompleteEdges(4));
        var solver = new WeightedMaxCliqueBranchAndBoundSolver();
        Assert.Equal(17, AssertCertificate(problem, solver.solve(problem)).Weight);
        Assert.Equal(1, solver.SearchNodes);
        Assert.Equal(1, solver.PrunedBranches);
        // Per-run counters reset, including when the same solver is reused.
        Assert.Equal(17, AssertCertificate(problem, solver.solve(problem)).Weight);
        Assert.Equal(1, solver.SearchNodes);
    }

    [Fact]
    public void BoundSearch_ImprovesSuboptimalSeedAndPrunesOtherBranches() {
        var problem = Instance([9, 6, 6, 6], (1, 2), (1, 3), (2, 3));
        var solver = new WeightedMaxCliqueBranchAndBoundSolver();
        Assert.Equal(18, AssertCertificate(problem, solver.solve(problem)).Weight);
        Assert.True(solver.SearchNodes > 1);
        Assert.True(solver.PrunedBranches > 0);
    }

    [Fact]
    public void Heuristic_MultipleStartsEscapeUnseededGreedyTrap() {
        var problem = Instance([9, 6, 6, 6], (1, 2), (1, 3), (2, 3));
        Assert.Equal(9, problem.Value([0])); // The unseeded greedy incumbent.
        Assert.Equal(18, AssertCertificate(problem, new WeightedMaxCliqueGreedyLocalSearchSolver().solve(problem)).Weight);
    }

    [Fact]
    public void Heuristic_ReturnsCompatibleAdditionsAndKeepsFirstTiedOptimum() {
        var problem = Instance([2, 3, 7, 5], (0, 1), (1, 2), (1, 3), (2, 3));
        var solver = new WeightedMaxCliqueGreedyLocalSearchSolver();
        Assert.Equal(15, AssertCertificate(problem, solver.solve(problem)).Weight);
        Assert.Equal("({v0},3)", solver.solve(Instance([3, 3])));
    }

    [Fact]
    public void LocalSearch_ImprovesBeyondEveryGreedyStartThroughPublicEntryPoint() {
        // Every pure greedy start reaches at most 20. A strict replacement unlocks
        // an addition, yielding {v1,v2,v4,v8} of weight 22. No private hooks needed.
        var problem = Instance([5, 5, 6, 7, 3, 2, 9, 3, 8],
            (0, 1), (0, 3), (0, 4), (0, 5), (1, 2), (1, 3), (1, 4), (1, 8),
            (2, 4), (2, 6), (2, 8), (3, 6), (3, 8), (4, 5), (4, 6), (4, 7), (4, 8), (6, 7));
        var solver = new WeightedMaxCliqueGreedyLocalSearchSolver();
        string certificate = solver.solve(problem);
        Assert.Equal("({v1,v2,v4,v8},22)", certificate);
        Assert.Equal(22, AssertCertificate(problem, certificate).Weight);
        Assert.Equal(22, AssertCertificate(problem, new WeightedMaxCliqueBruteForceSolver().solve(problem)).Weight);
    }

    [Fact]
    public void Heuristic_DoesNotClaimOptimalityWhenBoundedLocalSearchGetsStuck() {
        // Each triangle vertex attracts a heavier private neighbor, preventing
        // all greedy starts and strict one-for-one moves from finding the triangle.
        var problem = Instance([10, 10, 10, 6, 6, 6], (0, 3), (1, 4), (2, 5), (3, 4), (3, 5), (4, 5));
        Assert.Equal(16, AssertCertificate(problem, new WeightedMaxCliqueGreedyLocalSearchSolver().solve(problem)).Weight);
        Assert.Equal(18, AssertCertificate(problem, new WeightedMaxCliqueBruteForceSolver().solve(problem)).Weight);
    }

    [Fact]
    public void ExhaustiveSearch_EnumeratesTwentyVerticesAndRejectsTwentyOne() {
        var solver = new WeightedMaxCliqueBruteForceSolver();
        var twenty = Instance(Enumerable.Repeat(1L, 20).ToArray());
        Assert.Equal(1, AssertCertificate(twenty, solver.solve(twenty)).Weight);
        var twentyOne = Instance(Enumerable.Repeat(1L, 21).ToArray());
        var error = Assert.Throws<ExactSearchLimitExceededException>(() => solver.solve(twentyOne));
        Assert.Equal("Weighted Maximum Clique", error.ProblemName);
        Assert.Equal(21, error.VariableCount);
        Assert.Equal(20, error.MaxVariables);
        // Only exhaustive solving has this cap; parsing and other algorithms do not.
        Assert.Equal(1, AssertCertificate(twentyOne, new WeightedMaxCliqueBranchAndBoundSolver().solve(twentyOne)).Weight);
        Assert.Equal(1, AssertCertificate(twentyOne, new WeightedMaxCliqueGreedyLocalSearchSolver().solve(twentyOne)).Weight);
    }

    [Fact]
    public void Solvers_AreDeterministicAcrossRunsAndInputCollectionOrder() {
        var a = new WEIGHTEDMAXCLIQUE("(({a,b,c,d},{{a,b},{c,d}}),((a,5),(b,5),(c,5),(d,5)))");
        var b = new WEIGHTEDMAXCLIQUE("(({d,b,a,c},{{d,c},{b,a}}),((c,5),(a,5),(d,5),(b,5)))");
        foreach (var solver in Solvers()) {
            string expected = solver.solve(a);
            AssertCertificate(a, expected);
            Assert.Equal(expected, solver.solve(a));
            Assert.Equal(expected, solver.solve(b));
            Assert.Equal(expected, solver.solve(a.instance));
        }
    }

    [Fact]
    public void ZeroWeightGraphs_ReturnEmptyCliqueFromEverySolver() {
        foreach (var solver in Solvers()) {
            Assert.Equal("({},0)", solver.solve(Instance([0, 0, 0], CompleteEdges(3))));
            Assert.Equal("({},0)", solver.solve(Instance([])));
        }
    }

    [Fact]
    public void Heuristic_HandlesMoreVerticesThanItsStartCap() {
        var problem = Instance(Enumerable.Repeat(1L, 40).ToArray(), CompleteEdges(40));
        var result = AssertCertificate(problem, new WeightedMaxCliqueGreedyLocalSearchSolver()
            .solve(problem));
        Assert.Equal(40, result.Weight);
        Assert.Equal(40, result.Vertices.Length);
    }

    [Fact]
    public void ReduxTimer_NeverReturnsAnIncumbentCertificate() {
        var problem = new WEIGHTEDMAXCLIQUE();
        foreach (var solver in Solvers()) {
            solver.TimerExpired();
            Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(problem));
            solver.ResetTimer();
            foreach (bool withSteps in new[] { false, true }) {
                SolveRun run = solver.Run(problem.instance, withSteps, TimeSpan.Zero);
                Assert.True(run.TimedOut);
                Assert.Equal(SolveRun.TimedOutAnswer, run.Answer);
                Assert.Empty(run.Steps);
            }
            solver.ResetTimer();
            AssertCertificate(problem, solver.solve(problem));
            SolveRun completed = solver.Run(problem.instance, false);
            Assert.False(completed.TimedOut);
            AssertCertificate(problem, completed.Answer);
        }
    }

    [Fact]
    public void ReduxTimer_InterruptsExhaustiveSearchInProgress() {
        ISolver solver = new WeightedMaxCliqueBruteForceSolver();
        var problem = Instance(Enumerable.Repeat(1L, 20).ToArray(), CompleteEdges(20));
        SolveRun run = solver.Run(problem.instance, false, TimeSpan.FromMilliseconds(1));
        Assert.True(run.TimedOut);
        Assert.Equal(SolveRun.TimedOutAnswer, run.Answer);
    }

    [Fact]
    public void SolverMetadata_IsDeclaredAndComponentsImplementReduxInterfaces() {
        foreach (var solver in Solvers()) {
            Assert.StartsWith("Weighted Maximum Clique ", solver.solverName);
            Assert.NotEmpty(solver.source);
            Assert.NotEmpty(solver.sourceLink);
            Assert.EndsWith(solver.GetType().Name + ".cs", solver.sourceFile);
            Assert.NotNull(solver.spaceComplexity);
            Assert.True(solver.timeComplexity!.Evaluate(new Dictionary<string, double> { ["n"] = 4 }) > 0);
            Assert.Contains(solver.GetType(), ProblemProvider.Solvers.Values);
            Assert.Equal(typeof(WEIGHTEDMAXCLIQUE), ProblemProvider.ProblemTypeOf(solver.GetType(), typeof(ISolver<>)));
        }
        Assert.Equal(SolverType.BruteForce, new WeightedMaxCliqueBruteForceSolver().solverType);
        Assert.Equal(SolverType.Backtracking, new WeightedMaxCliqueBranchAndBoundSolver().solverType);
        Assert.Equal(SolverType.Greedy | SolverType.Heuristic, new WeightedMaxCliqueGreedyLocalSearchSolver().solverType);
        Assert.Equal(SolverComplexityBucket.Polynomial, new WeightedMaxCliqueGreedyLocalSearchSolver().complexityBucket);
        Assert.Equal(SolverComplexityBucket.Exponential, new WeightedMaxCliqueBruteForceSolver().complexityBucket);
        Assert.Equal(SolverComplexityBucket.Exponential, new WeightedMaxCliqueBranchAndBoundSolver().complexityBucket);
    }
}
