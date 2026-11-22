using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using API.Interfaces;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Solvers;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The oracle owns the pre-serialization graph. It never reads the production
// model's nodes, edges, weights, adjacency, value, or certificate helpers.
internal sealed record IndependentCliqueGraph(long[] Weights, HashSet<(int, int)> Edges) {
    internal int N => Weights.Length;
    internal bool Feasible(int[] selected) => selected.Distinct().Count() == selected.Length
        && selected.All(v => v >= 0 && v < N)
        && selected.SelectMany((a, i) => selected.Take(i).Select(b => (Math.Min(a, b), Math.Max(a, b))))
            .All(Edges.Contains);
    internal long Value(int[] selected) => selected.Aggregate(0L, (sum, v) => checked(sum + Weights[v]));
    internal int[] Subset(int mask) => Enumerable.Range(0, N).Where(v => (mask & (1 << v)) != 0).ToArray();
    internal long Optimum() {
        if (N > 20) throw new ArgumentOutOfRangeException(nameof(N));
        long best = 0;
        for (int mask = 0; mask < (1 << N); mask++) {
            int[] selected = Subset(mask);
            if (Feasible(selected)) best = Math.Max(best, Value(selected));
        }
        return best;
    }
    internal string Certificate(int[] selected, long? value = null) =>
        "({" + string.Join(",", selected.Select(v => $"v{v}")) + "},"
        + (value ?? Value(selected)).ToString(CultureInfo.InvariantCulture) + ")";
    internal string Input(Random? shuffle = null, bool duplicateEdges = false) {
        int[] order = Enumerable.Range(0, N).ToArray();
        var edges = Edges.Select(e => $"{{v{e.Item1},v{e.Item2}}}").ToList();
        if (duplicateEdges) edges.AddRange(Edges.SelectMany(e => new[] {
            $"{{v{e.Item2},v{e.Item1}}}", $"{{v{e.Item1},v{e.Item2}}}" }));
        if (shuffle != null) { shuffle.Shuffle(order); shuffle.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(edges)); }
        string nodes = string.Join(",", order.Select(v => $"v{v}"));
        string assignments = string.Join(",", order.Reverse().Select(v =>
            $"(v{v},{Weights[v].ToString(CultureInfo.InvariantCulture)})"));
        return $"(({{{nodes}}},{{{string.Join(",", edges)}}}),({assignments}))";
    }
    internal long CheckCertificate(string certificate) {
        var match = Regex.Match(certificate, @"\A\(\{(v[0-9]+(?:,v[0-9]+)*)?\},([0-9]+)\)\z",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Assert.True(match.Success, certificate);
        int[] selected = match.Groups[1].Length == 0 ? []
            : match.Groups[1].Value.Split(',').Select(v => int.Parse(v.AsSpan(1), CultureInfo.InvariantCulture)).ToArray();
        Assert.True(Feasible(selected), certificate);
        long reported = long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        Assert.Equal(Value(selected), reported);
        return reported;
    }
    internal static IndependentCliqueGraph Generate(Random random, int n, double density, int distribution) {
        long[] weights = Enumerable.Range(0, n).Select(v => distribution switch {
            0 => 0L,
            1 => 1L,
            2 => v == 0 ? 1000L : random.Next(0, 10),
            3 => random.NextInt64(0, long.MaxValue / Math.Max(1, n)),
            _ => random.Next(0, 25)
        }).ToArray();
        var edges = new HashSet<(int, int)>();
        for (int a = 0; a < n; a++)
            for (int b = a + 1; b < n; b++)
                if (random.NextDouble() < density) edges.Add((a, b));
        return new(weights, edges);
    }
    internal static IndependentCliqueGraph Multipartite(int parts, int size) {
        int n = parts * size;
        return new(Enumerable.Repeat(1L, n).ToArray(), Enumerable.Range(0, n)
            .SelectMany(a => Enumerable.Range(a + 1, n - a - 1)
                .Where(b => a / size != b / size).Select(b => (a, b))).ToHashSet());
    }
}

