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

public class MaximumValueKnapsackBruteForce_Tests {
    [Theory]
    [InlineData(MAXIMUMVALUEKNAPSACK.DefaultInstance, "({2,3},220)")]
    [InlineData("((),0)", "({},0)")]
    [InlineData("((),9223372036854775807)", "({},0)")]
    [InlineData("(((1,5)),0)", "({},0)")]
    [InlineData("(((0,7),(0,0),(1,5)),0)", "({1},7)")]
    [InlineData("(((1,0),(0,0)),1)", "({},0)")]
    [InlineData("(((2,3),(2,3)),4)", "({1,2},6)")]
    [InlineData("(((2,3),(2,3)),2)", "({1},3)")]
    [InlineData("(((1,3),(1,3),(2,6)),2)", "({1,2},6)")]
    [InlineData("(((20,100),(10,60),(10,55)),20)", "({2,3},115)")]
    [InlineData("(((1,4),(2,7),(3,9)),6)", "({1,2,3},20)")]
    [InlineData("(((0,9223372036854775806),(0,1)),0)", "({1,2},9223372036854775807)")]
    [InlineData("(((9223372036854775807,1),(9223372036854775807,2)),9223372036854775807)", "({2},2)")]
    [InlineData("(((9223372036854775807,9223372036854775807)),9223372036854775807)", "({1},9223372036854775807)")]
    public void KnownOptima_AreFeasibleDeterministicAndOptimal(string input, string expected) {
        var problem = new MAXIMUMVALUEKNAPSACK(input);
        var solver = new MaximumValueKnapsackBruteForceSolver();
        Assert.Equal(expected, solver.solve(problem));
        Assert.Equal(expected, solver.solve(problem));
        Assert.Equal(expected, ((ISolver)solver).solve(input));
        Assert.True(new MaximumValueKnapsackSolutionVerifier().verify(problem, expected));
        Assert.True(new MaximumValueKnapsackOptimalityVerifier().verify(problem, expected));
    }

    internal static string UnitItems(int count) => "((" + string.Join(",", Enumerable.Repeat("(1,1)", count)) + $"),{count})";

    [Theory]
    [InlineData(19)]
    [InlineData(20)]
    public void ExactBoundary_EnumeratesAllSubsetsAndFindsLastMaskOptimum(int count) {
        var problem = new MAXIMUMVALUEKNAPSACK(UnitItems(count));
        string certificate = new MaximumValueKnapsackBruteForceSolver().solve(problem);
        Assert.Equal("({" + string.Join(",", Enumerable.Range(1, count)) + $"}},{count})", certificate);
        Assert.True(new MaximumValueKnapsackOptimalityVerifier().verify(problem, certificate));
    }

    [Fact]
    public void TwentyOneItems_StillParseAndVerifyButBruteForceRefuses() {
        var problem = new MAXIMUMVALUEKNAPSACK(UnitItems(21));
        Assert.Equal(21, problem.items.Count);
        Assert.True(new MaximumValueKnapsackSolutionVerifier().verify(problem, "({21},1)"));
        Assert.False(new MaximumValueKnapsackOptimalityVerifier().verify(problem, "({21},1)"));
        Assert.False(((IVerifier)new MaximumValueKnapsackOptimalityVerifier()).verify(problem.instance, "({21},1)"));
        ISolver solver = new MaximumValueKnapsackBruteForceSolver();
        foreach (Action action in new Action[] {
            () => new MaximumValueKnapsackBruteForceSolver().solve(problem),
            () => solver.solve(problem.instance) }) {
            var error = Assert.Throws<ExactSearchLimitExceededException>(action);
            Assert.Equal("Maximum-Value 0-1 Knapsack", error.ProblemName);
            Assert.Equal(21, error.VariableCount);
            Assert.Equal(20, error.MaxVariables);
        }
        solver.TimerExpired();
        Assert.Throws<ExactSearchLimitExceededException>(() => solver.solve(problem.instance));
    }

