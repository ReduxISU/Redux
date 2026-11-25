using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using API.Interfaces;
using API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK;
using API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK.Solvers;
using API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class MaximumValueKnapsackBranchAndBound_Tests {
    [Theory]
    [InlineData(MAXIMUMVALUEKNAPSACK.DefaultInstance, 220)]
    [InlineData("((),0)", 0)]
    [InlineData("((),9223372036854775807)", 0)]
    [InlineData("(((1,9)),0)", 0)]
    [InlineData("(((0,7),(0,0),(1,5)),0)", 7)]
    [InlineData("(((1,0),(0,0)),1)", 0)]
    [InlineData("(((2,3),(2,3)),4)", 6)]
    [InlineData("(((2,3),(2,3)),2)", 3)]
    [InlineData("(((20,100),(10,60),(10,55)),20)", 115)]
    [InlineData("(((2,3),(3,4),(3,4)),6)", 8)]
    [InlineData("(((3,4),(4,5),(4,5)),8)", 10)]
    [InlineData("(((1,2),(10,10)),10)", 10)]
    [InlineData("(((0,9223372036854775806),(0,1)),0)", long.MaxValue)]
    [InlineData("(((9223372036854775807,1),(9223372036854775807,2)),9223372036854775807)", 2)]
    [InlineData("(((9223372036854775807,9223372036854775807)),9223372036854775807)", long.MaxValue)]
    [InlineData("(((1,2),(9223372036854775806,9223372036854775805)),9223372036854775806)", 9223372036854775805)]
    [InlineData("(((9223372036854775806,4611686018427387903),(9223372036854775807,4611686018427387904)),9223372036854775807)", 4611686018427387904)]
    public void KnownInstances_AgreeWithBruteForceAndVerify(string input, long optimum) {
        var problem = new MAXIMUMVALUEKNAPSACK(input);
        var solver = new MaximumValueKnapsackBranchAndBoundSolver();
        string certificate = solver.solve(problem);
        Assert.True(new MaximumValueKnapsackSolutionVerifier().verify(problem, certificate));
        Assert.True(new MaximumValueKnapsackOptimalityVerifier().verify(problem, certificate));
        Assert.Equal(optimum, MaximumValueKnapsackSolutionVerifier.ParseCertificate(problem, certificate).Value);
        string brute = new MaximumValueKnapsackBruteForceSolver().solve(problem);
        Assert.Equal(optimum, MaximumValueKnapsackSolutionVerifier.ParseCertificate(problem, brute).Value);
        Assert.Equal(certificate, solver.solve(problem));
        Assert.Equal(certificate, ((ISolver)solver).solve(input));
    }

    [Fact]
    public void FractionalBound_RoundsDownToPruneAnIntegerIncumbent() {
        // Greedy takes one item for 1; the fractional bound is 4/3. Its floor
        // proves the incumbent at the root, while ceiling would require search.
        var problem = new MAXIMUMVALUEKNAPSACK("(((3,1),(3,1)),4)");
        var solver = new MaximumValueKnapsackBranchAndBoundSolver();
        Assert.Equal("({1},1)", solver.solve(problem));
        Assert.Equal(1, solver.SearchNodes);
        Assert.Equal(1, solver.PrunedBranches);
    }

    [Fact]
    public void FractionalBound_ImprovesMisleadingDensitySeedAndPrunes() {
        var problem = new MAXIMUMVALUEKNAPSACK("(((2,3),(3,4),(3,4)),6)");
        // Density greedy takes indices 1,2 for 7. At the root the fractional
        // relaxation is 7 + 4/3; the legal optimum is indices 2,3 for 8.
        var solver = new MaximumValueKnapsackBranchAndBoundSolver();
        Assert.Equal("({2,3},8)", solver.solve(problem));
        Assert.True(solver.SearchNodes > 1);
        Assert.True(solver.PrunedBranches > 0);
    }

    [Theory]
    [InlineData(21)]
    [InlineData(80)]
    [InlineData(1000)]
    public void AllItemsFit_RootBoundProvesOptimumWithoutEnumeratingSubsets(int count) {
        var problem = new MAXIMUMVALUEKNAPSACK(MaximumValueKnapsackBruteForce_Tests.UnitItems(count));
        var solver = new MaximumValueKnapsackBranchAndBoundSolver();
        string certificate = solver.solve(problem);
        Assert.True(new MaximumValueKnapsackSolutionVerifier().verify(problem, certificate));
        Assert.Equal(count, MaximumValueKnapsackSolutionVerifier.ParseCertificate(problem, certificate).Value);
        Assert.Equal(1, solver.SearchNodes);
        Assert.Equal(1, solver.PrunedBranches);
        solver.timerHasExpired = true;
        Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(problem));
        Assert.Equal(0, solver.SearchNodes);
        Assert.Equal(0, solver.PrunedBranches);
    }

    [Fact]
    public void MoreThanTwentyItems_SearchImprovesGreedyInsteadOfInheritingBruteLimit() {
        // The 21 zero-value occurrences stay in the model and preserve indices.
        string pairs = string.Join(",", Enumerable.Repeat("(0,0)", 21));
        var problem = new MAXIMUMVALUEKNAPSACK($"(({pairs},(2,3),(3,4),(3,4)),6)");
        var solver = new MaximumValueKnapsackBranchAndBoundSolver();
        Assert.Equal("({23,24},8)", solver.solve(problem));
        Assert.True(solver.SearchNodes > 1);
        Assert.Throws<ExactSearchLimitExceededException>(() => new MaximumValueKnapsackBruteForceSolver().solve(problem));
    }

    [Fact]
    public void ExactDensityCrossProducts_DistinguishNearEqualRatiosAtInt64Boundary() {
        var problem = new MAXIMUMVALUEKNAPSACK("(((9223372036854775806,4611686018427387903),(9223372036854775807,4611686018427387904)),9223372036854775807)");
        var solver = new MaximumValueKnapsackBranchAndBoundSolver();
        Assert.Equal("({2},4611686018427387904)", solver.solve(problem));
        // Item 2 has the higher exact density. Taking it fills capacity and the
        // root bound proves the incumbent; rounded-to-double ties would start at 1.
        Assert.Equal(1, solver.SearchNodes);
    }

    [Fact]
    public void Ties_UseOriginalIndicesAndKeepDeterministicFeasibleIncumbent() {
        var problem = new MAXIMUMVALUEKNAPSACK("(((1,1),(2,2),(1,1),(0,0)),2)");
        var solver = new MaximumValueKnapsackBranchAndBoundSolver();
        for (int run = 0; run < 5; run++) Assert.Equal("({1,3},2)", solver.solve(problem));
        Assert.Equal("({1,2},2)", solver.solve(new MAXIMUMVALUEKNAPSACK("(((1,1),(1,1),(2,2)),2)")));
    }

    [Theory]
    [InlineData(MAXIMUMVALUEKNAPSACK.DefaultInstance)]
    [InlineData("((),0)")]
    [InlineData("(((0,7),(0,0)),0)")]
    public void PreexpiredAndZeroDeadline_ReturnTimeoutWithoutClaimingOptimality(string input) {
        ISolver solver = new MaximumValueKnapsackBranchAndBoundSolver();
        solver.TimerExpired();
        Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(input));
        foreach (bool steps in new[] { false, true }) {
            var run = solver.Run(input, steps, TimeSpan.Zero);
            Assert.True(run.TimedOut);
            Assert.Equal(SolveRun.TimedOutAnswer, run.Answer);
            Assert.Empty(run.Steps);
        }
        solver.ResetTimer();
        Assert.True(((IVerifier)new MaximumValueKnapsackSolutionVerifier()).verify(input, solver.solve(input)));
    }

    internal static string HardInstance(int count = 80) =>
        "((" + string.Join(",", Enumerable.Repeat("(2,2)", count)) + $"),{count - 1})";

    [Fact]
    public void CooperativeTimer_StopsFractionalSearchWithoutReturningIncumbent() {
        ISolver solver = new MaximumValueKnapsackBranchAndBoundSolver();
        // Odd capacity and equal densities leave an integrality gap in the bound:
        // an enormous number of tied subsets cannot be pruned at the root.
        var run = solver.Run(HardInstance(), false, TimeSpan.FromMilliseconds(10));
        Assert.True(run.TimedOut);
        Assert.Equal(SolveRun.TimedOutAnswer, run.Answer);
        Assert.Empty(run.Steps);
    }

    [Fact]
    public void MetadataAndDiscovery_KeepBruteForceDefault() {
        var solver = new MaximumValueKnapsackBranchAndBoundSolver();
        Assert.Equal(typeof(MaximumValueKnapsackBranchAndBoundSolver), ProblemProvider.Solvers["maximumvalueknapsackbranchandboundsolver"]);
        Assert.Equal(typeof(MAXIMUMVALUEKNAPSACK), ProblemProvider.ProblemTypeOf(solver.GetType(), typeof(ISolver<>)));
        IProblem problem = new MAXIMUMVALUEKNAPSACK();
        Assert.IsType<MaximumValueKnapsackBruteForceSolver>(problem.defaultSolver);
        Assert.Equal(SolverType.Backtracking, solver.solverType);
        Assert.Equal(SolverComplexityBucket.Exponential, solver.complexityBucket);
        Assert.Equal(24, solver.timeComplexity.Evaluate(new Dictionary<string, double> { ["i"] = 3 }));
        Assert.Equal(3, solver.spaceComplexity.Evaluate(new Dictionary<string, double> { ["i"] = 3 }));
        Assert.NotEmpty(solver.source);
        Assert.NotEmpty(solver.sourceLink);
        Assert.NotEmpty(solver.contributors);
        Assert.EndsWith("MaximumValueKnapsackBranchAndBoundSolver.cs", solver.sourceFile);
    }

    [Theory]
    [InlineData(874)]
    [InlineData(2026)]
    [InlineData(603)]
    public void FixedSeedRandomInstances_AgreeWithBruteForceAndIndependentOracle(int seed) {
        var random = new Random(seed);
        for (int trial = 0; trial < 40; trial++) {
            int n = trial % 13;
            long[] weights = Enumerable.Range(0, n).Select(_ => random.Next(6) == 0 ? long.MaxValue : random.NextInt64(0, 25)).ToArray();
            long[] values = Enumerable.Range(0, n).Select(_ => trial % 3 == 0
                ? random.NextInt64(0, long.MaxValue / Math.Max(1, n)) : random.NextInt64(0, 25)).ToArray();
            long capacity = trial % 4 == 0 ? long.MaxValue : random.NextInt64(0, 60);
            CompareWithOracle(weights, values, capacity);
        }
    }

    [Theory]
    [InlineData(19, 87419)]
    [InlineData(20, 87420)]
    public void RandomInstancesAtExhaustiveBoundary_AgreeWithBothBaselines(int count, int seed) {
        var random = new Random(seed);
        long[] weights = Enumerable.Range(0, count).Select(_ => random.NextInt64(0, 30)).ToArray();
        long[] values = Enumerable.Range(0, count).Select(_ => random.NextInt64(0, long.MaxValue / count)).ToArray();
        CompareWithOracle(weights, values, 100);
    }

    private static void CompareWithOracle(long[] weights, long[] values, long capacity) {
        // Test-only exhaustive recursion uses raw arrays and BigInteger totals;
        // it shares no parsing, feasibility, bound, or objective code with solvers.
        BigInteger optimum = 0;
        void Visit(int next, BigInteger weight, BigInteger value) {
            if (next == weights.Length) {
                if (weight <= capacity && value > optimum) optimum = value;
                return;
            }
            Visit(next + 1, weight, value);
            Visit(next + 1, weight + weights[next], value + values[next]);
        }
        Visit(0, 0, 0);
        string pairs = string.Join(",", weights.Select((w, i) => $"({w.ToString(CultureInfo.InvariantCulture)},{values[i].ToString(CultureInfo.InvariantCulture)})"));
        var problem = new MAXIMUMVALUEKNAPSACK($"(({pairs}),{capacity.ToString(CultureInfo.InvariantCulture)})");
        foreach (ISolver<MAXIMUMVALUEKNAPSACK> solver in new ISolver<MAXIMUMVALUEKNAPSACK>[] {
            new MaximumValueKnapsackBruteForceSolver(), new MaximumValueKnapsackBranchAndBoundSolver() }) {
            string certificate = solver.solve(problem);
            Match match = Regex.Match(certificate, @"\A\(\{([0-9]+(?:,[0-9]+)*)?\},([0-9]+)\)\z");
            Assert.True(match.Success, certificate);
            int[] selected = match.Groups[1].Length == 0 ? [] : match.Groups[1].Value.Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            Assert.Equal(selected.Order(), selected);
            Assert.Equal(selected.Length, selected.Distinct().Count());
            Assert.All(selected, i => Assert.InRange(i, 1, weights.Length));
            BigInteger actualWeight = selected.Aggregate(BigInteger.Zero, (sum, i) => sum + weights[i - 1]);
            BigInteger actualValue = selected.Aggregate(BigInteger.Zero, (sum, i) => sum + values[i - 1]);
            Assert.True(actualWeight <= capacity);
            Assert.Equal(optimum, actualValue);
            Assert.Equal(actualValue, BigInteger.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
            // Boundary runs are costly; determinism is also checked on all small cases.
            if (weights.Length < 19) Assert.Equal(certificate, solver.solve(problem));
        }
    }
}
