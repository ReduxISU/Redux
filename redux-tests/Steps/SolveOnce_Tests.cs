using System.Reflection;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.JSON_Objects.Tables;
using API.Interfaces.Steps;
using API.Problems.P.P_NFA;
using API.Problems.P.P_NFA.Solvers;
using API.Problems.P.P_NFA.Visualizations;
using API.Problems.P.P_SPSP;
using API.Problems.P.P_SPSP.Solvers;
using API.Problems.P.P_SSSP;
using API.Problems.P.P_SSSP.Solvers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The visualize endpoint runs the chosen solver once: the answer and every frame come from the same run (#734).
// SolveProbe counts each pass over a solver's algorithm (its typed Solve, and the older separate traversals the
// pictures used to make), so a picture that solves again shows up as a second count.
public class SolveOnce_Tests {
    private static readonly JsonSerializerOptions Json = new() { IncludeFields = true };

    private static string Serialize(object value) => JsonSerializer.Serialize(value, Json);

    private static string DefaultInstanceOf(string visualization) {
        Type visType = ProblemProvider.Visualizers[visualization];
        Type problemType = ProblemProvider.ProblemTypeOf(visType, typeof(IVisualization<>))!;
        return ((IProblem)Activator.CreateInstance(problemType)!).defaultInstance;
    }

    // Every picture whose solver records typed steps, with that solver.
    public static TheoryData<string, string, string> Pictures {
        get {
            var data = new TheoryData<string, string, string>();
            (string Vis, string Solver)[] pictures = {
                ("dfavisualization", nameof(API.Problems.P.P_DFA.Solvers.DFASolver)),
                ("dfatablevisualization", nameof(API.Problems.P.P_DFA.Solvers.DFASolver)),
                ("nfavisualization", nameof(NFASolver)),
                ("nfatablevisualization", nameof(NFASolver)),
                ("ssspvisualization", nameof(SSSPSolver)),
                ("sssptablevisualization", nameof(SSSPSolver)),
                ("spspvisualization", nameof(SPSPSolver)),
                ("spsptablevisualization", nameof(SPSPSolver)),
            };
            foreach (var (vis, solver) in pictures)
                foreach (var format in new[] { "list", "frames" })
                    data.Add(vis, solver, format);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Pictures))]
    public async Task Visualize_RunsTheSolverOnce(string visualization, string solver, string format) {
        string instance = DefaultInstanceOf(visualization);
        var counts = SolveProbe.Start();
        try {
            var result = await new ProblemProvider().visualize(visualization, instance, solver: null, format: format);
            Assert.IsType<ContentResult>(result);
        } finally {
            SolveProbe.Stop();
        }
        var only = Assert.Single(counts);
        Assert.Equal(solver, only.Key.Name);
        Assert.Equal(1, only.Value);
    }

    [Fact]
    public void SolveProbe_CountsEverySolveWhileStarted() {
        var counts = SolveProbe.Start();
        try {
            var solver = new SSSPSolver();
            solver.solve(new SSSP());
            solver.GetTableSteps(new SSSP());
        } finally {
            SolveProbe.Stop();
        }
        Assert.Equal(2, counts[typeof(SSSPSolver)]);
    }

    // ISolver.Run's default, for a solver without typed steps, calls GetSteps and then solve: two runs whenever
    // GetSteps does real work. So an untyped solver in the API must not have its own GetSteps, except these, whose
    // GetSteps returns a marker step without solving. (Test doubles in this assembly are left out.)
    private static readonly HashSet<string> MarkerGetSteps = new() { "PumpSchedulingCMSolver", "PumpSchedulingEMSolver" };

    [Fact]
    public void UntypedSolvers_HaveNoStepsOfTheirOwn() {
        var offenders = ProblemProvider.Solvers.Values
            .Where(t => t.Assembly == typeof(ISolver).Assembly)
            .Where(t => !t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<,>)))
            .Where(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Any(m => m.Name == "GetSteps" || m.Name.EndsWith(".GetSteps", StringComparison.Ordinal)))
            .Select(t => t.Name)
            .Where(name => !MarkerGetSteps.Contains(name))
            .ToList();
        Assert.Empty(offenders);
    }

    // ----- The frames made from the typed run are the frames the second run used to make ----- //

    private static List<string> RandomGraphInstances(int seed, bool withTarget) {
        var rnd = new Random(seed);
        var instances = new List<string>();
        for (int t = 0; t < 60; t++) {
            int n = rnd.Next(1, 9);
            var nodes = Enumerable.Range(1, n).Select(x => x.ToString()).ToList();
            int kind = rnd.Next(4);
            var edges = new List<string>();
            int m = rnd.Next(0, n * 2 + 1);
            for (int e = 0; e < m; e++) {
                string a = nodes[rnd.Next(n)], b = nodes[rnd.Next(n)];
                if (a == b) continue;
                int w = rnd.Next(1, 10);
                edges.Add(kind switch {
                    0 => $"(({a},{b}),{w})",
                    1 => $"({{{a},{b}}},{w})",
                    2 => $"({a},{b})",
                    _ => $"{{{a},{b}}}",
                });
            }
            string graph = $"{{{string.Join(",", nodes)}}},{{{string.Join(",", edges.Distinct())}}}";
            string source = nodes[rnd.Next(n)];
            instances.Add(withTarget ? $"({graph},{source},{nodes[rnd.Next(n)]})" : $"({graph},{source})");
        }
        return instances;
    }

    public static TheoryData<string> SsspInstances => new(RandomGraphInstances(734, withTarget: false));
    public static TheoryData<string> SpspInstances => new(RandomGraphInstances(735, withTarget: true));

    [Theory]
    [MemberData(nameof(SsspInstances))]
    public void Sssp_FramesFromTypedRun_MatchTheSecondRun(string instance) {
        var problem = new SSSP(instance);
        var solver = new SSSPSolver();
        var steps = ((ISolver)solver).Run(instance, withSteps: true).Steps;
        Assert.Equal(Serialize(solver.LegacyGraphSteps(problem)), Serialize(SSSPSolver.GraphSteps(problem, steps).Cast<object>().ToList()));
        Assert.Equal(Serialize(solver.GetTableSteps(problem)), Serialize(SSSPSolver.TableSteps(problem, steps).Cast<object>().ToList()));
    }

    [Theory]
    [MemberData(nameof(SpspInstances))]
    public void Spsp_FramesFromTypedRun_MatchTheSecondRun(string instance) {
        var problem = new SPSP(instance);
        var solver = new SPSPSolver();
        var steps = ((ISolver)solver).Run(instance, withSteps: true).Steps;
        Assert.Equal(Serialize(solver.LegacyPathSteps(problem)), Serialize(SPSPSolver.PathSteps(problem, steps).Cast<object>().ToList()));
        Assert.Equal(Serialize(solver.GetTableSteps(problem)), Serialize(SPSPSolver.TableSteps(problem, steps).Cast<object>().ToList()));
    }

    private static List<string> RandomNfaInstances(int seed) {
        var rnd = new Random(seed);
        var instances = new List<string>();
        for (int t = 0; t < 60; t++) {
            int n = rnd.Next(1, 5);
            var states = Enumerable.Range(1, n).Select(x => $"q{x}").ToList();
            var edges = new List<string>();
            int m = rnd.Next(0, n * 3 + 1);
            for (int e = 0; e < m; e++) {
                string symbol = rnd.Next(4) == 0 ? "ε" : (rnd.Next(2) == 0 ? "a" : "b");
                edges.Add($"({states[rnd.Next(n)]},{symbol},{states[rnd.Next(n)]})");
            }
            var accept = states.Where(_ => rnd.Next(2) == 0);
            int length = rnd.Next(0, 4);
            string input = length == 0 ? "ε" : new string(Enumerable.Range(0, length).Select(_ => rnd.Next(2) == 0 ? 'a' : 'b').ToArray());
            instances.Add($"(({{{string.Join(",", states)}}},{{a,b}},{{{string.Join(",", edges.Distinct())}}},{states[0]},{{{string.Join(",", accept)}}}),{input})");
        }
        return instances;
    }

    public static TheoryData<string> NfaInstances => new(RandomNfaInstances(736));

    [Theory]
    [MemberData(nameof(NfaInstances))]
    public void Nfa_RunTablesFromTypedRun_MatchTheSecondRun(string instance) {
        var problem = new NFA(instance);
        var solver = new NFASolver();
        var steps = ((ISolver)solver).Run(instance, withSteps: true).Steps;
        var (tables, notShown) = NFASolver.TableStepsFrom(problem, steps);
        if (notShown > 0)
            return; // a run too long for the recorder's cap; the next test covers that case
        Assert.Equal(Serialize(solver.GetTableSteps(problem)), Serialize(tables.Cast<object>().ToList()));
    }

    [Fact]
    public void Nfa_RunTable_WhenStepsWereNotKept_ListsTheRunsFoundAndSaysSo() {
        // Many runs: every state can move to every state on a and on b.
        string instance = "(({1,2,3},{a,b},{(1,a,1),(1,a,2),(1,a,3),(2,a,1),(2,a,2),(2,a,3),(3,a,1),(3,a,2),(3,a,3),(1,b,1),(2,b,2),(3,b,3)},1,{3}),aaab)";
        var problem = new NFA(instance);
        var solver = new NFASolver();
        var rec = new StepRecorder<ActiveStates>(cap: 10);
        solver.Solve(problem, rec);
        var steps = rec.Steps.Cast<object>().ToList();

        var (tables, notShown) = NFASolver.TableStepsFrom(problem, steps);
        Assert.Equal(rec.Hidden, notShown);
        Assert.True(notShown > 0);
        // Every run listed is one of the runs the full search finds.
        var allRuns = solver.GetPathRuns(problem).Select(Serialize).ToHashSet();
        Assert.All(NFASolver.RunsFromSteps(problem, steps).Runs, run => Assert.Contains(Serialize(run), allRuns));
        Assert.True(tables.Count < allRuns.Count);

        var frames = new NFATableVisualization().StepsVisualization(problem, steps);
        var last = Assert.IsType<API_TableJSON>(frames[^1]);
        Assert.Equal(NFATableVisualization.NotAllRunsTitle(notShown), last.title);
        Assert.Empty(last.rows);
    }

    [Fact]
    public void StepCaptions_ReadsTheNotShownNote() {
        Assert.Equal(0, StepCaptions.NotShown("Accepted."));
        Assert.Equal(12, StepCaptions.NotShown("Accepted." + StepCaptions.NotShownSuffix(12)));
    }
}
