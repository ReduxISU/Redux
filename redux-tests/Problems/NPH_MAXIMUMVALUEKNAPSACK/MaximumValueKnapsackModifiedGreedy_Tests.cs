using System.Diagnostics;
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

public class MaximumValueKnapsackModifiedGreedy_Tests(ITestOutputHelper output) {
    [Theory]
    [InlineData(MAXIMUMVALUEKNAPSACK.DefaultInstance, "({1,2},160)", 220)]
    [InlineData("((),0)", "({},0)", 0)]
    [InlineData("((),9223372036854775807)", "({},0)", 0)]
    [InlineData("(((1,7)),0)", "({},0)", 0)]
    [InlineData("(((0,7),(0,0),(1,5)),0)", "({1},7)", 7)]
    [InlineData("(((1,0),(0,0)),1)", "({},0)", 0)]
    [InlineData("(((2,3),(2,3)),4)", "({1,2},6)", 6)]
    [InlineData("(((2,3),(2,3)),2)", "({1},3)", 3)]
    [InlineData("(((1,2),(100,100)),100)", "({2},100)", 100)]
    [InlineData("(((3,5),(3,4),(1,1)),5)", "({1},5)", 6)]
    [InlineData("(((10,10),(9,10),(2,3)),10)", "({1},10)", 10)]
    [InlineData("(((0,7),(1,2),(100,100)),100)", "({1,3},107)", 107)]
    [InlineData("(((0,7),(10,60),(20,100),(30,120)),50)", "({1,2,3},167)", 227)]
    [InlineData("(((100,1000),(1,1)),1)", "({2},1)", 1)]
    [InlineData("(((0,9223372036854775806),(0,1)),0)", "({1,2},9223372036854775807)", long.MaxValue)]
    [InlineData("(((9223372036854775807,1),(9223372036854775807,2)),9223372036854775807)", "({2},2)", 2)]
    [InlineData("(((9223372036854775806,4611686018427387903),(9223372036854775807,4611686018427387904)),9223372036854775807)", "({2},4611686018427387904)", 4611686018427387904)]
    public void KnownInstances_ReturnExpectedFeasibleCandidateAndDocumentedQuality(string input, string expected, long optimum) {
        var problem = new MAXIMUMVALUEKNAPSACK(input);
        var solver = new MaximumValueKnapsackModifiedGreedySolver();
        Assert.Equal(expected, solver.solve(problem));
        Assert.Equal(expected, solver.solve(problem));
        Assert.Equal(expected, ((ISolver)solver).solve(input));
        Assert.True(new MaximumValueKnapsackSolutionVerifier().verify(problem, expected));
        long actual = MaximumValueKnapsackSolutionVerifier.ParseCertificate(problem, expected).Value;
        Assert.True(2 * (BigInteger)actual >= optimum);
        Assert.Equal(actual == optimum, new MaximumValueKnapsackOptimalityVerifier().verify(problem, expected));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(1000)]
    [InlineData(1000000)]
    public void AdversarialFamily_ApproachesOneHalfBound(long m) {
        // The first item has higher density but blocks either remaining item.
        // Both candidates have value m+2; the exact optimum is the other two, 2m.
        long[] weights = [m + 1, m, m];
        long[] values = [m + 2, m, m];
        BigInteger optimum = CompareWithOracle(weights, values, 2 * m);
        Assert.Equal(2 * (BigInteger)m, optimum);
        var problem = Instance(weights, values, 2 * m);
        string certificate = new MaximumValueKnapsackModifiedGreedySolver().solve(problem);
        Assert.Equal($"({{1}},{m + 2})", certificate);
        Assert.True(2 * (BigInteger)(m + 2) >= optimum);
        Assert.True((BigInteger)100 * (m + 2) <= 60 * optimum);
    }

    [Fact]
    public void ExactDensityComparison_PreservesPrefixForNearlyEqualRatios() {
        // The densities of items 1 and 2 differ below double precision. The exact
        // higher-density item 2 fills capacity and must beat item 1's singleton.
        long[] weights = [9007199254740993, 9007199254740992, 1];
        long[] values = [9007199254740992, 9007199254740992, 1];
        var problem = Instance(weights, values, 9007199254740993);
        Assert.Equal("({2,3},9007199254740993)", new MaximumValueKnapsackModifiedGreedySolver().solve(problem));
        CompareWithOracle(weights, values, problem.capacity);
    }