public class WeightedMaxCliqueIndependent_Tests(ITestOutputHelper output) {
    internal static ISolver<WEIGHTEDMAXCLIQUE>[] Solvers() => [
        new WeightedMaxCliqueBruteForceSolver(), new WeightedMaxCliqueBranchAndBoundSolver(),
        new WeightedMaxCliqueGreedyLocalSearchSolver()
    ];

    [Theory]
    [InlineData(17)]
    [InlineData(860)]
    [InlineData(2026)]
    [InlineData(71237)]
    [InlineData(314159)]
    public void RandomSolvers_AgreeWithRawGraphOracle(int seed) {
        var random = new Random(seed);
        double[] densities = [0, .05, .15, .35, .5, .7, .9, 1];
        int heuristicOptimal = 0;
        for (int trial = 0; trial < 200; trial++) {
            var graph = IndependentCliqueGraph.Generate(random, trial % 15, densities[trial % 8], trial % 6);
            long optimum = graph.Optimum();
            var problem = new WEIGHTEDMAXCLIQUE(graph.Input());
            foreach (var solver in Solvers()) {
                string certificate = solver.solve(problem);
                long actual = graph.CheckCertificate(certificate);
                bool exact = solver is not WeightedMaxCliqueGreedyLocalSearchSolver;
                Assert.True(exact ? actual == optimum : actual <= optimum,
                    $"seed={seed}, trial={trial}, solver={solver.GetType().Name}, input={problem.instance}, certificate={certificate}, optimum={optimum}");
                if (!exact && actual == optimum) heuristicOptimal++;
                Assert.Equal(certificate, solver.solve(problem));
                Assert.Equal(certificate, solver.solve(new WEIGHTEDMAXCLIQUE(graph.Input(random, true))));
            }
        }
        output.WriteLine($"seed={seed}: graphs=200; exact comparisons=400; heuristic feasible=200; heuristic optimal={heuristicOptimal}; deterministic/order checks=1200");
    }

    [Fact]
    public void AllTopologiesThroughFiveVertices_MatchOracleWithTwoWeightPatterns() {
        int graphs = 0, heuristicOptimal = 0;
        for (int n = 0; n <= 5; n++) {
            var pairs = Enumerable.Range(0, n).SelectMany(a => Enumerable.Range(a + 1, n - a - 1).Select(b => (a, b))).ToArray();
            for (int topology = 0; topology < (1 << pairs.Length); topology++) {
                var edges = pairs.Where((_, i) => (topology & (1 << i)) != 0).ToHashSet();
                foreach (long[] weights in new[] { Enumerable.Repeat(1L, n).ToArray(), Enumerable.Range(0, n).Select(v => v % 3 == 0 ? 0L : 2L * v + 1).ToArray() }) {
                    var graph = new IndependentCliqueGraph(weights, edges);
                    long optimum = graph.Optimum();
                    var problem = new WEIGHTEDMAXCLIQUE(graph.Input());
                    foreach (var solver in Solvers()) {
                        long actual = graph.CheckCertificate(solver.solve(problem));
                        Assert.True(solver is WeightedMaxCliqueGreedyLocalSearchSolver ? actual <= optimum : actual == optimum,
                            $"n={n}, topology={topology}, input={problem.instance}, solver={solver.GetType().Name}");
                        if (solver is WeightedMaxCliqueGreedyLocalSearchSolver && actual == optimum) heuristicOptimal++;
                    }
                    graphs++;
                }
            }
        }
        output.WriteLine($"exhaustive weighted graphs={graphs}; exact comparisons={graphs * 2}; heuristic feasible={graphs}; heuristic optimal={heuristicOptimal}");
    }

