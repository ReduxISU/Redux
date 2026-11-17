using System.Diagnostics;
using System.Text.RegularExpressions;
using API.Interfaces;
using API.Problems.NPComplete.NPC_EDITDISTANCE;
using API.Problems.NPComplete.NPC_KNAPSACK;
using API.Problems.NPComplete.NPC_SETCOVER;
using API.Problems.NPComplete.NPC_STEINERTREE;
using API.Problems.NPComplete.NPC_SUBSETSUM;
using API.Problems.NPComplete.NPC_TSP;
using API.Problems.NPComplete.NPC_TSP.Solvers;
using API.Problems.P.P_DFA;
using redux_tests.Fixtures;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Precheck inputs shared with the endpoint tests (#843).
internal static class PrecheckInputs {
    internal static string Nodes(int n) => "{" + string.Join(",", Enumerable.Range(0, n).Select(i => $"v{i}")) + "}";

    // A complete graph on n nodes, every edge of weight 1, and K = 0, so no tour is short enough: a brute force has to
    // try every permutation.
    internal static string CompleteTsp(int n) {
        var edges = new List<string>();
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                edges.Add($"({{v{i},v{j}}},1)");
        return $"(({Nodes(n)},{{{string.Join(",", edges)}}}),0)";
    }

    // A path on n nodes as a TSP instance: short, however many nodes, so a huge |N| is cheap to send.
    internal static string PathTsp(int n) =>
        $"(({Nodes(n)},{{{string.Join(",", Enumerable.Range(0, n - 1).Select(i => $"({{v{i},v{i + 1}}},1)"))}}}),0)";

    // TSP brute force on 15 nodes: 15! * 15 * 105, about 2.1e15 steps, which no time limit fits.
    internal static readonly string HopelessTsp = CompleteTsp(15);
}

// The Big-O precheck (#843): size measures, the estimate, and which solvers it checks.
public class ComputePrecheck_Tests {
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value)).Build();

    private static readonly TimeSpan ThirtySeconds = TimeSpan.FromSeconds(30);

    // ── Size measures ───────────────────────────────────────────────────────────

    // Every graph problem but the automata (DFA, NFA, a later slice) reports |N| and |E| from the instance it parsed.
    public static TheoryData<string> GraphProblems() {
        var data = new TheoryData<string>();
        // Only Redux's own problems: the test assembly's fixtures (DefaultFor_Fixtures) are scanned too.
        foreach (var (key, type) in ProblemProvider.GraphProblems.OrderBy(p => p.Key, StringComparer.Ordinal))
            if (key is not ("dfa" or "nfa") && type.Namespace?.StartsWith("API.Problems.", StringComparison.Ordinal) == true)
                data.Add(key);
        return data;
    }

    [Theory]
    [MemberData(nameof(GraphProblems))]
    public void GraphProblem_ReportsItsNodeAndEdgeCounts(string problem) {
        var instance = (IProblem)Activator.CreateInstance(ProblemProvider.Problems[problem])!;
        var measures = instance.SizeMeasures().ToDictionary(m => m.Part);
        var graph = (IGraphProblem)instance;
        var json = graph.graph.ToAPIGraph();
        Assert.True(measures["N"].IsCount);
        Assert.True(measures["E"].IsCount);
        Assert.Equal(json.nodes.Count, measures["N"].Value);
        Assert.Equal(json.links.Count, measures["E"].Value);
        Assert.Equal(measures.Count, instance.SizeMeasures().Count); // each part once
    }

    // The problems outside the graph problems that report their size measures (#843): the SAT family's formulas and
    // the families whose instance grammar names their parts.
    private static readonly string[] OtherReportingProblems = {
        "binpacking", "changemaking", "convexhull", "dfa", "dm3", "editdistance", "exactcover", "hittingset",
        "integerlinearprogramming", "intprogramming01", "jobseq", "knapsack", "nqueens", "partition", "primefactor",
        "sat", "sat3", "setcover", "subsetsum", "weightedmax3sat", "weightedmaxsat",
    };

    [Fact]
    public void TheProblemsThatReportMeasures_AreTheCoveredFamilies() {
        var reporting = ProblemProvider.Problems
            .Where(p => !p.Value.IsAbstract && Activator.CreateInstance(p.Value) is IProblem i && i.SizeMeasures().Count > 0)
            .Select(p => p.Key).ToHashSet();
        var covered = GraphProblems().Select(row => row.Data).Concat(OtherReportingProblems).ToHashSet();
        Assert.Subset(reporting, covered); // every covered problem reports its measures
        Assert.Subset(covered, reporting); // and no other problem does yet
    }

    [Fact]
    public void Tsp_ReportsNodesEdgesAndTheBound() {
        var measures = new TSP().SizeMeasures();
        Assert.Equal(
            [SizeMeasure.Count("N", 5), SizeMeasure.Count("E", 10), SizeMeasure.Number("K", 7000)],
            measures);
        Assert.Equal(["|N|", "|E|", "K"], measures.Select(m => m.Label));
    }

    [Fact]
    public void SteinerTree_ReportsItsTerminals() {
        var measures = new STEINERTREE().SizeMeasures().ToDictionary(m => m.Part);
        Assert.Equal(SizeMeasure.Count("R", 3), measures["R"]);
        Assert.Equal(SizeMeasure.Number("K", 6), measures["K"]);
    }

    // The SAT family measures its formula Φ: literal occurrences |phi|, |variables| and |clauses|.
    [Theory]
    [InlineData("sat", "(x1 | x2 | x3 | x4) & (!x1 | !x2) & (!x3 | x4) & (!x4 | x2) & (!x2 | !x3)", 12, 4, 5)]
    [InlineData("sat3", "(x1 | x2 | x3) & (!x1 | !x2 | !x3) & (!x1 | x2 | x3) & (x1 | !x2 | x3)", 12, 3, 4)]
    [InlineData("weightedmaxsat", "(((!x1 | x2) & (x3 | x4 | x1)),(1,2))", 5, 4, 2)]
    [InlineData("weightedmax3sat", "(((!x1 | x2) & (x3 | x3 | x1)),(1,2))", 5, 3, 2)]
    public void FormulaProblem_ReportsItsFormulasSizes(string problem, string instance, int phi, int variables, int clauses) {
        var parsed = (IProblem)Activator.CreateInstance(ProblemProvider.Problems[problem], instance)!;
        Assert.Equal(
            [SizeMeasure.Count("phi", phi), SizeMeasure.Count("variables", variables), SizeMeasure.Count("clauses", clauses)],
            parsed.SizeMeasures());
    }

    [Fact]
    public void SatBruteForce_HopelessFormula_IsRefused_AndWalkSatIsNot() {
        // 60 variables in 3-literal clauses: 2^60 * 180, about 2.1e20 steps.
        string formula = string.Join(" & ", Enumerable.Range(0, 60).Select(i => $"(x{i} | !x{(i + 1) % 60} | x{(i + 7) % 60})"));
        var estimate = ComputePrecheck.Refusal(ProblemProvider.Solvers["satbruteforcesolver"], formula, ThirtySeconds, null);
        Assert.NotNull(estimate);
        Assert.Equal(Math.Pow(2, 60) * 180, estimate.Steps, 1e-9 * estimate.Steps);
        Assert.Null(ComputePrecheck.Refusal(ProblemProvider.Solvers["walksat"], formula, ThirtySeconds, null));
        Assert.Null(ComputePrecheck.Refusal(ProblemProvider.Solvers["schoning"], formula, ThirtySeconds, null));
    }

    // The other families measure the parts their grammar names.
    [Fact]
    public void GrammarProblems_ReportTheirNamedParts() {
        Assert.Equal([SizeMeasure.Count("S", 4), SizeMeasure.Number("T", 28)], new SUBSETSUM().SizeMeasures());
        Assert.Equal([SizeMeasure.Count("U", 5), SizeMeasure.Count("S", 4), SizeMeasure.Number("K", 3)],
            new SETCOVER().SizeMeasures());
        Assert.Equal([SizeMeasure.Count("i", 3), SizeMeasure.Number("w", 50), SizeMeasure.Number("v", 220)],
            new KNAPSACK().SizeMeasures());
        Assert.Equal([SizeMeasure.Count("x", 5), SizeMeasure.Count("y", 3)], new EDITDISTANCE().SizeMeasures());
        var dfa = new DFA().SizeMeasures().ToDictionary(m => m.Part);
        Assert.Equal(SizeMeasure.Count("E", 6), dfa["E"]);
        Assert.Equal(SizeMeasure.Count("I", 3), dfa["I"]);
    }

    [Fact]
    public void SubsetSumBruteForce_HopelessSet_IsRefused_AndTheDpOfKnapsackIsNot() {
        // 60 integers: 60 * 2^60, about 6.9e19 steps.
        string set = "({" + string.Join(",", Enumerable.Range(1, 60)) + "},100000)";
        var estimate = ComputePrecheck.Refusal(ProblemProvider.Solvers["subsetsumbruteforce"], set, ThirtySeconds, null);
        Assert.NotNull(estimate);
        Assert.Equal(60 * Math.Pow(2, 60), estimate.Steps, 1e-9 * estimate.Steps);
        string items = "({" + string.Join(",", Enumerable.Range(1, 60).Select(i => $"({i},{i})")) + "},1000,500)";
        Assert.Null(ComputePrecheck.Refusal(ProblemProvider.Solvers["knapsackdp"], items, ThirtySeconds, null));
        Assert.NotNull(ComputePrecheck.Refusal(ProblemProvider.Solvers["knapsackbruteforce"], items, ThirtySeconds, null));
    }

    [Fact]
    public void SetCoverBruteForce_IsBoundedByTheCoverSize() {
        // 60 subsets: 2^60 would refuse it, but it only tries covers of 1 or 2 subsets.
        string subsets = string.Join(",", Enumerable.Range(1, 60).Select(i => $"{{{i}}}"));
        string universe = string.Join(",", Enumerable.Range(1, 60));
        Assert.Null(ComputePrecheck.Refusal(ProblemProvider.Solvers["setcoverbruteforce"], $"({{{universe}}},{{{subsets}}},2)",
            ThirtySeconds, null));
        Assert.NotNull(ComputePrecheck.Refusal(ProblemProvider.Solvers["setcoverbruteforce"], $"({{{universe}}},{{{subsets}}},30)",
            ThirtySeconds, null));
    }

    [Fact]
    public void ProblemWithoutMeasures_ReportsNone() {
        // The IProblem default: a problem that does not override SizeMeasures reports nothing.
        Assert.Empty(((IProblem)new NODEFAULTSFIXTURE()).SizeMeasures());
    }

    // ── The Big-O names ─────────────────────────────────────────────────────────

    // Every checked solver writes a set part as its count, |N|, never bare N: a bare N would read the same number but
    // say something else (and is what a pre-#843 "n" meant).
    [Fact]
    public void CheckedSolvers_WriteEverySetPartAsItsCount() {
        var failures = new List<string>();
        foreach (var (_, type) in ProblemProvider.Solvers) {
            if (ComputePrecheck.SkipReason(type) != null)
                continue;
            var solver = (ISolver)Activator.CreateInstance(type)!;
            var problem = (IProblem)Activator.CreateInstance(ProblemProvider.ProblemTypeOf(type, typeof(ISolver<>))!)!;
            string function = solver.timeComplexity!.Function;
            foreach (var measure in problem.SizeMeasures().Where(m => m.IsCount)) {
                string bare = Regex.Replace(function, $@"\|{Regex.Escape(measure.Part)}\|", "");
                if (Regex.IsMatch(bare, $@"\b{Regex.Escape(measure.Part)}\b"))
                    failures.Add($"{type.Name}: \"{function}\" uses {measure.Part} outside |{measure.Part}|");
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    // The solvers the precheck leaves to the time limit, because their Big-O uses a name their problem does not report
    // or for another reason SkipReason gives. Delete a solver from the list once the precheck checks it.
    private static readonly string[] Skipped = {
        // Solvers that prune their search (SolverType.Backtracking): their worst-case Big-O says little about how long
        // they run, so the precheck leaves them to the timer.
        "CarraghanPardalos", "DominatingSetForcedVertex", "DominatingSetNaiveInclusionExclusion",
        "TSPBranchAndBound", "VertexCoverBoundedSearchTree",
        "DancingLinks", "ExactCoverRecursive", "HeuristicSolver", "IntegerLinearProgrammingSolver", "NQueensBacktracking",
        "Sat3BacktrackingSolver", "WeightedMax3SatBranchAndBoundSolver", "WeightedMaxSatBranchAndBoundSolver",
        // Quantum solvers: their work runs on the external quantum service, not the server's processor.
        "BernsteinVaziraniQuantumSolver", "DeutschQuantumSolver", "DeutschJozsaQuantumSolver", "MaxCutQAOASolver",
        "SATGroverSolver",
        "ShorsQuantumSolver", "UnstructuredGroverSolver",
        // The oracle problems' classical baselines count oracle queries, and their instance is the oracle's own table, so
        // those problems report no size measures.
        "BernsteinVaziraniClassicalSolver", "DeutschClassicalSolver", "DeutschJozsaClassicalSolver", "SimonSolver",
        "UnstructuredSearchSolver",
        // Big-Os driven by a size the instance does not name.
        "ChibaNishizeki", // a = arboricity, which CLIQUE does not report
        "IntegerLinearProgrammingBruteForceSolver", // U = values per variable, from the constraints' bounds
        "LosslessDataCompressionSolver", // k = distinct characters in S (LOSSLESSDATACOMPRESSION reports no measures)
        "NFASolver", // d = largest out-degree of a state (NFA reports no measures)
        "PumpSchedulingCMSolver", "PumpSchedulingEMSolver", // B = tank buckets, from the capacity inside T (no measures)
        // SUDOKU's grid is always 9 x 9, so its Big-O, n^(n^2) at n = 9, is one constant for every instance and would
        // refuse them all; SUDOKU reports no measures.
        "SudokuSolver",
        "DummySolver", // the placeholder solver of IProblem
    };

    private static Dictionary<string, string> ActualSkipped() =>
        ProblemProvider.Solvers.Values
            .Select(t => (t.Name, Reason: ComputePrecheck.SkipReason(t)))
            .Where(s => s.Reason != null)
            .ToDictionary(s => s.Name, s => s.Reason!, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void NoNewSkippedSolver() {
        var known = new HashSet<string>(Skipped, StringComparer.OrdinalIgnoreCase);
        var unexpected = ActualSkipped().Where(s => !known.Contains(s.Key)).OrderBy(s => s.Key, StringComparer.Ordinal)
            .Select(s => $"{s.Key}: {s.Value}").ToList();
        Assert.True(unexpected.Count == 0,
            "The Big-O precheck skips these solvers (it leaves them to the time limit):\n" + string.Join("\n", unexpected) +
            "\nWrite the solver's timeComplexity in the names its problem reports (IProblem.SizeMeasures, e.g. |N|, |E|, K " +
            "for a graph problem), or, for a problem that reports none yet, add the solver to Skipped in ComputePrecheck_Tests.cs.");
    }

    [Fact]
    public void SkippedListHasNoStaleEntries() {
        var actual = ActualSkipped();
        var stale = Skipped.Where(s => !actual.ContainsKey(s)).ToList();
        Assert.True(stale.Count == 0,
            "The precheck now checks these solvers (or they were renamed or removed); delete them from Skipped in " +
            "ComputePrecheck_Tests.cs: " + string.Join(", ", stale));
    }

    [Fact]
    public void SkipReason_NamesTheMissingMeasure() {
        Assert.Equal("its timeComplexity uses a, which CLIQUE does not report",
            ComputePrecheck.SkipReason(ProblemProvider.Solvers["chibanishizeki"]));
        Assert.Equal("it runs on the external quantum service (SolverType.Quantum), not on the server's processor",
            ComputePrecheck.SkipReason(ProblemProvider.Solvers["satgroversolver"]));
        // Every "reports no size measures" reason names the solver's own problem.
        foreach (var type in ProblemProvider.Solvers.Values)
            if (ComputePrecheck.SkipReason(type) is { } reason && reason.EndsWith(" reports no size measures", StringComparison.Ordinal))
                Assert.Equal($"{ProblemProvider.ProblemTypeOf(type, typeof(ISolver<>))!.Name} reports no size measures", reason);
        Assert.Equal("it prunes its search (SolverType.Backtracking), so its worst-case Big-O says little about its running time",
            ComputePrecheck.SkipReason(typeof(TSPBranchAndBound)));
        Assert.Null(ComputePrecheck.SkipReason(typeof(TSPBruteForce)));
    }

    // ── The estimate ────────────────────────────────────────────────────────────

    [Fact]
    public void Estimate_IsTheBigOAtTheInstancesMeasures() {
        var estimate = ComputePrecheck.Evaluate(typeof(TSPBruteForce), PrecheckInputs.HopelessTsp, ThirtySeconds, null);
        Assert.NotNull(estimate);
        double factorial = Enumerable.Range(1, 15).Aggregate(1.0, (a, b) => a * b);
        Assert.Equal(factorial * 15 * 105, estimate.Steps, 1e-9 * estimate.Steps);
        Assert.Equal([SizeMeasure.Count("N", 15), SizeMeasure.Count("E", 105)], estimate.Measures);
        Assert.Equal(ComputePrecheck.DefaultStepsPerSecond * 30, estimate.StepsInLimit);
        Assert.Equal(ComputePrecheck.DefaultStepsPerSecond * 30 * ComputePrecheck.DefaultMargin, estimate.RefuseAbove);
        Assert.True(estimate.TooLarge);
        Assert.Equal("Traveling Salesperson Brute Force", estimate.Solver);
    }

    [Fact]
    public void SmallInstance_IsNotRefused() {
        var estimate = ComputePrecheck.Evaluate(typeof(TSPBruteForce), new TSP().instance, ThirtySeconds, null);
        Assert.NotNull(estimate);
        Assert.False(estimate.TooLarge);
        Assert.Null(ComputePrecheck.Refusal(typeof(TSPBruteForce), new TSP().instance, ThirtySeconds, null));
    }

    [Fact]
    public void HopelessInstance_IsRefused_AndAFastSolverIsNot() {
        Assert.NotNull(ComputePrecheck.Refusal(typeof(TSPBruteForce), PrecheckInputs.HopelessTsp, ThirtySeconds, null));
        // The greedy solver's |N|^3 * |E| is about 350,000 steps on the same instance.
        Assert.Null(ComputePrecheck.Refusal(typeof(TSPGreedy), PrecheckInputs.HopelessTsp, ThirtySeconds, null));
    }

    [Fact]
    public void ShorterLimit_RefusesSooner() {
        // TSP brute force on 12 nodes: 12! * 12 * 66, about 3.8e11 steps: run within 30 seconds, refused within 0.01.
        string twelve = PrecheckInputs.CompleteTsp(12);
        Assert.Null(ComputePrecheck.Refusal(typeof(TSPBruteForce), twelve, ThirtySeconds, null));
        Assert.NotNull(ComputePrecheck.Refusal(typeof(TSPBruteForce), twelve, TimeSpan.FromMilliseconds(10), null));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void MarginOfZeroOrLess_TurnsThePrecheckOff(string margin) {
        var config = Config((ComputePrecheck.MarginKey, margin));
        Assert.Null(ComputePrecheck.Margin(config));
        Assert.Null(ComputePrecheck.Evaluate(typeof(TSPBruteForce), PrecheckInputs.HopelessTsp, ThirtySeconds, config));
    }

    [Fact]
    public void StepsPerSecondOfZero_TurnsThePrecheckOff() {
        var config = Config((ComputePrecheck.StepsPerSecondKey, "0"));
        Assert.Null(ComputePrecheck.Refusal(typeof(TSPBruteForce), PrecheckInputs.HopelessTsp, ThirtySeconds, config));
    }

    [Fact]
    public void ConfiguredRateAndMargin_SetTheBudget() {
        var config = Config((ComputePrecheck.StepsPerSecondKey, "1000"), (ComputePrecheck.MarginKey, "2"));
        var estimate = ComputePrecheck.Evaluate(typeof(TSPBruteForce), new TSP().instance, TimeSpan.FromSeconds(3), config);
        Assert.NotNull(estimate);
        Assert.Equal(3000, estimate.StepsInLimit);
        Assert.Equal(6000, estimate.RefuseAbove);
        // The default instance: 5! * 5 * 10 = 6,000 steps, not over the line.
        Assert.Equal(6000, estimate.Steps);
        Assert.False(estimate.TooLarge);
    }

    [Fact]
    public void NoTimeLimit_NoPrecheck() {
        Assert.Null(ComputePrecheck.Evaluate(typeof(TSPBruteForce), PrecheckInputs.HopelessTsp, null, null));
    }

    [Fact]
    public void UnparsableInstance_IsLeftToTheSolve() {
        Assert.Null(ComputePrecheck.Evaluate(typeof(TSPBruteForce), "not an instance", ThirtySeconds, null));
    }

    [Fact]
    public void SkippedSolver_HasNoEstimate() {
        var grover = ProblemProvider.Solvers["satgroversolver"];
        Assert.Null(ComputePrecheck.Evaluate(grover, "(x1 | x2 | x3)", ThirtySeconds, null));
    }

    [Fact]
    public void HugeSize_IsEvaluatedAtOnce() {
        // 300 nodes: 300! is past double's range, so the estimate is infinite, and evaluating it does not loop.
        var sw = Stopwatch.StartNew();
        var estimate = ComputePrecheck.Evaluate(typeof(TSPBruteForce), PrecheckInputs.PathTsp(300), ThirtySeconds, null);
        Assert.NotNull(estimate);
        Assert.True(double.IsPositiveInfinity(estimate.Steps));
        Assert.True(estimate.TooLarge);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"took {sw.Elapsed}");
    }

    [Fact]
    public void InstancePastTheLengthCap_IsNotParsed_AndIsLeftToTheTimer() {
        // A complete graph on 400 nodes is about 1.6 million characters, which takes minutes to parse.
        string huge = PrecheckInputs.CompleteTsp(400);
        Assert.True(huge.Length > ComputePrecheck.MaxInstanceLength);
        var sw = Stopwatch.StartNew();
        Assert.Null(ComputePrecheck.Evaluate(typeof(TSPBruteForce), huge, ThirtySeconds, null));
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1), $"took {sw.Elapsed}");
    }

    // ── The too_large body ──────────────────────────────────────────────────────

    [Fact]
    public void TooLargeBody_FollowsTheErrorContract() {
        var estimate = ComputePrecheck.Evaluate(typeof(TSPBruteForce), PrecheckInputs.HopelessTsp, ThirtySeconds, null)!;
        var body = (Dictionary<string, object?>)ComputePrecheck.TooLargeBody("solve", estimate, "Try a smaller instance.",
            new Dictionary<string, object?> { ["solver"] = "TSPBruteForce" });
        Assert.Equal(
            ["error", "message", "detail", "limitSeconds", "estimatedSteps", "stepsInLimit", "refuseAboveSteps",
             "timeComplexity", "measures", "solver", "hint"],
            body.Keys);
        Assert.Equal("too_large", body["error"]);
        Assert.Equal(
            "The Traveling Salesperson Brute Force solver would need about 2.1 × 10^15 steps for this instance, but only " +
            $"about {ComputePrecheck.Approximately(ComputePrecheck.DefaultStepsPerSecond * 30)} fit in the 30-second time " +
            "limit, so the solve was not started.",
            body["message"]);
        Assert.StartsWith("The solver's declared running time, O(|N|! * |N| * |E|), comes to about 2.1 × 10^15 steps at |N| = 15, |E| = 105.",
            (string)body["detail"]!);
        Assert.Equal(30.0, body["limitSeconds"]);
        Assert.Equal("|N|! * |N| * |E|", body["timeComplexity"]);
        Assert.Equal(new Dictionary<string, double> { ["|N|"] = 15, ["|E|"] = 105 }, body["measures"]);
        Assert.Equal("Try a smaller instance.", body["hint"]);
    }

    [Fact]
    public void TooLargeBody_SendsAnInfiniteEstimateAsNull() {
        var estimate = ComputePrecheck.Evaluate(typeof(TSPBruteForce), PrecheckInputs.PathTsp(300), ThirtySeconds, null)!;
        var body = (Dictionary<string, object?>)ComputePrecheck.TooLargeBody("solve", estimate, "hint");
        Assert.Null(body["estimatedSteps"]);
        Assert.Contains("more than 10^308 steps", (string)body["message"]!);
        _ = System.Text.Json.JsonSerializer.Serialize(body); // JSON has no infinity: this must not throw
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(6000, "6,000")]
    [InlineData(999_999, "999,999")]
    [InlineData(1e6, "10^6")]
    [InlineData(3e11, "3 × 10^11")]
    [InlineData(2.0923e15, "2.1 × 10^15")]
    [InlineData(9.96e9, "10^10")]
    [InlineData(double.PositiveInfinity, "more than 10^308")]
    public void Approximately_ReadsAsWords(double steps, string expected) {
        Assert.Equal(expected, ComputePrecheck.Approximately(steps));
    }

    // ── MathematicalFunction: what the graph Big-Os need ───────────────────────

    [Fact]
    public void Cardinality_ReadsTheCountOfThePart() {
        var f = new MathematicalFunction("|N|! * |N| * |E|");
        Assert.Equal(["N", "E"], f.VariableNames);
        Assert.Equal(6 * 3 * 3, f.Evaluate(("N", 3), ("E", 3)));
    }

    [Fact]
    public void BareLog_TakesACardinality() {
        var f = new MathematicalFunction("|N| * |E| log |E|");
        Assert.Equal(["N", "E"], f.VariableNames);
        Assert.Equal(4 * 8 * Math.Log(8), f.Evaluate(("N", 4), ("E", 8)), 1e-9);
    }

    [Fact]
    public void MinAndMax_AreTheSmallerAndLarger() {
        var f = new MathematicalFunction("(min(K, |N|)+1)^|N| + max(K, |N|)");
        Assert.Equal(["K", "N"], f.VariableNames);
        Assert.Equal(Math.Pow(4, 5) + 5, f.Evaluate(("K", 3), ("N", 5)));
        Assert.Equal(Math.Pow(6, 5) + 9, f.Evaluate(("K", 9), ("N", 5)));
    }

    [Fact]
    public void HugeFactorial_IsInfiniteAtOnce() {
        var sw = Stopwatch.StartNew();
        Assert.True(double.IsPositiveInfinity(new MathematicalFunction("n!").Evaluate(("n", 1e12))));
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"took {sw.Elapsed}");
        Assert.Equal(120, new MathematicalFunction("n!").Evaluate(("n", 5)));
    }
}