    [Theory]
    [InlineData(21)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void LargeEasyInstances_AreFeasibleWithoutExactSearchCap(int count) {
        var problem = new MAXIMUMVALUEKNAPSACK(MaximumValueKnapsackBruteForce_Tests.UnitItems(count));
        var clock = Stopwatch.StartNew();
        string certificate = new MaximumValueKnapsackModifiedGreedySolver().solve(problem);
        clock.Stop();
        Assert.True(new MaximumValueKnapsackSolutionVerifier().verify(problem, certificate));
        Assert.Equal(count, MaximumValueKnapsackSolutionVerifier.ParseCertificate(problem, certificate).Value);
        output.WriteLine($"Modified greedy: {count} all-fit items, {clock.Elapsed.TotalMilliseconds:F3} ms (single run, not a benchmark).");
    }

    [Theory]
    [InlineData(MAXIMUMVALUEKNAPSACK.DefaultInstance)]
    [InlineData("((),0)")]
    [InlineData("(((0,7)),0)")]
    public void PreexpiredAndZeroDeadline_NeverReturnCertificate(string input) {
        ISolver solver = new MaximumValueKnapsackModifiedGreedySolver();
        solver.TimerExpired();
        Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(input));
        foreach (bool withSteps in new[] { false, true }) {
            var run = solver.Run(input, withSteps, TimeSpan.Zero);
            Assert.True(run.TimedOut);
            Assert.Equal(SolveRun.TimedOutAnswer, run.Answer);
            Assert.Empty(run.Steps);
        }
        solver.ResetTimer();
        Assert.True(((IVerifier)new MaximumValueKnapsackSolutionVerifier()).verify(input, solver.solve(input)));
        Assert.False(solver.Run(input, false).TimedOut);
    }