    [Theory]
    [InlineData(603)]
    [InlineData(618)]
    [InlineData(860)]
    [InlineData(1008)]
    public void RandomVerifiers_CheckEverySubsetAgainstOracle(int seed) {
        var random = new Random(seed);
        int certificates = 0, feasible = 0, optimal = 0;
        for (int trial = 0; trial < 40; trial++) {
            var graph = IndependentCliqueGraph.Generate(random, trial % 8, (trial % 5) / 4.0, trial % 6);
            var problem = new WEIGHTEDMAXCLIQUE(graph.Input());
            long optimum = graph.Optimum();
            var solution = new WeightedMaxCliqueSolutionVerifier();
            var optimality = new WeightedMaxCliqueOptimalityVerifier();
            for (int mask = 0; mask < (1 << graph.N); mask++) {
                int[] selected = graph.Subset(mask);
                bool expected = graph.Feasible(selected);
                long value = graph.Value(selected);
                string certificate = graph.Certificate(selected);
                Assert.Equal(expected, solution.verify(problem, certificate));
                Assert.Equal(expected && value == optimum, optimality.verify(problem, certificate));
                // Off-by-one values are representable: generated total is strictly below Int64.MaxValue.
                Assert.False(solution.verify(problem, graph.Certificate(selected, value + 1)));
                Assert.False(optimality.verify(problem, graph.Certificate(selected, value + 1)));
                if (expected) feasible++;
                if (expected && value == optimum) optimal++;
                certificates++;
            }
            Assert.False(solution.verify(problem, "({unknown},0)"));
            Assert.False(optimality.verify(problem, "({unknown},0)"));
            Assert.Throws<CertificateParseException>(() => solution.verify(problem, "({v0,v0},0)"));
            Assert.Throws<CertificateParseException>(() => optimality.verify(problem, "({v0,v0},0)"));
        }
        output.WriteLine($"seed={seed}: graphs=40; subset certificates={certificates}; feasible={feasible}; optimal={optimal}; verifier comparisons={certificates * 4 + 160}");
    }

    [Theory]
    [InlineData("({a},4)({a},4)")]
    [InlineData("({a},4),")]
    [InlineData("({a};4)")]
    [InlineData("([a],4)")]
    [InlineData("({a},4]")]
    [InlineData("({a},4 0)")]
    [InlineData("({a},٤)")]
    [InlineData("({a},4:ignored)")]
    [InlineData("({a},4|ignored)")]
    [InlineData("({a},NaN)")]
    [InlineData("({a},Infinity)")]
    [InlineData("({a},18446744073709551615)")]
    [InlineData("({a},-9223372036854775809)")]
    [InlineData("({a},0x4)")]
    [InlineData("({a},4_0)")]
    [InlineData("({a},4,) ")]
    [InlineData("({a,{}},4)")]
    public void AdditionalMalformedCertificates_AreParseErrors(string certificate) {
        var problem = new WEIGHTEDMAXCLIQUE();
        foreach (IVerifier<WEIGHTEDMAXCLIQUE> verifier in new IVerifier<WEIGHTEDMAXCLIQUE>[] {
            new WeightedMaxCliqueSolutionVerifier(), new WeightedMaxCliqueOptimalityVerifier() }) {
            Assert.Throws<CertificateParseException>(() => verifier.verify(problem, certificate));
            Assert.Throws<CertificateParseException>(() => verifier.verify(problem.instance, certificate));
        }
    }

    [Theory]
    [InlineData(false, "({a,b,c},12\u0000)")]
    [InlineData(true, "({a,b,c},12\u0000)")]
    [InlineData(false, "({},0\u0000)")]
    [InlineData(true, "({},0\u0000)")]
    [InlineData(false, "({},0\u0000\u0000)")]
    [InlineData(true, "({},0\u0000\u0000)")]
    public void NullTerminatedObjective_IsMalformedForBothVerifiers(bool optimality, string certificate) {
        var problem = new WEIGHTEDMAXCLIQUE("(({},{}),())");
        if (certificate.Contains('a')) problem = new WEIGHTEDMAXCLIQUE();
        IVerifier<WEIGHTEDMAXCLIQUE> verifier = optimality ? new WeightedMaxCliqueOptimalityVerifier() : new WeightedMaxCliqueSolutionVerifier();
        Assert.Throws<CertificateParseException>(() => verifier.verify(problem, certificate));
    }

