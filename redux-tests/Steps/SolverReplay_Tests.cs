using API.Interfaces;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Replay test over every solver that records typed steps (implements ISolver<T, TPartial>), found by
// reflection over the API assembly so converted solvers are picked up automatically. Each is run on its
// problem's default instance.
//
// Note: a randomized solver must take a seed (or otherwise be deterministic) for the answer-equality
// check (a) to hold across runs. A solver with a public constructor taking one int is built with a fixed seed here
// (see CreateSolver); the only random ones so far are the two vertex cover 2-approximations.
public class SolverReplay_Tests {
    private static Type? TypedInterface(Type solver) =>
        solver.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<,>));

    public static TheoryData<string> TypedSolverNames =>
        new(ProblemProvider.Solvers.Where(kv => TypedInterface(kv.Value) != null).Select(kv => kv.Key).OrderBy(n => n));

    private const int Seed = 12345;

    // The solver as the replay should run it: seeded if it can be, so repeated runs make the same random choices.
    private static ISolver CreateSolver(Type solverType) {
        var seeded = solverType.GetConstructor([typeof(int)]);
        return (ISolver)(seeded != null ? seeded.Invoke([Seed]) : Activator.CreateInstance(solverType)!);
    }

    // The certificate a verifier expects, taken from a solver's answer string. Path-style answers
    // ("The sequence of states to accept is: 1, 2") carry the certificate after the colon; a solver whose
    // answer is already a certificate needs no mapping.
    private static string CertificateFrom(string answer) {
        string firstLine = answer.Split('\n')[0].Trim();
        if (firstLine.StartsWith("No Solution")) return "";
        const string prefix = "The sequence of states to accept is:";
        return firstLine.StartsWith(prefix) ? firstLine[prefix.Length..].Replace(" ", "") : answer;
    }

    // The sets of a problem whose answers pick sets by their elements, in instance order.
    private static List<List<string>> SetsOf(IProblem problem) => problem switch {
        API.Problems.NPComplete.NPC_SETCOVER.SETCOVER setCover => setCover.subsets,
        API.Problems.NPComplete.NPC_EXACTCOVER.EXACTCOVER exactCover => exactCover.S,
        _ => throw new NotImplementedException($"Add the sets of {problem.GetType().Name} to SolverReplay_Tests.SetsOf."),
    };

    // What the final Partial must equal, derived independently from the answer string.
    private static object ExpectedFinalPartial(object lastPartial, string answer, string solverName, IProblem problem) {
        if (LayeredFinalPartial(lastPartial, answer, solverName, problem) is { } layered) return layered;
        if (lastPartial is ChosenSets) {
            // Set answers list the chosen sets by their elements ("{{1,2,3},{4,5}}"); the partial names them S1, S2, ...
            // by position. Each listed set is the first set of the instance with those elements not taken yet.
            var sets = SetsOf(problem);
            var taken = new bool[sets.Count];
            var ids = new List<string>();
            foreach (SPADE.UtilCollection listed in new SPADE.UtilCollection(answer).ToList()) {
                var elements = listed.ToList().Select(x => x.ToString()).ToHashSet();
                int i = Enumerable.Range(0, sets.Count).First(j => !taken[j] && sets[j].ToHashSet().SetEquals(elements));
                taken[i] = true;
                ids.Add($"S{i + 1}");
            }
            return new ChosenSets(ids);
        }
        if (lastPartial is ActiveStates last) {
            // The end states of the accepting run(s): the last state of each path line.
            var ends = answer.Split('\n')
                .Where(l => l.StartsWith("The sequence of states to accept is:"))
                .Select(l => l.Split(':')[1].Split(',').Last().Trim());
            return new ActiveStates(ends, last.Position);
        }
        if (lastPartial is NodeSet) {
            // Node-set answers are "{a,b,c}"; "{}" is no answer. Order does not matter (NodeSet sorts).
            return new NodeSet(answer.Replace("{", "").Replace("}", "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0));
        }
        if (lastPartial is EdgeSet) {
            // Edge-set answers list edges, plain ({{a,b},...}) or weighted ({({a,b},w),...}); the partial holds their ids.
            return API.Interfaces.Graphs.GraphEdgeFrames.ParseEdges(answer);
        }
        if (lastPartial is Groups && IsGroupsAnswer(solverName)) {
            // Graph Coloring and Clique Cover answer with their classes ({{a,d},{b}}): the n-th class is group n.
            return API.Interfaces.Graphs.GraphGroupFrames.ParseClasses(answer);
        }
        if (lastPartial is Groups) {
            // A cut is not recoverable from the answer string (a list of cut edges names no side), so it is checked
            // against the answer, per solver, in EdgeSetSolvers_Tests.TheDoneStep_ShowsTheAnswerTheSolverGave.
            return lastPartial;
        }
        if (lastPartial is Tour) {
            // Tour answers are "{a,b,c,a}": the order written, the closing repeat of the first node included. "{}" is no answer.
            return API.Interfaces.Graphs.GraphTourFrames.ParseTour(answer);
        }
        if (lastPartial is Assignment) {
            // SAT answers are "(x1:True,x2:False)"; "No Solution" is no answer. Order does not matter (Assignment sorts).
            return API.Interfaces.Graphs.GroupedGraphFrames.ParseAssignment(answer);
        }
        if (lastPartial is NetworkFlow flow) {
            // Minimum S-T Cut answers its S side ("{1,2}"); the flow itself is checked in FlowSolvers_Tests.
            return new NetworkFlow(flow.Flow, flow.Path, flow.Value, answer.Trim('{', '}').Split(',').Where(x => x.Length > 0));
        }
        throw new NotImplementedException(
            $"Add an expected final Partial for {lastPartial.GetType().Name} to SolverReplay_Tests.ExpectedFinalPartial.");
    }

    // The solvers whose answer lists the groups themselves (the groups shape), as opposed to the cut solvers, whose answer
    // lists the crossing edges.
    private static bool IsGroupsAnswer(string solverName) => solverName.StartsWith("graphcoloring") || solverName.StartsWith("cliquecover");

    // The Layered Graph solvers (#667), read with the picture's own answer readers. Null for any other solver.
    private static object? LayeredFinalPartial(object lastPartial, string answer, string solverName, IProblem problem) {
        switch (problem) {
            case API.Problems.NPComplete.NPC_TOPOLOGICALSORT.TOPOLOGICALSORT topo:
                return API.Interfaces.Graphs.LayeredFrames.ParseAnswer(
                    API.Problems.NPComplete.NPC_TOPOLOGICALSORT.Visualizations.TopologicalSortDefaultVisualization.Spec(topo), answer);
            case API.Problems.NPComplete.NPC_ARCSET.ARCSET:
                // Edges are named by the picture's directed edge ids ("2>3").
                return API.Interfaces.Graphs.LayeredFrames.ParseArcs(answer);
            case API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS.STRONGLYCONNECTEDCOMPONENTS: {
                    // Components are numbered in the order they are found, the answer lists them sorted: compare the split.
                    var done = (Components)lastPartial;
                    var parsed = API.Interfaces.Graphs.LayeredFrames.ParseComponents(answer);
                    static HashSet<string> Split(IReadOnlyDictionary<string, int> of) =>
                        of.GroupBy(p => p.Value).Select(g => string.Join(",", g.Select(p => p.Key).Order(StringComparer.Ordinal))).ToHashSet();
                    Assert.True(Split(parsed.Of).SetEquals(Split(done.Of)));
                    return lastPartial;
                }
            case API.Problems.P.P_SSSP.SSSP sssp:
                return API.Interfaces.Graphs.LayeredFrames.ParseAnswer(API.Problems.P.P_SSSP.Visualizations.SSSPVisualization.Spec(sssp), answer);
            case API.Problems.P.P_SPSP.SPSP spsp: {
                    // Dijkstra stops at the target, having settled other nodes too: the path it leads back along is the answer.
                    var done = (Distances)lastPartial;
                    var path = new List<string>();
                    for (string? x = spsp.targetNode; x != null; x = done.Pred.TryGetValue(x, out var u) ? u : null) path.Insert(0, x);
                    Assert.Equal("{" + string.Join(",", path) + "}", answer);
                    return lastPartial;
                }
            default:
                return null;
        }
    }

    [Fact]
    public void AtLeastDfaAndNfaAreDiscovered() {
        var names = TypedSolverNames.Select(d => d.Data).ToList();
        Assert.Contains("dfasolver", names);
        Assert.Contains("nfasolver", names);
    }

    [Theory]
    [MemberData(nameof(TypedSolverNames))]
    public void Replay_OnDefaultInstance_AgreesWithSolveAndVerifier(string name) {
        Type solverType = ProblemProvider.Solvers[name];
        Type problemType = TypedInterface(solverType)!.GetGenericArguments()[0];
        var problem = (IProblem)Activator.CreateInstance(problemType)!;
        string instance = problem.defaultInstance;
        var solver = CreateSolver(solverType);

        var withSteps = solver.Run(instance, withSteps: true);
        var withoutSteps = solver.Run(instance, withSteps: false);

        // (a) The answer does not depend on recording, and matches plain solve().
        Assert.Equal(solver.solve(instance), withSteps.Answer);
        Assert.Equal(withSteps.Answer, withoutSteps.Answer);
        Assert.Empty(withoutSteps.Steps);

        // The run reports the shape of its steps, and every step has that shape.
        Type shape = TypedInterface(solverType)!.GetGenericArguments()[1];
        Assert.Equal(shape, withSteps.StepShape);
        Assert.NotEmpty(withSteps.Steps);
        Assert.All(withSteps.Steps, s => Assert.IsType(typeof(SolverStep<>).MakeGenericType(shape), s));

        // (b) The last step is Done, and its Partial is the answer's shape.
        dynamic last = withSteps.Steps[^1];
        Assert.Equal(StepEvent.Done, (StepEvent)last.Event);
        Assert.Equal(1, withSteps.Steps.Count(s => ((dynamic)s).Event == StepEvent.Done));
        object finalPartial = last.Partial;
        Assert.Equal(ExpectedFinalPartial(finalPartial, withSteps.Answer, name, problem), finalPartial);

        // (c) Done.Ok agrees with the problem's own verifier on the returned answer.
        bool? ok = last.Ok;
        Assert.NotNull(ok);
        // "{}" is how the node-set solvers say "no answer"; the verifiers reject it or refuse to parse it, so it
        // counts as not verified without asking them (a vertex cover of size 3 may simply not exist, as with
        // the 2-approximation on the default Vertex Cover instance).
        bool noAnswer = finalPartial is NodeSet { Nodes.Length: 0 } or Groups { Of.Count: 0 } or Tour { Order.Length: 0 } or Assignment { Values.Count: 0 };
        bool verified = !noAnswer && problem.defaultVerifier.verify(instance, CertificateFrom(withSteps.Answer));
        Assert.Equal(verified, ok);
    }

    [Fact]
    public void GetSteps_OnConvertedSolver_StillReturnsTypedSteps() {
        var steps = ((ISolver)new API.Problems.P.P_DFA.Solvers.DFASolver()).GetSteps(new API.Problems.P.P_DFA.DFA().defaultInstance);
        Assert.NotEmpty(steps);
        Assert.IsType<SolverStep<ActiveStates>>(steps[0]);
    }

    [Fact]
    public void LegacySolver_RunKeepsUntypedSteps() {
        // A solver that has not been converted reports no shape and keeps its untyped steps (SSSP was the example until #667 typed it).
        var pumps = ProblemProvider.Solvers.Values.First(t => TypedInterface(t) == null && typeof(ISolver).IsAssignableFrom(t) && t.Name == "PumpSchedulingCMSolver");
        var legacy = (ISolver)Activator.CreateInstance(pumps)!;
        var problem = (IProblem)Activator.CreateInstance(
            pumps.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>)).GetGenericArguments()[0])!;
        var run = legacy.Run(problem.defaultInstance, true);
        Assert.Null(run.StepShape);
        Assert.NotEmpty(run.Steps);
    }

    [Fact]
    public void StepsFor_DropsStepsOnShapeMismatch_KeepsThemOnMatch() {
        object[] steps = [new SolverStep<ActiveStates>(new ActiveStates(["1"]), StepEvent.Done, [], "x") { Ok = true }];
        var typed = new SolveRun("a", steps, typeof(ActiveStates));
        var legacy = new SolveRun("a", ["legacy"], null);

        Assert.Single(typed.StepsFor(typeof(ActiveStates)));
        Assert.Empty(typed.StepsFor(typeof(NodeSet)));   // different typed shape
        Assert.Empty(typed.StepsFor(null));              // typed steps, legacy visualization
        Assert.Empty(legacy.StepsFor(typeof(ActiveStates))); // legacy steps, typed visualization
        Assert.Single(legacy.StepsFor(null));            // legacy with legacy keeps working
    }
}