    [Theory]
    [InlineData(MAXIMUMVALUEKNAPSACK.DefaultInstance)]
    [InlineData("((),0)")]
    public void Timer_PreexpiredAndZeroDeadlineNeverReturnCertificate(string input) {
        ISolver solver = new MaximumValueKnapsackBruteForceSolver();
        solver.TimerExpired();
        Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(input));
        foreach (bool steps in new[] { false, true }) {
            SolveRun run = solver.Run(input, steps, TimeSpan.Zero);
            Assert.True(run.TimedOut);
            Assert.Equal(SolveRun.TimedOutAnswer, run.Answer);
            Assert.Empty(run.Steps);
        }
        solver.ResetTimer();
        Assert.True(((IVerifier)new MaximumValueKnapsackSolutionVerifier()).verify(input, solver.solve(input)));
        Assert.False(solver.Run(input, false).TimedOut);
    }

    [Fact]
    public void Timer_StopsEnumerationInProgressWithoutReturningIncumbent() {
        ISolver solver = new MaximumValueKnapsackBruteForceSolver();
        SolveRun run = solver.Run(UnitItems(20), false, TimeSpan.FromMilliseconds(1));
        Assert.True(run.TimedOut);
        Assert.Equal(SolveRun.TimedOutAnswer, run.Answer);
        Assert.Empty(run.Steps);
    }

    [Fact]
    public void MetadataAndDefaults_AreDiscoveredWithoutChangingSolutionVerifier() {
        IProblem problem = new MAXIMUMVALUEKNAPSACK();
        var solver = Assert.IsType<MaximumValueKnapsackBruteForceSolver>(problem.defaultSolver);
        Assert.Same(solver, problem.defaultSolver);
        Assert.IsType<MaximumValueKnapsackSolutionVerifier>(problem.defaultVerifier);
        Assert.Equal(typeof(MaximumValueKnapsackBruteForceSolver), ProblemProvider.Solvers["maximumvalueknapsackbruteforcesolver"]);
        Assert.Equal(typeof(MaximumValueKnapsackBruteForceSolver), ProblemDefaults.Find(typeof(MAXIMUMVALUEKNAPSACK), typeof(ISolver)));
        Assert.Equal(typeof(MAXIMUMVALUEKNAPSACK), ProblemProvider.ProblemTypeOf(typeof(MaximumValueKnapsackBruteForceSolver), typeof(ISolver<>)));
        Assert.Equal(SolverType.BruteForce, solver.solverType);
        Assert.Equal(SolverComplexityBucket.Exponential, solver.complexityBucket);
        Assert.Equal(24, solver.timeComplexity.Evaluate(new Dictionary<string, double> { ["i"] = 3 }));
        Assert.Equal(3, solver.spaceComplexity.Evaluate(new Dictionary<string, double> { ["i"] = 3 }));
        Assert.Contains("optimal", solver.solverDefinition);
        Assert.NotEmpty(solver.source);
        Assert.NotEmpty(solver.sourceLink);
        Assert.NotEmpty(solver.contributors);
        Assert.EndsWith("MaximumValueKnapsackBruteForceSolver.cs", solver.sourceFile);
    }

    [Theory]
    [InlineData(874)]
    [InlineData(603)]
    [InlineData(2026)]
    public void SmallRandomInstances_AgreeWithIndependentRawArrayBigIntegerOracle(int seed) {
        var random = new Random(seed);
        for (int trial = 0; trial < 60; trial++) {
            int n = trial % 10;
            long[] weights = Enumerable.Range(0, n).Select(_ => random.Next(5) == 0 ? long.MaxValue : random.NextInt64(0, 20)).ToArray();
            long[] values = Enumerable.Range(0, n).Select(_ => trial % 3 == 0
                ? random.NextInt64(0, long.MaxValue / Math.Max(1, n)) : random.NextInt64(0, 15)).ToArray();
            long capacity = trial % 4 == 0 ? long.MaxValue : random.NextInt64(0, 40);
            string pairs = string.Join(",", weights.Select((w, i) => $"({w.ToString(CultureInfo.InvariantCulture)},{values[i].ToString(CultureInfo.InvariantCulture)})"));
            string input = $"(({pairs}),{capacity.ToString(CultureInfo.InvariantCulture)})";
            // Oracle reads only the raw arrays and uses BigInteger for BOTH totals.
            BigInteger optimum = 0;
            for (int mask = 0; mask < (1 << n); mask++) {
                BigInteger weight = 0, value = 0;
                for (int i = 0; i < n; i++) {
                    if ((mask & (1 << i)) == 0) continue;
                    weight += weights[i];
                    value += values[i];
                }
                if (weight <= capacity && value > optimum) optimum = value;
            }
            var problem = new MAXIMUMVALUEKNAPSACK(input);
            var solver = new MaximumValueKnapsackBruteForceSolver();
            string certificate = solver.solve(problem);
            // Independently parse and check output, without production parser/evaluation helpers.
            Match match = Regex.Match(certificate, @"\A\(\{([0-9]+(?:,[0-9]+)*)?\},([0-9]+)\)\z");
            Assert.True(match.Success, certificate);
            int[] selected = match.Groups[1].Length == 0 ? [] : match.Groups[1].Value.Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            Assert.Equal(selected.Order(), selected);
            Assert.Equal(selected.Length, selected.Distinct().Count());
            Assert.All(selected, i => Assert.InRange(i, 1, n));
            BigInteger actualWeight = selected.Aggregate(BigInteger.Zero, (sum, i) => sum + weights[i - 1]);
            BigInteger actualValue = selected.Aggregate(BigInteger.Zero, (sum, i) => sum + values[i - 1]);
            Assert.True(actualWeight <= capacity);
            Assert.Equal(optimum, actualValue);
            Assert.Equal(actualValue, BigInteger.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
            Assert.Equal(certificate, solver.solve(problem));
            Assert.True(new MaximumValueKnapsackOptimalityVerifier().verify(problem, certificate));
        }
    }
}