    [Fact]
    public void NullTerminatedInstanceWeight_IsMalformed() {
        Assert.Throws<ProblemParseException>(() => new WEIGHTEDMAXCLIQUE("(({a},{}),((a,4\u0000)))"));
    }

    [Fact]
    public void IndependentOracle_ConfirmsHeuristicCanBeFeasibleAndSuboptimal() {
        var graph = new IndependentCliqueGraph([10, 10, 10, 6, 6, 6], [(0, 3), (1, 4), (2, 5), (3, 4), (3, 5), (4, 5)]);
        Assert.Equal(18, graph.Optimum());
        Assert.Equal(16, graph.CheckCertificate(new WeightedMaxCliqueGreedyLocalSearchSolver().solve(new WEIGHTEDMAXCLIQUE(graph.Input()))));
    }

    [Fact]
    public void Int64Boundaries_HeavyTrapAndDisconnectedTies_MatchIndependentOptima() {
        IndependentCliqueGraph[] cases = [
            new([long.MaxValue - 10, 5, 5], [(0, 1), (0, 2), (1, 2)]),
            new([long.MaxValue - 10, 5, 5], [(1, 2)]),
            new([9, 6, 6, 6], [(1, 2), (1, 3), (2, 3)]),
            new([5, 5, 5, 5, 0], [(0, 1), (2, 3)])
        ];
        foreach (var graph in cases) {
            var problem = new WEIGHTEDMAXCLIQUE(graph.Input(duplicateEdges: true));
            long optimum = graph.Optimum();
            foreach (var solver in Solvers()) {
                long actual = graph.CheckCertificate(solver.solve(problem));
                Assert.True(solver is WeightedMaxCliqueGreedyLocalSearchSolver ? actual <= optimum : actual == optimum);
            }
            for (int mask = 0; mask < (1 << graph.N); mask++) {
                int[] selected = graph.Subset(mask);
                Assert.Equal(graph.Feasible(selected), new WeightedMaxCliqueSolutionVerifier().verify(problem, graph.Certificate(selected)));
                Assert.Equal(graph.Feasible(selected) && graph.Value(selected) == optimum,
                    new WeightedMaxCliqueOptimalityVerifier().verify(problem, graph.Certificate(selected)));
            }
        }
        Assert.Throws<ProblemParseException>(() => new WEIGHTEDMAXCLIQUE("(({a,b},{}),((a,9223372036854775807),(b,1)))"));
    }

    [Theory]
    [InlineData(21)]
    [InlineData(40)]
    [InlineData(64)]
    public void LargeKnownOptima_AreSupportedWithoutExhaustiveOracle(int n) {
        var random = new Random(860 + n);
        var complete = IndependentCliqueGraph.Generate(random, n, 1, 4);
        var isolated = IndependentCliqueGraph.Generate(random, n, 0, 4);
        var multipartite = IndependentCliqueGraph.Multipartite(3, n / 3);
        foreach (var graph in new[] { complete, isolated, multipartite }) {
            long optimum = ReferenceEquals(graph, complete) ? graph.Weights.Sum()
                : ReferenceEquals(graph, isolated) ? graph.Weights.Max() : 3;
            var problem = new WEIGHTEDMAXCLIQUE(graph.Input());
            foreach (var solver in Solvers().Skip(1)) {
                long actual = graph.CheckCertificate(solver.solve(problem));
                Assert.True(solver is WeightedMaxCliqueGreedyLocalSearchSolver ? actual <= optimum : actual == optimum);
            }
            Assert.True(new WeightedMaxCliqueSolutionVerifier().verify(problem, "({},0)"));
            Assert.Throws<ExactSearchLimitExceededException>(() => new WeightedMaxCliqueBruteForceSolver().solve(problem));
            Assert.Throws<ExactSearchLimitExceededException>(() => new WeightedMaxCliqueOptimalityVerifier().verify(problem, "({},0)"));
        }
    }

