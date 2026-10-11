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

    // The problem's own verifier; a certificate it cannot read is not correct.
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
            // The bound (K) comes from no single piece of either side (#694).
            if (g.GetProperty("gadgetKind").GetString() != "bound") {
                Assert.NotEmpty(g.GetProperty("sourceIds").EnumerateArray());
                Assert.NotEmpty(g.GetProperty("targetIds").EnumerateArray());
            }
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
    public async Task Build_StepsRevealTheWholeTargetBeforeDone_AndSkippedPairsGiveTheirEnds(string reduction, string instance) {
        var view = await View(reduction, instance);
        var payload = view.GetProperty("target").GetProperty("picture").GetProperty("payload");
        var nodes = payload.GetProperty("nodes").EnumerateArray().Select(n => n.GetProperty("id").GetString()!).ToHashSet();
        var edges = payload.GetProperty("edges").EnumerateArray().Select(e => e.GetProperty("id").GetString()!).ToHashSet();
        var steps = Build(reduction, instance).BuildRun(true)!.Steps;
        var build = Phase(view, "build");
        int shownEdges = 0;
        for (int i = 0; i < steps.Count; i++) {
            var target = build[i].GetProperty("target");
            // Every edge a step adds is a picture edge under the same id, so each frame reveals exactly that many more.
            Assert.All(steps[i].Partial.Edges, e => Assert.Contains(e.Id, edges));
            int e = target.GetProperty("builtEdges").GetInt32();
            Assert.Equal(steps[i].Partial.Edges.Length, e - shownEdges);
            shownEdges = e;
            if (target.TryGetProperty("skipped", out var skipped)) {
                Assert.All(skipped.EnumerateArray(), s => {
                    string id = s.GetProperty("id").GetString()!, a = s.GetProperty("a").GetString()!, b = s.GetProperty("b").GetString()!;
                    Assert.Contains(a, nodes);
                    Assert.Contains(b, nodes);
                    Assert.Equal(API.Interfaces.Graphs.GraphSubsetFrames.EdgeId(a, b), id);
                    Assert.DoesNotContain(id, edges);
                });
            }
        }
        // The steps themselves announce the whole target: the frame before Done already shows all of it.
        var beforeDone = build[^2].GetProperty("target");
        Assert.Equal(nodes.Count, beforeDone.GetProperty("builtNodes").GetInt32());
        Assert.Equal(edges.Count, beforeDone.GetProperty("builtEdges").GetInt32());
    }

    // The pane states a frame lists, as (id, state), from every state map of the pane (nodes, edges, groups).
    private static IEnumerable<(string Id, string State)> States(JsonElement pane) =>
        pane.EnumerateObject()
            .Where(p => p.Value.ValueKind == JsonValueKind.Object)
            .SelectMany(p => p.Value.EnumerateObject())
            .Where(e => e.Value.ValueKind == JsonValueKind.Object && e.Value.TryGetProperty("state", out _))
            .Select(e => (e.Name, e.Value.GetProperty("state").GetString()!));

    // While the target is being built nothing is chosen or judged: both panes only highlight what the step is about. Drawn as a
    // whole candidate (a Try of the empty set), the Vertex Cover target marked every edge Rejected in every build frame.
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Build_PanesOnlyHighlight_NothingIsSolutionOrRejected(string reduction, string instance) {
        var view = await View(reduction, instance);
        foreach (var frame in Phase(view, "build")) {
            Assert.All(States(frame.GetProperty("source")), s => Assert.Equal("ElementHighlight", s.State));
            Assert.All(States(frame.GetProperty("target")), s => Assert.Equal("ElementHighlight", s.State));
            Assert.Empty(frame.GetProperty("target").GetProperty("phantoms").EnumerateArray());
        }
    }

    // A build frame never draws a target element that is not on screen yet: every id its target pane gives a state, and both
    // ends of every skipped pair, are among the first builtNodes / builtEdges of buildOrder.
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Build_FramesOnlyNameTargetElementsAlreadyBuilt(string reduction, string instance) {
        var view = await View(reduction, instance);
        var order = view.GetProperty("target").GetProperty("buildOrder");
        var nodeOrder = Strings(order.GetProperty("nodes"));
        var edgeOrder = Strings(order.GetProperty("edges"));
        foreach (var frame in Phase(view, "build")) {
            var target = frame.GetProperty("target");
            var builtNodes = nodeOrder.Take(target.GetProperty("builtNodes").GetInt32()).ToHashSet();
            var builtEdges = edgeOrder.Take(target.GetProperty("builtEdges").GetInt32()).ToHashSet();
            Assert.All(target.GetProperty("nodes").EnumerateObject(), n => Assert.Contains(n.Name, builtNodes));
            Assert.All(target.GetProperty("edges").EnumerateObject(), e => Assert.Contains(e.Name, builtEdges));
            if (target.TryGetProperty("skipped", out var skipped))
                Assert.All(skipped.EnumerateArray(), s => {
                    Assert.Contains(s.GetProperty("a").GetString()!, builtNodes);
                    Assert.Contains(s.GetProperty("b").GetString()!, builtNodes);
                });
        }
    }

    // Past the step cap the rest of the construction is folded into one step: the view shows exactly what the steps add, so the
    // frame before Done must already show the whole target (nothing is filled in from the picture at the end).
    [Theory]
    [InlineData(Sat, 20)]
    public async Task Build_PastTheCap_TheFoldedStepCompletesTheTarget(string reduction, int clauses) {
        string instance = string.Join(" & ", Enumerable.Range(0, clauses).Select(i => $"(x{i} | !x{i + 1} | x{i + 2})"));
        var view = await View(reduction, instance, "carraghanpardalos");
        var build = Phase(view, "build");
        Assert.Equal(StepRecorder<BuildDelta>.DefaultCap + 2, build.Count);
        Assert.StartsWith("The last ", build[^2].GetProperty("caption").GetString());
        var payload = view.GetProperty("target").GetProperty("picture").GetProperty("payload");
        var order = view.GetProperty("target").GetProperty("buildOrder");
        Assert.Equal(payload.GetProperty("nodes").EnumerateArray().Select(n => n.GetProperty("id").GetString()!).Order(StringComparer.Ordinal),
            Strings(order.GetProperty("nodes")).Order(StringComparer.Ordinal));
        Assert.Equal(payload.GetProperty("edges").EnumerateArray().Select(e => e.GetProperty("id").GetString()!).Order(StringComparer.Ordinal),
            Strings(order.GetProperty("edges")).Order(StringComparer.Ordinal));
        foreach (var frame in new[] { build[^2], build[^1] }) {
            Assert.Equal(order.GetProperty("nodes").GetArrayLength(), frame.GetProperty("target").GetProperty("builtNodes").GetInt32());
            Assert.Equal(order.GetProperty("edges").GetArrayLength(), frame.GetProperty("target").GetProperty("builtEdges").GetInt32());
        }
    }

    // ---- One build ----

    // /reductionView and /buildSteps build the reduction once, with the construction recorded on the way, and use that one
    // build for both the reduction and its steps. That needs the recording constructor on every reduction that records steps.
    [Fact]
    public void EveryRecordedReduction_HasTheRecordingConstructor() {
        var recorded = ProblemProvider.Reductions.Values.Distinct()
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRecordedReduction<,>))).ToList();
        Assert.NotEmpty(recorded);
        Assert.All(recorded, t => Assert.NotNull(t.GetConstructor([typeof(string), typeof(StepRecorder<BuildDelta>)])));
    }

    // The one recorded build is the same build as the plain one, and its steps are the ones BuildRun records on a copy.
    [Theory]
    [MemberData(nameof(Cases))]
    public void RecordedReduction_BuildsOnce_TheSameTargetGadgetsAndSteps(string reduction, string instance) {
        var rec = new StepRecorder<BuildDelta>(cap: int.MaxValue);
        var once = (IReduction)Activator.CreateInstance(ProblemProvider.Reductions[reduction.ToLower()], instance, rec)!;
        // The constructor that built the reduction is the one that recorded: there is no second build.
        Assert.NotEmpty(rec.Steps);
        Assert.Equal(StepEvent.Done, rec.Steps[^1].Event);

        var (red, steps) = ProblemProvider.RecordedReduction(reduction, instance);
        IReduction plain = Build(reduction, instance);
        var expected = plain.BuildRun(true)!.Steps;
        Assert.Equal(plain.reductionTo.instance, red.reductionTo.instance);
        Assert.Equal(plain.reductionTo.instance, once.reductionTo.instance);
        Assert.Equal(JsonSerializer.Serialize(plain.gadgets), JsonSerializer.Serialize(red.gadgets));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(steps));
    }

    [Fact]
    public async Task Build_SkippedPairs_AreListedWithTheirEnds() {
        var view = await View(Sat, "(x1) & (!x1)");
        var skipped = Phase(view, "build").Select(f => f.GetProperty("target")).Where(t => t.TryGetProperty("skipped", out _)).ToList();
        var pair = Assert.Single(Assert.Single(skipped).GetProperty("skipped").EnumerateArray());
        Assert.Equal("!x1_1-x1_0", pair.GetProperty("id").GetString());
        Assert.Equal("!x1_1", pair.GetProperty("a").GetString());
        Assert.Equal("x1_0", pair.GetProperty("b").GetString());
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
        // "{}" too: the verifiers read it as the empty set, which can be a real answer (Clique -> VC on a triangle with K = 3
        // gives an edgeless target with K' = 0).
        bool expected = Verifies(red.reductionTo, targetInstance, answer);

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

    // With K = 0 the empty set is a real answer on both sides, and the verifiers say so (#762).
    [Fact]
    public async Task Map_EmptyAnswerThatIsReal_EndsOk() {
        var view = await View("reduceToCLIQUE", "(({a,b},{{a,b}}),0)");
        var map = Phase(view, "map");
        Assert.True(map[^1].GetProperty("ok").GetBoolean(), map[^1].GetProperty("caption").GetString());
    }

    // Clique -> Vertex Cover on a triangle with K = N = 3: the complement has no edges and K' = 0, so the empty cover is a
    // valid answer, and the VC verifier says so (#762).
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

    // The backward map builds the source answer one element at a time, like an incremental solver: what it has mapped so far
    // stays Solution in every step, including a step that leaves an element out (Clique -> Vertex Cover: "d is in the cover, so
    // it is left out of the clique"), and only the element being read is highlighted. Drawn as a whole candidate, that Reject
    // step turned the clique built so far from Solution to ElementHighlight.
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Map_SourcePane_KeepsWhatIsMappedSolution_AndOnlyHighlightsTheElementRead(string reduction, string instance) {
        var view = await View(reduction, instance);
        var map = Phase(view, "map");
        var mappedSoFar = new HashSet<string>(StringComparer.Ordinal);
        // A Grouped Graph literal is also read when its variable is (a step about a variable no clique node mentions, #692).
        var variableOf = Pictures[reduction].SourceType != "GroupedGraph" ? new Dictionary<string, string>()
            : view.GetProperty("source").GetProperty("picture").GetProperty("payload").GetProperty("nodes").EnumerateArray()
                .ToDictionary(n => n.GetProperty("id").GetString()!, n => n.GetProperty("variable").GetString()!);
        foreach (var frame in map) {
            var focus = Strings(frame.GetProperty("sourceFocus")).ToHashSet();
            bool Read(string id) => focus.Contains(id) || (variableOf.TryGetValue(id, out var v) && focus.Contains(v));
            var states = States(frame.GetProperty("source")).ToList();
            // A highlight or a rejection is only ever the element being read.
            Assert.All(states.Where(s => s.State is "ElementHighlight" or "Rejected" && !IsClause(s.Id)), s => Assert.True(Read(s.Id), $"{s.Id} is {s.State} but not read"));
            // What an earlier step mapped is still Solution now.
            var solution = states.Where(s => s.State == "Solution").Select(s => s.Id).ToHashSet();
            Assert.All(mappedSoFar.Where(id => !Read(id)), id => Assert.Contains(id, solution));
            mappedSoFar.UnionWith(solution);
        }
        // A Reject step on a Graph source shows the element read as Rejected: it is left out.
        foreach (var frame in map.Where(f => f.GetProperty("event").GetString() == "Reject" && Pictures[reduction].SourceType == "Graph"))
            Assert.All(Strings(frame.GetProperty("sourceFocus")), id =>
                Assert.Equal("Rejected", frame.GetProperty("source").GetProperty("nodes").GetProperty(id).GetProperty("state").GetString()));

        // A clause of the Grouped Graph that every literal leaves false is Rejected by the assignment, not by the map.
        static bool IsClause(string id) => System.Text.RegularExpressions.Regex.IsMatch(id, @"^c\d+$");
    }

    [Fact]
    public async Task Map_RejectStep_IsInTheCases() {
        // The theory above checks a Reject map step only if one of the cases has it; this one does.
        var view = await View(Vc, "(({a,b,c,d},{{a,b},{b,c},{a,c},{c,d}}),3)");
        var reject = Assert.Single(Phase(view, "map"), f => f.GetProperty("event").GetString() == "Reject");
        var nodes = reject.GetProperty("source").GetProperty("nodes");
        Assert.Equal("Rejected", nodes.GetProperty("d").GetProperty("state").GetString());
        foreach (string kept in new[] { "a", "b", "c" })
            Assert.Equal("Solution", nodes.GetProperty(kept).GetProperty("state").GetString());
        foreach (string edge in new[] { "a-b", "a-c", "b-c" })
            Assert.Equal("Solution", reject.GetProperty("source").GetProperty("edges").GetProperty(edge).GetProperty("state").GetString());
    }

    // A literal the mapped-back assignment makes false is False on the 3SAT pane in every map frame; a true one is Solution (or
    // highlighted while it is read); a literal of a variable not assigned yet is not drawn.
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Map_SatSource_FalseLiteralsAreFalse_TrueOnesSolution(string reduction, string instance) {
        if (Pictures[reduction].SourceType != "GroupedGraph") return;
        var view = await View(reduction, instance);
        var literals = view.GetProperty("source").GetProperty("picture").GetProperty("payload").GetProperty("nodes").EnumerateArray()
            .Select(n => (Id: n.GetProperty("id").GetString()!, Variable: n.GetProperty("variable").GetString()!, Positive: n.GetProperty("positive").GetBoolean()))
            .ToList();
        foreach (var frame in Phase(view, "map")) {
            var source = frame.GetProperty("source");
            var assignment = source.GetProperty("assignment");
            var nodes = source.GetProperty("nodes");
            var focus = Strings(frame.GetProperty("sourceFocus")).ToHashSet();
            foreach (var (id, variable, positive) in literals) {
                string? state = nodes.TryGetProperty(id, out var s) ? s.GetProperty("state").GetString() : null;
                if (!assignment.TryGetProperty(variable, out var value)) { Assert.Null(state); continue; }
                if (value.GetBoolean() != positive) Assert.Contains(state, new[] { "False", "Rejected" });
                // Read as itself, or as its variable (a step about a variable no clique node mentions, #692).
                else Assert.Equal(focus.Contains(id) || focus.Contains(variable) ? "ElementHighlight" : "Solution", state);
            }
        }
    }

    // Solution is only a real answer: a brute-force solver's tries (Try, Reject) never draw a node as Solution on the target pane.
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Solve_BruteForceTries_AreNeverSolution(string reduction, string instance) {
        var view = await View(reduction, instance);
        IReduction red = Build(reduction, instance);
        Assert.False(red.reductionTo.defaultVisualization.solver.Run(red.reductionTo.instance, false).Incremental);
        foreach (var frame in Phase(view, "solve").Where(f => f.GetProperty("event").GetString() is "Try" or "Reject"))
            Assert.DoesNotContain(States(frame.GetProperty("target")), s => s.State == "Solution");
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

    // Every solver of the target problem, on every case: 200, and both verdicts are the verifiers' (some of these solvers
    // answer covers larger than K, or no answer at all, so this covers the failing and the "{}" paths too).
    public static TheoryData<string, string, string> EverySolverCases() {
        var data = new TheoryData<string, string, string>();
        foreach (var (reduction, instance) in Reduction_Output_Snapshot_Tests.Cases()) {
            Type target = Build(reduction, instance).reductionTo.GetType();
            foreach (var (key, type) in ProblemProvider.Solvers.OrderBy(p => p.Key, StringComparer.Ordinal))
                if (type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>) && i.GetGenericArguments()[0] == target))
                    data.Add(reduction, instance, key);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(EverySolverCases))]
    public async Task EverySolverOfTheTarget_GivesAView_WhoseVerdictsAreTheVerifiers(string reduction, string instance, string solver) {
        var view = await View(reduction, instance, solver);
        IReduction red = Build(reduction, instance);
        // The answer is read off the view's own solved frame (its Solution nodes): some of these solvers choose at random (the
        // 2-approximation picks an arbitrary edge), so a second run here could answer differently.
        var solved = Phase(view, "solve")[^1].GetProperty("target").GetProperty("nodes").EnumerateObject()
            .Where(n => n.Value.GetProperty("state").GetString() == "Solution").Select(n => n.Name);
        string answer = "{" + string.Join(",", solved) + "}";
        bool solvedOk = Verifies(red.reductionTo, red.reductionTo.instance, answer);
        Assert.Equal(solvedOk, Phase(view, "solve")[^1].GetProperty("ok").GetBoolean());
        var mapped = red.MapBackRun(answer, true)!;
        var done = mapped.Steps[^1];
        Assert.Equal((bool?)done.GetType().GetProperty("Ok")!.GetValue(done), Phase(view, "map")[^1].GetProperty("ok").GetBoolean());
        // The mapped answer is judged by the source verifier alone, "{}" included.
        Assert.Equal(Verifies(red.reductionFrom, instance, mapped.Answer), Phase(view, "map")[^1].GetProperty("ok").GetBoolean());
    }

    // ---- 400s ----

    private async Task<JsonElement> Error(string reduction, string instance, string? solver = null) {
        var (status, body) = await Post(reduction, instance, solver);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        return JsonDocument.Parse(body).RootElement;
    }

    // #760: a Clique K outside 0..N is input Clique -> Vertex Cover cannot take (K' = N - K would be negative or above N).
    [Theory]
    [InlineData("(({a,b,c},{{a,b}}),4)")]
    [InlineData("(({a,b,c},{{a,b}}),-1)")]
    public async Task CliqueKOutsideZeroToN_Is400ReductionInput(string instance) {
        var error = await Error(Vc, instance);
        Assert.Equal("reduction_input_parse_error", error.GetProperty("error").GetString());
        Assert.Contains("K must be between 0 and the number of nodes (3)", error.GetProperty("detail").GetString());
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
        // a reduction missing both says what it is missing first (the build), and the ones that have both answer 200: the three
        // of #692 and the reductions out of 3SAT and SAT (#693).
        var both = ReductionsWhere(r => r.hasBuildSteps() && r.hasBackwardMap()).ToList();
        Assert.Equal(new[] { Is, Sat, Vc, "KarpReduceGRAPHCOLORING", "GareyJohnson", "KarpIntProgStandard", "KarpSATToSAT3", "SipserReduceToSAT3", "LawlerKarp", "KarpVertexCoverToNodeSet", "KarpVertexCoverToSetCover", "KarpDirectedHamiltonianToUndirectedHamiltonian", "GraphColoringToCliqueCover" }.Select(n => n.ToLower()).Order(),
            both.Select(n => n.ToLower()).Order());
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
        // A stopped solve has no answer: neither its stopped frame nor the map frame draws anything as the answer.
        foreach (var frame in new[] { solve[^1], only })
            foreach (string pane in new[] { "source", "target" })
                Assert.All(frame.GetProperty(pane).EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Object),
                    p => Assert.Empty(p.Value.EnumerateObject()));
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