    [Fact]
    public void CooperativeTimer_InterruptsWorkOnAnAlreadyParsedLargeInstance() {
        var problem = new MAXIMUMVALUEKNAPSACK(MaximumValueKnapsackBruteForce_Tests.UnitItems(20000));
        var solver = new MaximumValueKnapsackModifiedGreedySolver();
        using var timer = SolverTimer.Start(solver, TimeSpan.FromMilliseconds(1));
        // The timer's callback runs on the thread pool, so with the suite running in parallel it can arrive seconds late
        // (in CI it once missed 100 back-to-back solves), and this polynomial solver may finish before it. So wait until
        // the timer has really fired, then check that the solver honours it on the already-parsed instance.
        Assert.True(SpinWait.SpinUntil(() => solver.timerHasExpired, TimeSpan.FromSeconds(30)), "the 1 ms solver timer never fired");
        Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(problem));
        Assert.True(solver.timerHasExpired);
    }

    [Fact]
    public void DiscoveryAndMetadata_PreserveExistingDefaults() {
        var solver = new MaximumValueKnapsackModifiedGreedySolver();
        IProblem problem = new MAXIMUMVALUEKNAPSACK();
        Assert.IsType<MaximumValueKnapsackBruteForceSolver>(problem.defaultSolver);
        Assert.IsType<MaximumValueKnapsackSolutionVerifier>(problem.defaultVerifier);
        Assert.Equal(typeof(MaximumValueKnapsackModifiedGreedySolver), ProblemProvider.Solvers["maximumvalueknapsackmodifiedgreedysolver"]);
        Assert.Equal(typeof(MAXIMUMVALUEKNAPSACK), ProblemProvider.ProblemTypeOf(solver.GetType(), typeof(ISolver<>)));
        Assert.Empty(solver.GetType().GetCustomAttributes(typeof(DefaultForAttribute), false));
        Assert.Equal(SolverType.Greedy | SolverType.Approximation, solver.solverType);
        Assert.Equal(SolverComplexityBucket.Polynomial, solver.complexityBucket);
        Assert.Equal(10 * Math.Log(10), solver.timeComplexity.Evaluate(("i", 10)), 8);
        Assert.Equal(10, solver.spaceComplexity.Evaluate(("i", 10)));
        Assert.Contains("ALG >= OPT / 2", solver.solverDefinition);
        Assert.Contains("not necessarily optimal", solver.solverDefinition);
        Assert.NotEmpty(solver.source);
        Assert.NotEmpty(solver.sourceLink);
        Assert.NotEmpty(solver.contributors);
        Assert.EndsWith("MaximumValueKnapsackModifiedGreedySolver.cs", solver.sourceFile);
    }

    [Theory]
    [InlineData(8745)]
    [InlineData(603)]
    [InlineData(2026)]
    public void RandomSmallInstances_SatisfyIndependentHalfApproximationOracle(int seed) {
        var random = new Random(seed);
        for (int trial = 0; trial < 60; trial++) {
            int n = trial % 13;
            long[] weights = Enumerable.Range(0, n).Select(_ => random.Next(6) == 0 ? long.MaxValue : random.NextInt64(0, 20)).ToArray();
            long[] values = Enumerable.Range(0, n).Select(_ => trial % 3 == 0
                ? random.NextInt64(0, long.MaxValue / Math.Max(1, n)) : random.NextInt64(0, 20)).ToArray();
            long capacity = trial % 4 == 0 ? long.MaxValue : random.NextInt64(0, 40);
            CompareWithOracle(weights, values, capacity);
        }
    }

    [Fact]
    public void CompareTwentyItemInstanceWithBothExactSolversAndOracle() {
        var random = new Random(874520);
        long[] weights = Enumerable.Range(0, 20).Select(_ => random.NextInt64(0, 30)).ToArray();
        long[] values = Enumerable.Range(0, 20).Select(_ => random.NextInt64(0, 100)).ToArray();
        CompareWithOracle(weights, values, 100, logTimings: true);
    }

    private static MAXIMUMVALUEKNAPSACK Instance(long[] weights, long[] values, long capacity) {
        string pairs = string.Join(",", weights.Select((w, i) => $"({w.ToString(CultureInfo.InvariantCulture)},{values[i].ToString(CultureInfo.InvariantCulture)})"));
        return new MAXIMUMVALUEKNAPSACK($"(({pairs}),{capacity.ToString(CultureInfo.InvariantCulture)})");
    }

    private BigInteger CompareWithOracle(long[] weights, long[] values, long capacity, bool logTimings = false) {
        // Independent exhaustive oracle: raw arrays and BigInteger for both sums.
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
        var problem = Instance(weights, values, capacity);
        foreach (ISolver<MAXIMUMVALUEKNAPSACK> solver in new ISolver<MAXIMUMVALUEKNAPSACK>[] {
            new MaximumValueKnapsackModifiedGreedySolver(), new MaximumValueKnapsackBruteForceSolver(), new MaximumValueKnapsackBranchAndBoundSolver() }) {
            var clock = Stopwatch.StartNew();
            string certificate = solver.solve(problem);
            clock.Stop();
            Match match = Regex.Match(certificate, @"\A\(\{([0-9]+(?:,[0-9]+)*)?\},([0-9]+)\)\z");
            Assert.True(match.Success, certificate);
            int[] selected = match.Groups[1].Length == 0 ? [] : match.Groups[1].Value.Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            Assert.Equal(selected.Order(), selected);
            Assert.Equal(selected.Length, selected.Distinct().Count());
            Assert.All(selected, i => Assert.InRange(i, 1, weights.Length));
            BigInteger actualWeight = selected.Aggregate(BigInteger.Zero, (sum, i) => sum + weights[i - 1]);
            BigInteger actualValue = selected.Aggregate(BigInteger.Zero, (sum, i) => sum + values[i - 1]);
            Assert.True(actualWeight <= capacity);
            Assert.Equal(actualValue, BigInteger.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
            Assert.True(new MaximumValueKnapsackSolutionVerifier().verify(problem, certificate));
            if (solver is MaximumValueKnapsackModifiedGreedySolver) {
                Assert.True(2 * actualValue >= optimum);
                Assert.True(actualValue <= optimum);
                Assert.Equal(certificate, solver.solve(problem));
            } else Assert.Equal(optimum, actualValue);
            if (logTimings) output.WriteLine($"{solver.GetType().Name}: {clock.Elapsed.TotalMilliseconds:F3} ms, value={actualValue}, oracle OPT={optimum} (single run, not a benchmark).");
        }
        return optimum;
    }
}