    [Fact]
    public void TwentyVertexCompleteGraph_ExercisesAllSubsetsAtInt64Boundary() {
        long[] weights = Enumerable.Repeat(1L, 20).ToArray();
        weights[0] = long.MaxValue - 19;
        var graph = IndependentCliqueGraph.Generate(new Random(860), 20, 1, 1) with { Weights = weights };
        var problem = new WEIGHTEDMAXCLIQUE(graph.Input());
        foreach (var solver in Solvers()) {
            long actual = graph.CheckCertificate(solver.solve(problem));
            Assert.True(solver is WeightedMaxCliqueGreedyLocalSearchSolver ? actual <= long.MaxValue : actual == long.MaxValue);
        }
        Assert.True(new WeightedMaxCliqueOptimalityVerifier().verify(problem, graph.Certificate(Enumerable.Range(0, 20).ToArray())));
    }

    [Fact]
    public async Task BranchAndBound_InterruptedAfterSearchStarts_ReturnsNoIncumbentCertificate() {
        var graph = IndependentCliqueGraph.Multipartite(16, 3);
        var problem = new WEIGHTEDMAXCLIQUE(graph.Input());
        var solver = new WeightedMaxCliqueBranchAndBoundSolver();
        Task<string> work = Task.Run(() => solver.solve(problem), TestContext.Current.CancellationToken);
        try {
            Assert.True(SpinWait.SpinUntil(() => solver.SearchNodes >= 100 || work.IsCompleted, TimeSpan.FromSeconds(10)));
            Assert.False(work.IsCompleted);
        } finally {
            solver.timerHasExpired = true;
        }
        Assert.Equal(SolveRun.TimedOutAnswer, await work.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.True(solver.SearchNodes >= 100);
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public void Benchmark_ExactSearchReportsMeasurementsWithoutTimeAssertions() {
        // Run separately from correctness tests; parsing is excluded, one warmup,
        // three measurements, median elapsed time. Counters are production counters.
        output.WriteLine("case,n,m,distribution,optimum,brute_ms,bound_ms,nodes,prunes");
        foreach (int n in new[] { 0, 1, 12, 16, 20 }) {
            foreach (double density in new[] { 0.0, .15, .5, .85, 1.0 }) {
                var graph = IndependentCliqueGraph.Generate(new Random(860 + n), n, density, density == .5 ? 2 : 4);
                Measure($"random-{density}", graph, density == .5 ? "heavy-first" : "uniform-0-24", null);
            }
        }
        foreach (int n in new[] { 18, 24, 30, 36 })
            Measure("multipartite", IndependentCliqueGraph.Multipartite(n / 3, 3), "unit", n / 3);

        void Measure(string name, IndependentCliqueGraph graph, string distribution, long? knownOptimum) {
            var problem = new WEIGHTEDMAXCLIQUE(graph.Input());
            var bound = new WeightedMaxCliqueBranchAndBoundSolver();
            var brute = new WeightedMaxCliqueBruteForceSolver();
            long optimum = knownOptimum ?? graph.CheckCertificate(brute.solve(problem));
            graph.CheckCertificate(bound.solve(problem));
            double Median(ISolver<WEIGHTEDMAXCLIQUE> solver) {
                double[] times = new double[3];
                for (int i = 0; i < times.Length; i++) {
                    var watch = Stopwatch.StartNew();
                    string certificate = solver.solve(problem);
                    times[i] = watch.Elapsed.TotalMilliseconds;
                    Assert.Equal(optimum, graph.CheckCertificate(certificate));
                }
                Array.Sort(times);
                return times[1];
            }
            double? bruteMs = graph.N <= 20 ? Median(brute) : null;
            double boundMs = Median(bound);
            output.WriteLine(FormattableString.Invariant($"{name},{graph.N},{graph.Edges.Count},{distribution},{optimum},{bruteMs:F4},{boundMs:F4},{bound.SearchNodes},{bound.PrunedBranches}"));
        }
    }
}
