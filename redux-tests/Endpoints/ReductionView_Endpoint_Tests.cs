using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Reduction View (#692) on the actual /ProblemProvider/reductionView output: the source and target pictures, the gadget map
// that must line up with them, and the three phases. Every test reads the real endpoint response, never a hand-built object.
public class ReductionView_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public ReductionView_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private const string Sat = "SipserReduceToCliqueStandard", Vc = "sipserReductionVertexCover", Is = "reduceToCLIQUE";

    // What each reduction draws: its source picture type, its target picture type, and the default solver of the target.
    private static readonly Dictionary<string, (string SourceType, string TargetType)> Pictures = new(StringComparer.OrdinalIgnoreCase) {
        [Sat] = ("GroupedGraph", "Graph"),
        [Vc] = ("Graph", "Graph"),
        [Is] = ("Graph", "Graph"),
    };

    // The same instances the /reduce and /gadgets snapshots pin: each reduction's default instance, and ones with no answer
    // (a formula that cannot be satisfied, a clique or independent set too big to exist).
    public static TheoryData<string, string> Cases() {
        var data = new TheoryData<string, string>();
        foreach (var (reduction, instance) in Reduction_Output_Snapshot_Tests.Cases()) data.Add(reduction, instance);
        return data;
    }

    private static IReduction Build(string reduction, string instance) =>
        (IReduction)Activator.CreateInstance(ProblemProvider.Reductions[reduction.ToLower()], instance)!;

    private async Task<(HttpStatusCode Status, string Body)> Post(string reduction, string instance, string? solver = null) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        string url = $"/ProblemProvider/reductionView?reduction={reduction}" + (solver == null ? "" : $"&solver={solver}");
        var response = await _client.PostAsync(url, content, TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task<JsonElement> View(string reduction, string instance, string? solver = null) {
        var (status, body) = await Post(reduction, instance, solver);
        Assert.True(status == HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement;
    }

    private static List<JsonElement> Phase(JsonElement view, string phase) =>
        view.GetProperty("frames").EnumerateArray().Where(f => f.GetProperty("phase").GetString() == phase).ToList();

    // Every id the picture payload names: nodes, groups (clauses), variables, and both edge lists.
    private static HashSet<string> PictureIds(JsonElement payload) {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (string list in new[] { "nodes", "groups", "variables", "edges", "clauseEdges" })
            if (payload.TryGetProperty(list, out var items))
                foreach (var item in items.EnumerateArray()) ids.Add(item.GetProperty("id").GetString()!);
        return ids;
    }

    // The problem's own verifier; a certificate it cannot read (such as "{}") is not correct.
    private static bool Verifies(IProblem problem, string instance, string answer) {
        try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
    }

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()!).ToArray();

    // ---- Shape ----

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Shape_HasBothPicturesTheGadgetsAndFrames(string reduction, string instance) {
        var view = await View(reduction, instance);
        var (sourceType, targetType) = Pictures[reduction];

        Assert.Equal("Reduction", view.GetProperty("type").GetString());
        Assert.Equal("reduction", view.GetProperty("kind").GetString());
        Assert.Equal(reduction, view.GetProperty("reduction").GetString(), ignoreCase: true);
        IReduction red = Build(reduction, instance);
        Assert.Equal(red.reductionName, view.GetProperty("reductionName").GetString());

        var source = view.GetProperty("source");
        Assert.Equal(red.reductionFrom.problemName, source.GetProperty("problem").GetString());
        Assert.Equal(instance, source.GetProperty("instance").GetString());
        Assert.Equal(sourceType, source.GetProperty("picture").GetProperty("type").GetString());
        Assert.Equal(sourceType == "Graph" ? "graph" : "groupedGraph", source.GetProperty("picture").GetProperty("payload").GetProperty("kind").GetString());

        var target = view.GetProperty("target");
        Assert.Equal(red.reductionTo.problemName, target.GetProperty("problem").GetString());
        Assert.Equal(red.reductionTo.instance, target.GetProperty("instance").GetString());
        Assert.Equal(targetType, target.GetProperty("picture").GetProperty("type").GetString());
        Assert.Equal("graph", target.GetProperty("picture").GetProperty("payload").GetProperty("kind").GetString());

        var gadgets = view.GetProperty("gadgets").EnumerateArray().ToList();
        Assert.Equal(red.gadgets.Count, gadgets.Count);
        Assert.All(gadgets, g => {
            Assert.True(Enum.TryParse<API.Interfaces.JSON_Objects.GadgetKind>(g.GetProperty("gadgetKind").GetString(), true, out _));
            Assert.NotEmpty(g.GetProperty("sourceIds").EnumerateArray());
            Assert.NotEmpty(g.GetProperty("targetIds").EnumerateArray());
            // The legacy fields stay in /gadgets; the view's gadgets carry only the new ones.
            Assert.False(g.TryGetProperty("color", out _));
        });

        Assert.All(view.GetProperty("frames").EnumerateArray(), f => {
            Assert.Contains(f.GetProperty("phase").GetString(), new[] { "build", "solve", "map" });
            Assert.False(string.IsNullOrEmpty(f.GetProperty("caption").GetString()));
            Assert.Equal(JsonValueKind.Array, f.GetProperty("sourceFocus").ValueKind);
            Assert.Equal(JsonValueKind.Array, f.GetProperty("targetFocus").ValueKind);
            Assert.Equal(JsonValueKind.Object, f.GetProperty("source").ValueKind);
            Assert.Equal(JsonValueKind.Object, f.GetProperty("target").ValueKind);
        });
    }

    // ---- The key cross-pane contract ----

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task EveryGadgetId_IsAnIdOfItsPanesPicture(string reduction, string instance) {
        var view = await View(reduction, instance);
        var sourceIds = PictureIds(view.GetProperty("source").GetProperty("picture").GetProperty("payload"));
        var targetIds = PictureIds(view.GetProperty("target").GetProperty("picture").GetProperty("payload"));
        foreach (var g in view.GetProperty("gadgets").EnumerateArray()) {
            Assert.All(Strings(g.GetProperty("sourceIds")), id => Assert.Contains(id, sourceIds));
            Assert.All(Strings(g.GetProperty("targetIds")), id => Assert.Contains(id, targetIds));
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task EveryFocusId_IsAnIdOfItsPanesPicture(string reduction, string instance) {
        var view = await View(reduction, instance);
        var sourceIds = PictureIds(view.GetProperty("source").GetProperty("picture").GetProperty("payload"));
        var targetIds = PictureIds(view.GetProperty("target").GetProperty("picture").GetProperty("payload"));
        foreach (var f in view.GetProperty("frames").EnumerateArray()) {
            Assert.All(Strings(f.GetProperty("sourceFocus")), id => Assert.Contains(id, sourceIds));
            Assert.All(Strings(f.GetProperty("targetFocus")), id => Assert.Contains(id, targetIds));
            // A build frame's gadget indices point into the gadget list.
            if (f.TryGetProperty("gadgets", out var used))
                Assert.All(used.EnumerateArray(), i => Assert.InRange(i.GetInt32(), 0, view.GetProperty("gadgets").GetArrayLength() - 1));
        }
    }

    // ---- Phases ----

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Phases_AppearInOrder_BuildThenSolveThenMap_EachEndingInDone(string reduction, string instance) {
        var view = await View(reduction, instance);
        var phases = view.GetProperty("frames").EnumerateArray().Select(f => f.GetProperty("phase").GetString()!).ToList();
        var order = new[] { "build", "solve", "map" };
        Assert.Equal(order, phases.Distinct());
        Assert.Equal(phases.OrderBy(p => Array.IndexOf(order, p)), phases);
        foreach (string phase in order) {
            var frames = Phase(view, phase);
            Assert.Equal("Done", frames[^1].GetProperty("event").GetString());
            Assert.True(frames[^1].TryGetProperty("ok", out _), $"{phase} ends without ok");
            // Done closes a phase and nothing else does.
            Assert.DoesNotContain(frames.Take(frames.Count - 1), f => f.GetProperty("event").GetString() == "Done");
            // The recorders cap decisions at 150 and keep Done on top.
            Assert.InRange(frames.Count, 1, StepRecorder<object>.DefaultCap + 2);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Build_ReplayEndsWithExactlyTheTargetsNodesAndEdges(string reduction, string instance) {
        var view = await View(reduction, instance);
        var target = view.GetProperty("target");
        var payload = target.GetProperty("picture").GetProperty("payload");
        var nodes = payload.GetProperty("nodes").EnumerateArray().Select(n => n.GetProperty("id").GetString()!).ToList();
        var edges = payload.GetProperty("edges").EnumerateArray().Select(e => e.GetProperty("id").GetString()!).ToList();

        // The target picture is the reduction's real target.
        IReduction red = Build(reduction, instance);
        var (realNodes, realPairs) = red.reductionTo switch {
            API.Problems.NPComplete.NPC_CLIQUE.CLIQUE c => (c.nodes, c.edges),
            API.Problems.NPComplete.NPC_VERTEXCOVER.VERTEXCOVER v => (v.nodes, v.edges),
            var other => throw new NotSupportedException(other.GetType().Name),
        };
        Assert.Equal(realNodes.OrderBy(n => n, StringComparer.Ordinal), nodes.OrderBy(n => n, StringComparer.Ordinal));
        var realEdges = realPairs.Select(e => string.CompareOrdinal(e.Key, e.Value) <= 0 ? e.Key + "-" + e.Value : e.Value + "-" + e.Key).Distinct();
        Assert.Equal(realEdges.OrderBy(e => e, StringComparer.Ordinal), edges.OrderBy(e => e, StringComparer.Ordinal));

        // buildOrder names every target node and edge exactly once.
        var order = target.GetProperty("buildOrder");
        Assert.Equal(nodes.OrderBy(n => n, StringComparer.Ordinal), Strings(order.GetProperty("nodes")).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(edges.OrderBy(e => e, StringComparer.Ordinal), Strings(order.GetProperty("edges")).OrderBy(e => e, StringComparer.Ordinal));
        Assert.Equal(nodes.Count, order.GetProperty("nodes").GetArrayLength());
        Assert.Equal(edges.Count, order.GetProperty("edges").GetArrayLength());

        // The frames reveal the order monotonically and end with all of it. Each frame's focus is on screen when it is shown.
        var build = Phase(view, "build");
        int shownNodes = 0, shownEdges = 0;
        var nodeOrder = Strings(order.GetProperty("nodes"));
        var edgeOrder = Strings(order.GetProperty("edges"));
        foreach (var frame in build) {
            int n = frame.GetProperty("target").GetProperty("builtNodes").GetInt32(), e = frame.GetProperty("target").GetProperty("builtEdges").GetInt32();
            Assert.True(n >= shownNodes && e >= shownEdges, "the target only grows");
            (shownNodes, shownEdges) = (n, e);
            var visible = nodeOrder.Take(n).Concat(edgeOrder.Take(e)).ToHashSet();
            Assert.All(Strings(frame.GetProperty("targetFocus")), id => Assert.Contains(id, visible));
        }
        Assert.Equal(nodes.Count, shownNodes);
        Assert.Equal(edges.Count, shownEdges);
        // Solve and map show the finished target: no hiding.
        Assert.All(view.GetProperty("frames").EnumerateArray().Where(f => f.GetProperty("phase").GetString() != "build"),
            f => Assert.False(f.GetProperty("target").TryGetProperty("builtNodes", out _)));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Build_ReplaysTheRecordedStepsOfTheReduction(string reduction, string instance) {
        var view = await View(reduction, instance);
        var steps = Build(reduction, instance).BuildRun(true)!.Steps;
        var build = Phase(view, "build");
        Assert.Equal(steps.Count, build.Count);
        for (int i = 0; i < steps.Count; i++) {
            Assert.Equal(steps[i].Caption, build[i].GetProperty("caption").GetString());
            Assert.Equal(steps[i].Event.ToString(), build[i].GetProperty("event").GetString());
            Assert.Equal(steps[i].Partial.SourceFocus, Strings(build[i].GetProperty("sourceFocus")));
            Assert.Equal(steps[i].Partial.Gadgets.Length > 0, build[i].TryGetProperty("gadgets", out _));
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Solve_DoneOk_MatchesTheTargetVerifier(string reduction, string instance) {
        var view = await View(reduction, instance);
        IReduction red = Build(reduction, instance);
        string targetInstance = red.reductionTo.instance;
        string answer = red.reductionTo.defaultVisualization.solver.Run(targetInstance, false).Answer;
        // "{}" is judged by the reduction's targetAcceptsEmptyAnswer: the empty set can be a real answer (Clique -> VC on a triangle
        // with K = 3 gives an edgeless target with K' = 0) that the VC verifier refuses as unreadable.
        bool expected = answer.Trim() == "{}" ? red.targetAcceptsEmptyAnswer() : Verifies(red.reductionTo, targetInstance, answer);

        var solve = Phase(view, "solve");
        Assert.Equal(expected, solve[^1].GetProperty("ok").GetBoolean());
        // The source stays neutral while the target is solved.
        Assert.All(solve, f => Assert.Empty(f.GetProperty("sourceFocus").EnumerateArray()));
        Assert.All(solve, f => Assert.All(f.GetProperty("source").EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Object),
            p => Assert.Empty(p.Value.EnumerateObject())));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Map_DoneOk_MatchesTheSourceVerifierOnTheMappedBackAnswer(string reduction, string instance) {
        var view = await View(reduction, instance);
        IReduction red = Build(reduction, instance);
        string answer = red.reductionTo.defaultVisualization.solver.Run(red.reductionTo.instance, false).Answer;
        string mapped = red.MapBackRun(answer, false)!.Answer;
        bool expected = Verifies(red.reductionFrom, instance, mapped);

        var map = Phase(view, "map");
        Assert.Equal(expected, map[^1].GetProperty("ok").GetBoolean());
        // The mapped steps are the backward map's own, one frame each.
        var steps = red.MapBackRun(answer, true)!.Steps;
        Assert.Equal(steps.Count, map.Count);
    }

    // With K = 0 the empty set is a real answer on both sides; the verifiers alone refuse "{}" as unreadable, so the
    // last map frame must take the backward map's own verdict.
    [Fact]
    public async Task Map_EmptyAnswerThatIsReal_EndsOk() {
        var view = await View("reduceToCLIQUE", "(({a,b},{{a,b}}),0)");
        var map = Phase(view, "map");
        Assert.True(map[^1].GetProperty("ok").GetBoolean(), map[^1].GetProperty("caption").GetString());
    }

    // Clique -> Vertex Cover on a triangle with K = N = 3: the complement has no edges and K' = 0, so the empty cover is a
    // valid answer. The VC verifier refuses "{}" as unreadable; the solve phase must take the reduction's own word for it.
    [Fact]
    public async Task Solve_EmptyAnswerThatIsReal_EndsOk() {
        const string instance = "(({a,b,c},{{a,b},{b,c},{a,c}}),3)";
        IReduction red = Build(Vc, instance);
        Assert.Equal("{}", red.reductionTo.defaultVisualization.solver.Run(red.reductionTo.instance, false).Answer);

        var view = await View(Vc, instance);
        var solve = Phase(view, "solve");
        Assert.True(solve[^1].GetProperty("ok").GetBoolean(), solve[^1].GetProperty("caption").GetString());
        var map = Phase(view, "map");
        Assert.True(map[^1].GetProperty("ok").GetBoolean(), map[^1].GetProperty("caption").GetString());
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Map_StepsHighlightBothPanes_AndTheTargetKeepsItsSolution(string reduction, string instance) {
        var view = await View(reduction, instance);
        IReduction red = Build(reduction, instance);
        string answer = red.reductionTo.defaultVisualization.solver.Run(red.reductionTo.instance, false).Answer;
        var map = Phase(view, "map");
        var solved = Phase(view, "solve")[^1].GetProperty("target");
        // "{}" means B has no answer, unless the empty set is itself B's answer (the empty cover of an edgeless
        // graph); then it maps back like any other answer and the mapped answer is not "{}".
        if (ReductionBack.IsNoAnswer(answer) && ReductionBack.IsNoAnswer(red.MapBackRun(answer, false)!.Answer)) {
            Assert.Single(map);
            Assert.False(map[0].GetProperty("ok").GetBoolean());
            Assert.Empty(map[0].GetProperty("targetFocus").EnumerateArray());
            return;
        }
        // Each step that reads a target element names it, and the source ids it was built from.
        foreach (var frame in map.Where(f => f.GetProperty("targetFocus").GetArrayLength() > 0)) {
            Assert.NotEmpty(frame.GetProperty("sourceFocus").EnumerateArray());
            // The target still shows the solved picture: every element the solution marks is still marked, except where the focus
            // is drawn over it.
            var focus = Strings(frame.GetProperty("targetFocus")).ToHashSet();
            var nodes = frame.GetProperty("target").GetProperty("nodes");
            foreach (var marked in solved.GetProperty("nodes").EnumerateObject().Where(p => !focus.Contains(p.Name)))
                Assert.Equal(marked.Value.GetProperty("state").GetString(), nodes.GetProperty(marked.Name).GetProperty("state").GetString());
            Assert.All(focus, id => Assert.Equal("ElementHighlight", nodes.GetProperty(id).GetProperty("state").GetString()));
        }
    }

    // ---- The solver ----

    [Theory]
    [InlineData(Sat, "carraghanpardalos")]
    [InlineData(Vc, "greedyvertexcover")]
    [InlineData(Is, "carraghanpardalos")]
    public async Task ChosenSolver_RunsInTheSolvePhase(string reduction, string solver) {
        IReduction red = Build(reduction, ((IReduction)Activator.CreateInstance(ProblemProvider.Reductions[reduction.ToLower()])!).reductionFrom.defaultInstance);
        string instance = red.reductionFrom.defaultInstance;
        var chosen = (ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solver])!;
        var view = await View(reduction, instance, solver);
        var byDefault = await View(reduction, instance);

        Assert.Equal(chosen.solverName, view.GetProperty("solver").GetString());
        Assert.Equal(red.reductionTo.defaultVisualization.solver.solverName, byDefault.GetProperty("solver").GetString());
        Assert.NotEqual(view.GetProperty("solver").GetString(), byDefault.GetProperty("solver").GetString());

        // Its steps, not the default solver's, are the solve phase.
        var run = chosen.Run(red.reductionTo.instance, true);
        var solve = Phase(view, "solve");
        var lastStep = run.Steps.LastOrDefault();
        bool endsInDone = lastStep != null && lastStep.GetType().GetProperty("Event")!.GetValue(lastStep) is StepEvent.Done;
        Assert.Equal(run.Steps.Count + (endsInDone ? 0 : 1), solve.Count);
        Assert.Equal(Verifies(red.reductionTo, red.reductionTo.instance, run.Answer), solve[^1].GetProperty("ok").GetBoolean());
        // The build phase does not depend on the solver.
        Assert.Equal(Phase(byDefault, "build").Count, Phase(view, "build").Count);
    }

    // An incremental solver's solve phase is drawn the way its own picture draws it: the clique so far stays Solution in a
    // step that skips a vertex, and only the skipped vertex is Rejected (IIncrementalSolver, SolveRun.Incremental).
    [Theory]
    [InlineData(Is, "carraghanpardalos")]
    public async Task IncrementalSolver_SolvePhase_KeepsTheCommittedPartSolution_LikeItsOwnPicture(string reduction, string solver) {
        IReduction red = Build(reduction, ((IReduction)Activator.CreateInstance(ProblemProvider.Reductions[reduction.ToLower()])!).reductionFrom.defaultInstance);
        var view = await View(reduction, red.reductionFrom.defaultInstance, solver);
        var run = ((ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solver])!).Run(red.reductionTo.instance, true);
        Assert.True(run.Incremental);
        var steps = run.Steps.Cast<SolverStep<NodeSet>>().ToList();
        var solve = Phase(view, "solve");
        var checkedSteps = steps.Select((s, i) => (Step: s, Frame: solve[i])).Where(p => p.Step.Event is StepEvent.Try or StepEvent.Reject).ToList();
        Assert.Contains(checkedSteps, p => p.Step.Event == StepEvent.Reject && p.Step.Partial.Nodes.Length > 0);
        foreach (var (step, frame) in checkedSteps) {
            var nodes = frame.GetProperty("target").GetProperty("nodes");
            foreach (var node in step.Partial.Nodes.Except(step.Focus))
                Assert.Equal("Solution", nodes.GetProperty(node).GetProperty("state").GetString());
            string mark = step.Event == StepEvent.Reject ? "Rejected" : "ElementHighlight";
            foreach (var id in step.Focus)
                Assert.Equal(mark, nodes.GetProperty(id).GetProperty("state").GetString());
        }
    }

    // ---- 400s ----

    private async Task<JsonElement> Error(string reduction, string instance, string? solver = null) {
        var (status, body) = await Post(reduction, instance, solver);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        return JsonDocument.Parse(body).RootElement;
    }

    [Fact]
    public async Task UnknownReduction_Is400() {
        var error = await Error("nosuchreduction", "x");
        Assert.Equal("unknown_reduction", error.GetProperty("error").GetString());
        Assert.Equal("nosuchreduction", error.GetProperty("received").GetString());
    }

    // A reduction without construction steps or a backward map cannot be shown. Pick one of each from the registry so the
    // test keeps its meaning as reductions gain them.
    private static IEnumerable<string> ReductionsWhere(Func<IReduction, bool> keep) =>
        ProblemProvider.Reductions.Values
            .Select(t => { try { return (IReduction?)Activator.CreateInstance(t); } catch { return null; } })
            .Where(r => r != null && keep(r!)).Select(r => r!.GetType().Name);

    [Fact]
    public async Task ReductionWithoutBuildSteps_Is400_NoBuildSteps() {
        string name = ReductionsWhere(r => !r.hasBuildSteps()).First();
        var instance = ((IReduction)Activator.CreateInstance(ProblemProvider.Reductions[name.ToLower()])!).reductionFrom.defaultInstance;
        var error = await Error(name, instance);
        Assert.Equal("no_build_steps", error.GetProperty("error").GetString());
        Assert.Equal(name, error.GetProperty("reduction").GetString());
    }

    [Fact]
    public async Task ReductionWithBuildStepsButNoBackwardMap_Is400_NoBackwardMap() {
        // None of the shipped reductions has build steps without a backward map yet, so only the order of the checks is pinned:
        // a reduction missing both says what it is missing first (the build), and the three that have both answer 200.
        var both = ReductionsWhere(r => r.hasBuildSteps() && r.hasBackwardMap()).ToList();
        Assert.Equal(new[] { Is, Sat, Vc }.Select(n => n.ToLower()).Order(), both.Select(n => n.ToLower()).Order());
        var missing = ReductionsWhere(r => r.hasBuildSteps() && !r.hasBackwardMap()).ToList();
        foreach (string name in missing) {
            var instance = ((IReduction)Activator.CreateInstance(ProblemProvider.Reductions[name.ToLower()])!).reductionFrom.defaultInstance;
            Assert.Equal("no_backward_map", (await Error(name, instance)).GetProperty("error").GetString());
        }
    }

    [Theory]
    [InlineData(Sat, "(x1 | x2 | x3 | x4)")]
    [InlineData(Vc, "this is not an instance")]
    [InlineData(Is, "this is not an instance")]
    public async Task UnparseableSourceInstance_Is400_WithAParseErrorBody(string reduction, string badInstance) {
        var error = await Error(reduction, badInstance);
        Assert.Contains(error.GetProperty("error").GetString(), new[] { "instance_parse_error", "reduction_input_parse_error" });
        Assert.Equal(badInstance, error.GetProperty("received").GetString());
    }

    [Fact]
    public async Task UnknownSolver_Is400() {
        var error = await Error(Sat, ((IReduction)Activator.CreateInstance(ProblemProvider.Reductions[Sat.ToLower()])!).reductionFrom.defaultInstance, "nosuchsolver");
        Assert.Equal("unknown_solver", error.GetProperty("error").GetString());
    }

    [Fact]
    public async Task SolverForAnotherProblem_Is400_SolverMismatch() {
        // The solve phase runs on the TARGET (a Clique), so a 3SAT solver does not fit.
        var error = await Error(Sat, ((IReduction)Activator.CreateInstance(ProblemProvider.Reductions[Sat.ToLower()])!).reductionFrom.defaultInstance, "sat3backtrackingsolver");
        Assert.Equal("solver_mismatch", error.GetProperty("error").GetString());
        Assert.Equal("Clique", error.GetProperty("problem").GetString(), ignoreCase: true);
    }

    // ---- Old endpoints are untouched ----

    [Fact]
    public void TheNewTypeIsNotReportedByInfo() {
        // Reduction is a payload type of the new endpoint only: no visualization claims it, so /info and the legacy
        // contract (snapshots in Snapshots/) are unchanged.
        foreach (var vis in ProblemProvider.Visualizers.Values) {
            var instance = Activator.CreateInstance(vis) as IVisualization;
            if (instance != null) Assert.NotEqual(VisualizationType.Reduction, instance.visualizationType);
        }
    }
}

// The Reduction View with a 50 ms solve budget: a solve that runs past it ends the solve phase with its stopped Done frame, and
// the map phase is a single Done frame saying there is nothing to map back.
public class ReductionView_TimeLimit_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public ReductionView_TimeLimit_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task StoppedSolve_EndsTheSolvePhaseWithTheStoppedFrame_AndSkipsTheMap() {
        // Independent Set on K30 with K = 15 becomes Clique on the edgeless complement with K = 15: the brute force tries
        // C(30,15) sets, none a clique, far longer than the budget.
        var nodes = Enumerable.Range(1, 30).Select(i => $"n{i}").ToList();
        var edges = from a in nodes from b in nodes where string.CompareOrdinal(a, b) < 0 select $"{{{a},{b}}}";
        string instance = $"(({{{string.Join(",", nodes)}}},{{{string.Join(",", edges)}}}),15)";
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await _client.PostAsync("/ProblemProvider/reductionView?reduction=reduceToCLIQUE&solver=cliquebruteforce",
            content, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"took {sw.Elapsed}");

        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        var solve = frames.Where(f => f.GetProperty("phase").GetString() == "solve").ToList();
        var map = frames.Where(f => f.GetProperty("phase").GetString() == "map").ToList();
        Assert.Equal("Done", solve[^1].GetProperty("event").GetString());
        Assert.False(solve[^1].GetProperty("ok").GetBoolean());
        Assert.StartsWith("Stopped after 0.05 s and ", solve[^1].GetProperty("caption").GetString());
        Assert.Single(solve, f => f.GetProperty("event").GetString() == "Done");
        var only = Assert.Single(map);
        Assert.Equal("Done", only.GetProperty("event").GetString());
        Assert.False(only.GetProperty("ok").GetBoolean());
        Assert.Equal("The solver had not finished, so there is no Clique answer to map back.", only.GetProperty("caption").GetString());
        Assert.Equal("map", frames[^1].GetProperty("phase").GetString());
    }

    [Fact]
    public async Task StoppedSolve_OnAVertexCoverTarget_DrawsTheStoppedFrameBlank_NotAnEmptyCoverWithEveryEdgeRejected() {
        // Clique on 30 nodes with one edge and K = 15 becomes Vertex Cover on the complement (nearly K30) with K = 15: the brute
        // force tries C(30,15) sets and cannot finish in 50 ms. Drawn as an ordinary Done, the stopped step's empty cover would
        // mark every edge Rejected; it must be blank instead.
        string instance = StepTimeLimit_Endpoint_Tests.SlowClique;
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/reductionView?reduction=sipserReductionVertexCover&solver=vertexcoverbruteforce",
            content, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        var last = frames.Where(f => f.GetProperty("phase").GetString() == "solve").Last();
        Assert.False(last.GetProperty("ok").GetBoolean());
        Assert.StartsWith("Stopped after 0.05 s and ", last.GetProperty("caption").GetString());
        var target = last.GetProperty("target");
        Assert.Empty(target.GetProperty("nodes").EnumerateObject());
        Assert.Empty(target.GetProperty("edges").EnumerateObject());
    }
}
