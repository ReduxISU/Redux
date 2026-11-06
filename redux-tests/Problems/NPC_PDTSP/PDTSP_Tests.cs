using API.Interfaces;
using API.Problems.NPComplete.NPC_PDTSP;
using API.Problems.NPComplete.NPC_PDTSP.Solvers;
using API.Problems.NPComplete.NPC_PDTSP.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class PDTSP_Tests {
    private const string Triangle = "(({P,D,S},{({S,P},1),({P,D},2),({D,S},3)}),{(P,D)},S,6)";
    private const string Complete = "(({S,P1,D1,P2,D2},{({S,P1},1),({S,D1},1),({S,P2},1),({S,D2},1),({P1,D1},1),({P1,P2},1),({P1,D2},1),({D1,P2},1),({D1,D2},1),({P2,D2},1)}),{(P1,D1),(P2,D2)},S,5)";

    private static ISolver<PDTSP> Solver(int kind) => kind switch {
        0 => new PDTSPBruteForce(),
        1 => new PDTSPBacktracking(),
        _ => new PDTSPIntelligent()
    };

    [Fact]
    public void DefaultInstance_ParsesAndPublishesUsableFormats() {
        var problem = new PDTSP();
        Assert.Equal(5, problem.nodes.Count);
        Assert.Equal(5, problem.edges.Count);
        Assert.Equal(2, problem.requests.Count);
        Assert.Equal(5, problem.K);
        Assert.Equal("S", problem.depot);
        Assert.Contains(problem.defaultInstance, problem.instanceFormat);
        Assert.Contains(PDTSPVerifier.CertificateExample, problem.certificateFormat);
        Assert.True(new PDTSPVerifier().verify(problem, PDTSPVerifier.CertificateExample));
        Assert.Equal(5, problem.graph.ToAPIGraph().nodes.Count);
    }

    [Fact]
    public void ApiDiscovery_ResolvesProblemSolversAndDefaults() {
        Assert.Equal(typeof(PDTSP), ProblemProvider.Problems["pdtsp"]);
        Assert.Equal(typeof(PDTSPBruteForce), ProblemProvider.Solvers["pdtspbruteforce"]);
        Assert.Equal(typeof(PDTSPBacktracking), ProblemProvider.Solvers["pdtspbacktracking"]);
        Assert.Equal(typeof(PDTSPIntelligent), ProblemProvider.Solvers["pdtspintelligent"]);
        IProblem problem = new PDTSP();
        Assert.IsType<PDTSPIntelligent>(problem.defaultSolver);
        Assert.IsType<PDTSPVerifier>(problem.defaultVerifier);
        string answer = problem.defaultSolver.solve(problem.instance);
        Assert.True(problem.defaultVerifier.verify(problem.instance, answer));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{{{")]
    [InlineData("not an instance")]
    [InlineData("(({},{}),{},S,0)")]
    [InlineData("(({S},{}),{},X,0)")]
    [InlineData("(({S},{}),{},S,-1)")]
    [InlineData("(({S},{}),{},S,1.5)")]
    [InlineData("(({S},{}),{},S,9223372036854775808)")]
    [InlineData("(({S},{}),{},S,0,extra)")]
    [InlineData("prefix(({S},{}),{},S,0)")]
    [InlineData("(({S},{}),{},S,0)suffix")]
    [InlineData("(({S},{}),{},S,0}")]
    [InlineData("(({S},{}),{},S,0")]
    [InlineData("(({S),{}),{},S,0)")]
    [InlineData("(({S},{}),{},S,0)(extra)")]
    [InlineData("(({S,P,D},{}),{},S,5)")]
    [InlineData("(({S,P,D},{}),{(P,X)},S,5)")]
    [InlineData("(({S,P,D},{}),{(S,P)},S,5)")]
    [InlineData("(({S,P,D},{}),{(P,P)},S,5)")]
    [InlineData("(({S,P,D},{}),{(P,D),(D,P)},S,5)")]
    [InlineData("(({S,P,D},{}),{{P,D}},S,5)")]
    [InlineData("(({S,P,D},{({S,X},1)}),{(P,D)},S,5)")]
    [InlineData("(({S,P,D},{({S,P},-1)}),{(P,D)},S,5)")]
    [InlineData("(({S,P,D},{({S,P},2147483648)}),{(P,D)},S,5)")]
    [InlineData("(({S,P,D},{({S,P},1),({P,S},2)}),{(P,D)},S,5)")]
    [InlineData("(({S,P,D},{({S,S},1)}),{(P,D)},S,5)")]
    [InlineData("(({S,P,D},{((S,P),1)}),{(P,D)},S,5)")]
    public void Parser_RejectsMalformedOrInconsistentInstances(string instance) {
        Assert.Throws<ProblemParseException>(() => new PDTSP(instance));
    }

    [Theory]
    [InlineData("{S,P,D,S}", true)]
    [InlineData(" { S, P, D, S } ", true)]
    [InlineData("{}", false)]
    [InlineData("{S,P,S}", false)]
    [InlineData("{S,P,P,S}", false)]
    [InlineData("{S,D,P,S}", false)]
    [InlineData("{P,D,S,P}", false)]
    [InlineData("{S,P,D}", false)]
    [InlineData("{S,P,D,S,S}", false)]
    [InlineData("{S,P,X,S}", false)]
    public void Verifier_ChecksCoverageEndpointsAndPrecedence(string certificate, bool expected) {
        var verifier = new PDTSPVerifier();
        Assert.Equal(expected, verifier.verify(new PDTSP(Triangle), certificate));
        Assert.Equal(certificate, verifier.certificate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("S,P,D,S")]
    [InlineData("{S,P,D,S")]
    [InlineData("{{S,P,D,S}}")]
    [InlineData("{S,,D,S}")]
    [InlineData("{S,(P,D),S}")]
    [InlineData("{S,P,D,S}garbage")]
    public void Verifier_ThrowsForMalformedCertificates(string certificate) {
        Assert.Throws<CertificateParseException>(() => new PDTSPVerifier().verify(new PDTSP(Triangle), certificate));
    }

    [Fact]
    public void Verifier_AllowsInterleavedRequests() {
        Assert.True(new PDTSPVerifier().verify(new PDTSP(Complete), "{S,P1,P2,D1,D2,S}"));
    }

    [Theory]
    [InlineData("(({S,P,D},{({S,P},1),({P,D},2)}),{(P,D)},S,100)")]
    [InlineData("(({S,P,D},{({S,P},1),({P,D},2),({D,S},3)}),{(P,D)},S,5)")]
    [InlineData("(({S,P,D},{({S,P},2147483647),({P,D},2147483647),({D,S},2147483647)}),{(P,D)},S,2147483647)")]
    public void Verifier_RejectsMissingReturnEdgeOrExcessiveTotalCost(string instance) {
        Assert.False(new PDTSPVerifier().verify(new PDTSP(instance), "{S,P,D,S}"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void AllSolvers_FindVerifiedSolutionsAndCanBeReused(int kind) {
        ISolver<PDTSP> solver = Solver(kind);
        string[] instances = {
            new PDTSP().defaultInstance, Triangle, Complete,
            "(({S},{}),{},S,0)",
            "(({S,P,D},{({P,S},0),({D,P},0),({S,D},0)}),{(P,D)},S,0)",
            "(({S,P,D},{({S,P},2147483647),({P,D},2147483647),({D,S},2147483647)}),{(P,D)},S,6442450941)",
            // Taking the cheapest initial edge S-P2 leads to a dead end.
            "(({S,P1,D1,P2,D2},{({S,P1},1),({P1,D1},1),({D1,P2},1),({P2,D2},1),({D2,S},1),({S,P2},0)}),{(P1,D1),(P2,D2)},S,5)"
        };
        foreach (string instance in instances) {
            var problem = new PDTSP(instance);
            string answer = solver.solve(problem);
            Assert.NotEqual("{}", answer);
            Assert.True(new PDTSPVerifier().verify(problem, answer), $"{solver.GetType().Name}: {answer} for {instance}");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void AllSolvers_ProveInfeasibility(int kind) {
        ISolver<PDTSP> solver = Solver(kind);
        string[] instances = {
            Triangle.Replace(",S,6)", ",S,5)"),
            "(({S,P,D},{({S,P},1),({P,D},1)}),{(P,D)},S,100)",
            "(({S,P,D},{}),{(P,D)},S,100)",
            // The two orientations of this cycle each violate one request.
            "(({S,P1,D1,P2,D2},{({S,P1},1),({P1,D2},1),({D2,D1},1),({D1,P2},1),({P2,S},1)}),{(P1,D1),(P2,D2)},S,100)"
        };
        foreach (string instance in instances)
            Assert.Equal("{}", solver.solve(new PDTSP(instance)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void AllSolvers_HonorTimeoutAndReset(int kind) {
        ISolver<PDTSP> solver = Solver(kind);
        solver.timerHasExpired = true;
        Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(new PDTSP()));
        solver.ResetTimer();
        Assert.True(new PDTSPVerifier().verify(new PDTSP(), solver.solve(new PDTSP())));
        Assert.True(solver.Run(new PDTSP().defaultInstance, false, TimeSpan.Zero).TimedOut);
    }

    [Fact]
    public void IntelligentSolver_HandlesMoreThan64Vertices() {
        string[] labels = Enumerable.Range(0, 65).Select(i => $"v{i}").ToArray();
        string edges = string.Join(",", Enumerable.Range(0, 65).Select(i => $"({{{labels[i]},{labels[(i + 1) % 65]}}},1)"));
        string requests = string.Join(",", Enumerable.Range(0, 32).Select(i => $"({labels[2 * i + 1]},{labels[2 * i + 2]})"));
        var problem = new PDTSP($"(({{{string.Join(",", labels)}}},{{{edges}}}),{{{requests}}},v0,65)");
        string answer = new PDTSPIntelligent().solve(problem);
        Assert.True(new PDTSPVerifier().verify(problem, answer));
    }

    [Fact]
    public void AllSolvers_AgreeWithIndependentExhaustiveOracle() {
        var random = new Random(1739);
        // Include complete/sparse graphs, zero weights, arbitrary depot positions,
        // and exact feasibility boundaries. The oracle uses no production parser or verifier.
        for (int sample = 0; sample < 36; sample++) {
            int n = sample < 24 ? 5 : 7;
            int[,] weights = new int[n, n];
            var edgeText = new List<string>();
            for (int i = 0; i < n; i++) {
                for (int j = i + 1; j < n; j++) {
                    int weight = sample % 3 == 0 && random.Next(4) == 0 ? -1 : random.Next(10);
                    weights[i, j] = weights[j, i] = weight;
                    if (weight >= 0)
                        edgeText.Add($"({{v{i},v{j}}},{weight})");
                }
            }
            long optimum = OracleMinimum(weights);
            long[] budgets = optimum == long.MaxValue ? new long[] { 100 } : new[] { Math.Max(0, optimum - 1), optimum, optimum + 5 };
            string labels = string.Join(",", Enumerable.Range(0, n).OrderBy(_ => random.Next()).Select(i => $"v{i}"));
            string requests = string.Join(",", Enumerable.Range(0, (n - 1) / 2).Select(i => $"(v{2 * i + 1},v{2 * i + 2})"));
            foreach (long budget in budgets) {
                string instance = $"(({{{labels}}},{{{string.Join(",", edgeText)}}}),{{{requests}}},v0,{budget})";
                var problem = new PDTSP(instance);
                for (int kind = 0; kind < 3; kind++) {
                    string answer = Solver(kind).solve(problem);
                    Assert.Equal(optimum <= budget, answer != "{}");
                    if (answer != "{}")
                        Assert.True(new PDTSPVerifier().verify(problem, answer), $"{kind}: {answer} for {instance}");
                }
            }
        }
    }

    private static long OracleMinimum(int[,] weights) {
        int n = weights.GetLength(0);
        int[] permutation = Enumerable.Range(1, n - 1).ToArray();
        long minimum = long.MaxValue;

        void Enumerate(int depth) {
            if (depth < permutation.Length) {
                for (int i = depth; i < permutation.Length; i++) {
                    (permutation[depth], permutation[i]) = (permutation[i], permutation[depth]);
                    Enumerate(depth + 1);
                    (permutation[depth], permutation[i]) = (permutation[i], permutation[depth]);
                }
                return;
            }
            for (int pickup = 1; pickup < n; pickup += 2)
                if (Array.IndexOf(permutation, pickup) > Array.IndexOf(permutation, pickup + 1))
                    return;
            long cost = 0;
            int previous = 0;
            foreach (int next in permutation.Append(0)) {
                if (weights[previous, next] < 0)
                    return;
                cost += weights[previous, next];
                previous = next;
            }
            minimum = Math.Min(minimum, cost);
        }

        Enumerate(0);
        return minimum;
    }
}
