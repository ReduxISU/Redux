using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_CLIQUE;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Reduction View (#692) of the graph reductions (#694), on the real /ProblemProvider/reductionView output. Their sources
// are drawn with the Graph picture (subsets, colorings and tours) and their targets with four pictures (Graph, Grouped Graph,
// Layered Graph, Bipartite Graph), so these tests read each payload by its kind, as ReductionView_Sat3_Tests does.
public class ReductionView_Graph_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public ReductionView_Graph_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    public const string CliqueToSat3 = "SipserReduceToSAT3", CliqueToVc = "sipserReductionVertexCover", IsToClique = "reduceToCLIQUE",
        VcToArcSet = "LawlerKarp", VcToNodeSet = "KarpVertexCoverToNodeSet", VcToSetCover = "KarpVertexCoverToSetCover",
        DirHamToHam = "KarpDirectedHamiltonianToUndirectedHamiltonian", ColoringToCliqueCover = "GraphColoringToCliqueCover",
        ColoringToExactCover = "KarpGraphColorToExactCover", ColoringToSat = "KarpReduceSAT";

    public static readonly string[] All = [CliqueToSat3, CliqueToVc, IsToClique, VcToArcSet, VcToNodeSet, VcToSetCover, DirHamToHam,
        ColoringToCliqueCover, ColoringToExactCover, ColoringToSat];

    // Each reduction's target picture type. Every source is drawn with the Graph picture.
    public static readonly Dictionary<string, string> TargetPicture = new(StringComparer.OrdinalIgnoreCase) {
        [CliqueToSat3] = "GroupedGraph",
        [CliqueToVc] = "Graph",
        [IsToClique] = "Graph",
        [VcToArcSet] = "LayeredGraph",
        [VcToNodeSet] = "LayeredGraph",
        [VcToSetCover] = "BipartiteGraph",
        [DirHamToHam] = "Graph",
        [ColoringToCliqueCover] = "Graph",
        [ColoringToExactCover] = "BipartiteGraph",
        [ColoringToSat] = "GroupedGraph",
    };

    // The clique graph Sipser's 3SAT to Clique builds from a formula: the only graphs Sipser's Clique to 3SAT is a reduction for.
    internal static string Sipser(string formula) => new SipserReduceToCliqueStandard(new SAT3(formula)).reductionTo.instance;

    private const string VcYes = "(({a,b,c},{{a,b},{b,c}}),1)", VcNo = "(({a,b,c},{{a,b},{b,c},{a,c}}),1)", VcEmpty = "(({a,b},{}),0)",
        VcSeparators = "(({x-1,y>2,z},{{x-1,y>2},{y>2,z}}),1)", VcUnderscores = "(({a_b,c,a,b_c},{{a_b,c},{a,b_c}}),1)",
        VcTwins = "(({a,b,c},{{a,b}}),1)";
    private const string ColorYes = "(({a,b,c},{{a,b},{b,c}}),2)", ColorNo = "(({a,b,c},{{a,b},{b,c},{a,c}}),2)", ColorOne = "(({a},{}),1)",
        ColorSeparators = "(({x-1,y_2,z},{{x-1,y_2},{y_2,z}}),2)",
        ColorIsolated = "(({a,b,c},{{a,b}}),2)";

    // Instances small enough for every solver of the target, each with whether the source has an answer, worked out by hand:
    // yes and no instances, an empty or one-node graph, and node names that hold the pictures' separators.
    public static IEnumerable<(string Reduction, string Instance, bool HasAnswer)> Cases() {
        yield return (CliqueToSat3, Sipser("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)"), true);
        yield return (CliqueToSat3, Sipser("(x1) & (!x1)"), false);
        yield return (CliqueToSat3, Sipser("(a | b) & (!a) & (!b)"), false);
        yield return (CliqueToSat3, Sipser("(x1 | x1 | x2) & (!x2)"), true);
        // A triangle a, b, c holds the clique of 3; the path does not.
        yield return (CliqueToVc, "(({a,b,c,d},{{a,b},{b,c},{a,c},{c,d}}),3)", true);
        yield return (CliqueToVc, "(({a,b,c,d},{{a,b},{b,c},{c,d}}),3)", false);
        yield return (CliqueToVc, "(({a-b,c},{{a-b,c}}),2)", true);
        // The path a-b-c-d has the independent set {a,c}; a triangle has no two independent nodes.
        yield return (IsToClique, "(({a,b,c,d},{{a,b},{b,c},{c,d}}),2)", true);
        yield return (IsToClique, "(({a,b,c},{{a,b},{b,c},{a,c}}),2)", false);
        yield return (IsToClique, "(({a,b,c},{}),3)", true);
        // b covers the path a-b-c; no one node covers a triangle; an edgeless graph needs none; the two edges of the
        // underscore graph share no node.
        foreach (string reduction in new[] { VcToArcSet, VcToNodeSet, VcToSetCover }) {
            yield return (reduction, VcYes, true);
            yield return (reduction, VcNo, false);
            yield return (reduction, VcEmpty, true);
            yield return (reduction, VcSeparators, true);
            yield return (reduction, VcUnderscores, false);
            // a and b touch the same edge, so Set Cover reads their two sets as one; c touches none.
            yield return (reduction, VcTwins, true);
        }
        yield return (DirHamToHam, "({1,2,3},{(1,2),(2,3),(3,1)})", true);
        yield return (DirHamToHam, "({1,2,3},{(1,2),(2,3),(1,3)})", false);
        yield return (DirHamToHam, "({a-b,c},{(a-b,c),(c,a-b)})", true);
        // A path takes 2 colors; a triangle does not.
        foreach (string reduction in new[] { ColoringToCliqueCover, ColoringToExactCover, ColoringToSat }) {
            yield return (reduction, ColorYes, true);
            yield return (reduction, ColorNo, false);
            yield return (reduction, ColorOne, true);
            // c has no edge, so Exact Cover reads its two color sets as one.
            yield return (reduction, ColorIsolated, true);
        }
        yield return (ColoringToCliqueCover, ColorSeparators, true);
        yield return (ColoringToExactCover, ColorSeparators, true);
        yield return (ColoringToCliqueCover, "(({a,b,c},{{a,b},{b,c},{a,c}}),3)", true);
        yield return (ColoringToSat, "(({a,b,c},{{a,b},{b,c},{a,c}}),3)", true);
    }

    public static TheoryData<string, string> CaseData() {
        var data = new TheoryData<string, string>();
        foreach (var (r, i, _) in Cases()) data.Add(r, i);
        return data;
    }

    public static TheoryData<string, string, bool> CaseAnswers() {
        var data = new TheoryData<string, string, bool>();
        foreach (var (r, i, yes) in Cases()) data.Add(r, i, yes);
        return data;
    }

    internal static IReduction Build(string reduction, string instance) =>
        (IReduction)Activator.CreateInstance(ProblemProvider.Reductions[reduction.ToLower()], instance)!;

    private async Task<(HttpStatusCode Status, string Body)> Post(string path, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/" + path, content, TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task<JsonElement> View(string reduction, string instance, string? solver = null) {
        var (status, body) = await Post($"reductionView?reduction={reduction}" + (solver == null ? "" : $"&solver={solver}"), instance);
        Assert.True(status == HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement;
    }

    private static List<JsonElement> Phase(JsonElement view, string phase) =>
        view.GetProperty("frames").EnumerateArray().Where(f => f.GetProperty("phase").GetString() == phase).ToList();

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()!).ToArray();

    private static IEnumerable<string> Ids(JsonElement payload, string list) =>
        payload.TryGetProperty(list, out var items) ? items.EnumerateArray().Select(i => i.GetProperty("id").GetString()!) : [];

    // The pieces of a picture a build step reveals, by the picture's kind (see BuildDelta): what stands on its own (nodes) and
    // what is drawn between two of those (edges).
    internal static (List<string> Nodes, List<string> Edges) Pieces(JsonElement payload) => payload.GetProperty("kind").GetString() switch {
        "graph" or "layeredGraph" => (Ids(payload, "nodes").ToList(), Ids(payload, "edges").ToList()),
        "groupedGraph" => (Ids(payload, "groups").Concat(Ids(payload, "nodes")).Concat(Ids(payload, "variables")).ToList(),
            Ids(payload, "edges").Concat(Ids(payload, "clauseEdges")).ToList()),
        "bipartiteGraph" => (Ids(payload, "elements").Concat(Ids(payload, "sets")).ToList(), Ids(payload, "edges").ToList()),
        var other => throw new NotSupportedException(other),
    };

    internal static HashSet<string> PictureIds(JsonElement payload) {
        var (nodes, edges) = Pieces(payload);
        return nodes.Concat(edges).ToHashSet(StringComparer.Ordinal);
    }

    private static bool Verifies(IProblem problem, string instance, string answer) {
        try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
    }

    // The answer a source pane shows, as certificate text: the chosen nodes (Solution) of a subset picture, the color classes of
    // a coloring, the order of a tour. Read from the picture, not from the backward map.
    private static string ShownAnswer(JsonElement pane) {
        if (pane.TryGetProperty("groups", out var groups)) {
            var classes = groups.EnumerateObject().GroupBy(p => p.Value.GetInt32()).OrderBy(g => g.Key).Select(g => "{" + string.Join(",", g.Select(p => p.Name)) + "}");
            return "{" + string.Join(",", classes) + "}";
        }
        if (pane.TryGetProperty("order", out var order))
            return "{" + string.Join(",", Strings(order)) + "}";
        var chosen = pane.GetProperty("nodes").EnumerateObject().Where(p => p.Value.GetProperty("state").GetString() == "Solution").Select(p => p.Name);
        return "{" + string.Join(",", chosen) + "}";
    }

    internal static IEnumerable<(string Id, string State)> States(JsonElement pane) =>
        pane.EnumerateObject()
            .Where(p => p.Value.ValueKind == JsonValueKind.Object)
            .SelectMany(p => p.Value.EnumerateObject())
            .Where(e => e.Value.ValueKind == JsonValueKind.Object && e.Value.TryGetProperty("state", out _))
            .Select(e => (e.Name, e.Value.GetProperty("state").GetString()!));

    // ---- Shape and ids ----

    [Theory]
    [MemberData(nameof(CaseData))]
    public async Task Shape_BothPicturesTheGadgetsAndThreePhases(string reduction, string instance) {
        var view = await View(reduction, instance);
        IReduction red = Build(reduction, instance);
        Assert.Equal("Reduction", view.GetProperty("type").GetString());
        Assert.Equal(red.reductionName, view.GetProperty("reductionName").GetString());
        Assert.Equal("Graph", view.GetProperty("source").GetProperty("picture").GetProperty("type").GetString());
        Assert.Equal(TargetPicture[reduction], view.GetProperty("target").GetProperty("picture").GetProperty("type").GetString());
        Assert.Equal(red.reductionTo.instance, view.GetProperty("target").GetProperty("instance").GetString());
        var gadgets = view.GetProperty("gadgets").EnumerateArray().ToList();
        Assert.Equal(red.gadgets.Count, gadgets.Count);
        Assert.All(gadgets, g => Assert.True(Enum.TryParse<API.Interfaces.JSON_Objects.GadgetKind>(g.GetProperty("gadgetKind").GetString(), true, out _)));

        var phases = view.GetProperty("frames").EnumerateArray().Select(f => f.GetProperty("phase").GetString()!).ToList();
        Assert.Equal(["build", "solve", "map"], phases.Distinct());
        foreach (string phase in new[] { "build", "solve", "map" }) {
            var frames = Phase(view, phase);
            Assert.Equal("Done", frames[^1].GetProperty("event").GetString());
            Assert.DoesNotContain(frames.Take(frames.Count - 1), f => f.GetProperty("event").GetString() == "Done");
        }
        Assert.All(view.GetProperty("frames").EnumerateArray(), f => Assert.False(string.IsNullOrWhiteSpace(f.GetProperty("caption").GetString())));
    }

    [Theory]
    [MemberData(nameof(CaseData))]
    public async Task EveryGadgetAndFocusId_IsAnIdOfItsPanesPicture(string reduction, string instance) {
        var view = await View(reduction, instance);
        var sourceIds = PictureIds(view.GetProperty("source").GetProperty("picture").GetProperty("payload"));
        var targetIds = PictureIds(view.GetProperty("target").GetProperty("picture").GetProperty("payload"));
        foreach (var g in view.GetProperty("gadgets").EnumerateArray()) {
            Assert.All(Strings(g.GetProperty("sourceIds")), id => Assert.Contains(id, sourceIds));
            Assert.All(Strings(g.GetProperty("targetIds")), id => Assert.Contains(id, targetIds));
        }
        foreach (var f in view.GetProperty("frames").EnumerateArray()) {
            Assert.All(Strings(f.GetProperty("sourceFocus")), id => Assert.Contains(id, sourceIds));
            Assert.All(Strings(f.GetProperty("targetFocus")), id => Assert.Contains(id, targetIds));
        }
    }

    // Every gadget is built by exactly one build step, and names something on both sides except the bound (K), which comes from
    // no single piece of either.
    [Theory]
    [MemberData(nameof(CaseData))]
    public async Task Build_EveryGadgetIsBuiltByOneStep(string reduction, string instance) {
        var view = await View(reduction, instance);
        var used = Phase(view, "build").Where(f => f.TryGetProperty("gadgets", out _))
            .SelectMany(f => f.GetProperty("gadgets").EnumerateArray().Select(i => i.GetInt32())).ToList();
        Assert.Equal(used.Count, used.Distinct().Count());
        Assert.Equal(Enumerable.Range(0, view.GetProperty("gadgets").GetArrayLength()), used.Order());
        foreach (var g in view.GetProperty("gadgets").EnumerateArray()) {
            bool bound = g.GetProperty("gadgetKind").GetString() == "bound";
            Assert.Equal(!bound, g.GetProperty("sourceIds").GetArrayLength() > 0);
            Assert.Equal(!bound, g.GetProperty("targetIds").GetArrayLength() > 0);
        }
    }

    // ---- Build: replaying the steps gives exactly the target ----

    [Theory]
    [MemberData(nameof(CaseData))]
    public async Task Build_ReplayEqualsTheTarget(string reduction, string instance) {
        var view = await View(reduction, instance);
        var target = view.GetProperty("target");
        var (nodes, edges) = Pieces(target.GetProperty("picture").GetProperty("payload"));
        var order = target.GetProperty("buildOrder");
        var nodeOrder = Strings(order.GetProperty("nodes"));
        var edgeOrder = Strings(order.GetProperty("edges"));
        Assert.Equal(nodes.Order(StringComparer.Ordinal), nodeOrder.Order(StringComparer.Ordinal));
        Assert.Equal(edges.Order(StringComparer.Ordinal), edgeOrder.Order(StringComparer.Ordinal));

        var build = Phase(view, "build");
        int shownNodes = 0, shownEdges = 0;
        foreach (var frame in build) {
            int n = frame.GetProperty("target").GetProperty("builtNodes").GetInt32(), e = frame.GetProperty("target").GetProperty("builtEdges").GetInt32();
            Assert.True(n >= shownNodes && e >= shownEdges, "the target only grows");
            (shownNodes, shownEdges) = (n, e);
            var visible = nodeOrder.Take(n).Concat(edgeOrder.Take(e)).ToHashSet(StringComparer.Ordinal);
            Assert.All(Strings(frame.GetProperty("targetFocus")), id => Assert.True(visible.Contains(id), $"{id} is focused before it is built"));
        }
        Assert.Equal(nodes.Count, build[^1].GetProperty("target").GetProperty("builtNodes").GetInt32());
        Assert.Equal(edges.Count, build[^1].GetProperty("target").GetProperty("builtEdges").GetInt32());
    }

    // The steps' pieces, worked out here from the target problem itself rather than from its picture.
    private static (List<string> Nodes, List<string> Edges) TargetPieces(IProblem target) {
        static List<string> Distinct(IEnumerable<string> ids) => ids.Distinct(StringComparer.Ordinal).ToList();
        switch (target) {
            case API.Problems.NPComplete.NPC_VERTEXCOVER.VERTEXCOVER p:
                return (p.nodes, Distinct(p.edges.Where(e => e.Key != e.Value).Select(e => GraphSubsetFrames.EdgeId(e.Key, e.Value))));
            case API.Problems.NPComplete.NPC_CLIQUE.CLIQUE p:
                return (p.nodes, Distinct(p.edges.Where(e => e.Key != e.Value).Select(e => GraphSubsetFrames.EdgeId(e.Key, e.Value))));
            case API.Problems.NPComplete.NPC_CLIQUECOVER.CLIQUECOVER p:
                return (p.nodes, Distinct(p.edges.Where(e => e.Key != e.Value).Select(e => GraphSubsetFrames.EdgeId(e.Key, e.Value))));
            case API.Problems.NPComplete.NPC_HAMILTONIAN.HAMILTONIAN p:
                return (p.nodes, Distinct(p.edges.Where(e => e.Key != e.Value).Select(e => GraphSubsetFrames.EdgeId(e.Key, e.Value))));
            case API.Problems.NPComplete.NPC_ARCSET.ARCSET p:
                return (p.graph.Nodes.ToList().Select(n => LayeredFrames.NodeId(n.ToString())).ToList(),
                    Distinct(p.graph.Edges.ToList().Select(e => e.ToList()).Where(e => e[0].ToString() != e[1].ToString()).Select(e => LayeredFrames.ArcId(e[0].ToString(), e[1].ToString()))));
            case API.Problems.NPComplete.NPC_NODESET.NODESET p:
                return (p.nodes.Select(LayeredFrames.NodeId).ToList(),
                    Distinct(p.edges.Where(e => e.Key != e.Value).Select(e => LayeredFrames.ArcId(e.Key, e.Value))));
            case API.Problems.NPComplete.NPC_SETCOVER.SETCOVER p: {
                    var parsed = new API.Problems.NPComplete.NPC_SETCOVER.SETCOVER(p.instance);
                    return Bipartite(parsed.universal, parsed.subsets);
                }
            case API.Problems.NPComplete.NPC_EXACTCOVER.EXACTCOVER p: {
                    var parsed = new API.Problems.NPComplete.NPC_EXACTCOVER.EXACTCOVER(p.instance);
                    return Bipartite(parsed.X, parsed.S);
                }
            case SAT3 s:
                return Grouped(new SAT3(s.instance).clauses);
            case API.Problems.NPComplete.NPC_SAT.SAT s:
                return Grouped(new API.Problems.NPComplete.NPC_SAT.SAT(s.instance).clauses);
            default:
                throw new NotSupportedException(target.GetType().Name);
        }
    }

    private static (List<string>, List<string>) Bipartite(List<string> universe, List<List<string>> sets) =>
        (universe.Select(BipartiteFrames.ElementId).Concat(sets.Select((_, i) => BipartiteFrames.SetId(i))).ToList(),
         sets.SelectMany((set, i) => set.Distinct(StringComparer.Ordinal).Select(e => BipartiteFrames.EdgeId(BipartiteFrames.ElementId(e), BipartiteFrames.SetId(i)))).ToList());

    private static (List<string>, List<string>) Grouped(List<List<string>> clauses) {
        var payload = GroupedGraphFrames.Payload(GroupedGraphSpec.FromLiterals(clauses));
        return (payload.Groups.Select(g => g.Id).Concat(payload.Nodes.Select(n => n.Id)).Concat(payload.Variables.Select(v => v.Id)).ToList(),
            payload.Edges.Concat(payload.ClauseEdges).Select(e => e.Id).ToList());
    }

    [Theory]
    [MemberData(nameof(CaseData))]
    public void Build_ReplayedPiecesAreTheRealTargets(string reduction, string instance) {
        IReduction red = Build(reduction, instance);
        var steps = red.BuildRun(true)!.Steps;
        var nodes = steps.SelectMany(s => s.Partial.Nodes).ToList();
        var edges = steps.SelectMany(s => s.Partial.Edges).ToList();
        Assert.Equal(nodes.Count, nodes.Distinct().Count());
        Assert.Equal(edges.Count, edges.Select(e => e.Id).Distinct().Count());
        var (expectedNodes, expectedEdges) = TargetPieces(red.reductionTo);
        Assert.Equal(expectedNodes.Order(StringComparer.Ordinal), nodes.Order(StringComparer.Ordinal));
        Assert.Equal(expectedEdges.Order(StringComparer.Ordinal), edges.Select(e => e.Id).Order(StringComparer.Ordinal));
        // Every edge's ends are built by its own step or an earlier one.
        var built = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in steps) {
            built.UnionWith(step.Partial.Nodes);
            Assert.All(step.Partial.Edges, e => { Assert.Contains(e.A, built); Assert.Contains(e.B, built); });
        }
    }

    [Theory]
    [MemberData(nameof(CaseData))]
    public async Task Build_PanesOnlyHighlight(string reduction, string instance) {
        var view = await View(reduction, instance);
        foreach (var frame in Phase(view, "build"))
            foreach (string pane in new[] { "source", "target" })
                Assert.All(States(frame.GetProperty(pane)), s => Assert.Equal("ElementHighlight", s.State));
    }

    // ---- Stable gadgets ----

    [Theory]
    [MemberData(nameof(CaseData))]
    public void Gadgets_AreTheSame_AcrossBuilds_AndAfterReduceAgain(string reduction, string instance) {
        IReduction first = Build(reduction, instance), second = Build(reduction, instance);
        string gadgets = JsonSerializer.Serialize(first.gadgets);
        Assert.Equal(gadgets, JsonSerializer.Serialize(second.gadgets));
        first.reduce();
        Assert.Equal(gadgets, JsonSerializer.Serialize(first.gadgets));
        Assert.Equal(gadgets, JsonSerializer.Serialize(first.BuildRun(true)!.Gadgets));
        Assert.Equal(second.reductionTo.instance, first.reduce().instance);
    }

    [Theory]
    [MemberData(nameof(CaseData))]
    public void RecordedReduction_BuildsOnce_TheSameTargetGadgetsAndSteps(string reduction, string instance) {
        var rec = new StepRecorder<BuildDelta>(cap: int.MaxValue);
        var once = (IReduction)Activator.CreateInstance(ProblemProvider.Reductions[reduction.ToLower()], instance, rec)!;
        IReduction plain = Build(reduction, instance);
        Assert.Equal(plain.reductionTo.instance, once.reductionTo.instance);
        Assert.Equal(JsonSerializer.Serialize(plain.gadgets), JsonSerializer.Serialize(once.gadgets));
        Assert.Equal(JsonSerializer.Serialize(plain.BuildRun(true)!.Steps), JsonSerializer.Serialize(ConstructionSteps.Limit(rec.Steps)));
    }

    // Example captions, in both problems' words: one of the build and one of the map, per reduction.
    [Theory]
    [InlineData(CliqueToVc, "(({a,b,c,d},{{a,b},{b,c},{a,c},{c,d}}),3)", "build", "Node a has no edge to d in the clique graph, so it gets that edge in the complement.")]
    [InlineData(CliqueToVc, "(({a,b,c,d},{{a,b},{b,c},{a,c},{c,d}}),3)", "map", "Node a is not in the vertex cover, so it is in the clique.")]
    [InlineData(IsToClique, "(({a,b,c,d},{{a,b},{b,c},{c,d}}),2)", "build", "Node a has no edge to c, d in the original graph, so it gets those edges in the complement.")]
    [InlineData(IsToClique, "(({a,b,c,d},{{a,b},{b,c},{c,d}}),2)", "map", "Node a is in the clique of the complement graph, so it shares no edge with the others in the original: it is in the independent set.")]
    [InlineData(VcToArcSet, VcYes, "build", "Edge {a,b} becomes the arcs a1 → b0 and b1 → a0, closing the cycle a0 → a1 → b0 → b1 → a0. Removing a0 → a1 or b0 → b1 breaks it, as a or b covers the edge.")]
    [InlineData(VcToArcSet, VcYes, "map", "Arc b0 → b1 is removed. It is node b's own arc, so b is in the vertex cover.")]
    [InlineData(VcToNodeSet, VcYes, "build", "Edge {a,b} becomes the arcs a → b and b → a: a cycle of two, which only removing a or b breaks, as only a or b covers the edge.")]
    [InlineData(VcToNodeSet, VcYes, "map", "Node b is in the feedback node set, so it is in the vertex cover.")]
    [InlineData(VcToSetCover, VcYes, "build", "Node b becomes set S2, the edges it touches: {a_b,b_c}. Edge {b,c} is new, so it joins the elements to cover.")]
    [InlineData(VcToSetCover, VcYes, "map", "Set S2 {a_b,b_c} is in the cover. It is node b's set, the edges that touch b, so b is in the vertex cover.")]
    [InlineData(DirHamToHam, "({1,2,3},{(1,2),(2,3),(3,1)})", "build", "Arc 1 → 2 becomes the edge {13,21}: it leaves 1 by its out-end 13 and enters 2 by its in-end 21.")]
    [InlineData(DirHamToHam, "({1,2,3},{(1,2),(2,3),(3,1)})", "map", "Edge {13,21} is on the cycle. It leaves 1 by its out-end 13 and enters 2 by its in-end 21, so the tour goes 1 → 2.")]
    [InlineData(ColoringToCliqueCover, ColorYes, "build", "Node a has no edge to c in the graph, so it gets that edge in the complement.")]
    [InlineData(ColoringToCliqueCover, ColorYes, "map", "Node b is in clique 2 of the complement, so it takes color 2.")]
    [InlineData(ColoringToExactCover, ColorYes, "build", "Node a becomes the element a, 2 slots (one per edge at a and color) and 2 sets, one per color: the set for color j holds a and a's slots for color j. Exactly one of them covers a, so a gets one color.")]
    [InlineData(ColoringToExactCover, ColorYes, "map", "Set S1 {a,a_a_b_1} is in the exact cover. It is a's set for color 1, so a takes color 1.")]
    [InlineData(ColoringToSat, ColorYes, "build", "Edge {a,b} becomes 2 clauses, (!v0_0 | !v1_0), (!v0_1 | !v1_1): a and b never take the same color.")]
    [InlineData(ColoringToSat, ColorYes, "map", "v1_0 = True, so node b takes color 0.")]
    // #825: a node name that is not a SAT variable name still reduces; the variables are named by node position.
    [InlineData(ColoringToSat, "(({a-b,c},{{a-b,c}}),2)", "build", "Node a-b becomes the variables v0_0, v0_1, one per color, and 2 clauses: (v0_0 | v0_1) says a-b takes a color, and (!v0_0 | !v0_1) says it takes only one.")]
    public async Task Captions_SayWhatHappened(string reduction, string instance, string phase, string caption) {
        var view = await View(reduction, instance);
        Assert.Contains(caption, Phase(view, phase).Select(f => f.GetProperty("caption").GetString()));
    }

    [Fact]
    public async Task SipserCliqueToSat3_Captions_SayWhatHappened() {
        var view = await View(CliqueToSat3, Sipser("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)"));
        Assert.Contains("Nodes x1_0, !x2_0, x3_0 of the clique graph all end in _0, so they become clause C1 (x1 | !x2 | x3): each node is the literal written before its _0.",
            Phase(view, "build").Select(f => f.GetProperty("caption").GetString()));
        Assert.Contains("x1 = True, so x1 is true in clause C1: its node x1_0 is in the clique.", Phase(view, "map").Select(f => f.GetProperty("caption").GetString()));
    }

    // ---- Solve and map ----

    // The target has an answer exactly when the source does (worked out by hand for each case), and then the backward map
    // gives a correct source answer; the map's Done verdict is the source verifier's on the answer the source pane shows.
    [Theory]
    [MemberData(nameof(CaseAnswers))]
    public async Task YesMapsToYes_NoToNo_AndTheVerdictIsTheSourceVerifiers(string reduction, string instance, bool hasAnswer) {
        var view = await View(reduction, instance);
        IReduction red = Build(reduction, instance);
        bool solved = Phase(view, "solve")[^1].GetProperty("ok").GetBoolean();
        Assert.Equal(hasAnswer, solved);
        var map = Phase(view, "map");
        bool ok = map[^1].GetProperty("ok").GetBoolean();
        Assert.Equal(hasAnswer, ok);
        Assert.Equal(Verifies(red.reductionFrom, instance, ShownAnswer(map[^1].GetProperty("source"))), ok);
    }

    [Theory]
    [MemberData(nameof(CaseData))]
    public async Task Map_StepsReadTargetPieces_AndNameTheSourcePiecesTheyCameFrom(string reduction, string instance) {
        var view = await View(reduction, instance);
        var targetIds = PictureIds(view.GetProperty("target").GetProperty("picture").GetProperty("payload"));
        foreach (var frame in Phase(view, "map").SkipLast(1)) {
            var focus = Strings(frame.GetProperty("targetFocus"));
            Assert.Single(focus);
            Assert.Contains(focus[0], targetIds);
            Assert.NotEmpty(frame.GetProperty("sourceFocus").EnumerateArray());
        }
    }

    // Every solver of the target, on every case: 200, the map verdict is the source verifier's on what the source pane shows, and
    // a correct target answer maps back to a correct one. Some are approximations or randomized (Greedy Vertex Cover, WalkSAT),
    // so the failing paths are covered too. Grover's SAT solver asks an outside quantum service that the tests do not run.
    public static TheoryData<string, string, string> EverySolverCases() {
        var data = new TheoryData<string, string, string>();
        foreach (var (reduction, instance, _) in Cases()) {
            Type target = Build(reduction, instance).reductionTo.GetType();
            foreach (var (key, type) in ProblemProvider.Solvers.OrderBy(p => p.Key, StringComparer.Ordinal))
                if (!type.Name.Contains("Grover") && type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>) && i.GetGenericArguments()[0] == target))
                    data.Add(reduction, instance, key);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(EverySolverCases))]
    public async Task EverySolverOfTheTarget_GivesAView_WhoseVerdictsAreTheVerifiers(string reduction, string instance, string solver) {
        var view = await View(reduction, instance, solver);
        IReduction red = Build(reduction, instance);
        var map = Phase(view, "map");
        bool ok = map[^1].GetProperty("ok").GetBoolean();
        Assert.Equal(Verifies(red.reductionFrom, instance, ShownAnswer(map[^1].GetProperty("source"))), ok);
        if (Phase(view, "solve")[^1].GetProperty("ok").GetBoolean()) Assert.True(ok, map[^1].GetProperty("caption").GetString());
    }

    // ---- The backward map on its own endpoint ----

    // A correct target answer, made from a correct source answer by the forward map, maps back to a correct source answer.
    [Theory]
    [InlineData(CliqueToVc, "(({a,b,c,d},{{a,b},{b,c},{a,c},{c,d}}),3)", "{a,b,c}")]
    [InlineData(IsToClique, "(({a,b,c,d},{{a,b},{b,c},{c,d}}),2)", "{a,c}")]
    [InlineData(VcToArcSet, VcYes, "{b}")]
    [InlineData(VcToNodeSet, VcYes, "{b}")]
    [InlineData(VcToSetCover, VcYes, "{b}")]
    [InlineData(DirHamToHam, "({1,2,3},{(1,2),(2,3),(3,1)})", "{2,3,1,2}")]
    [InlineData(ColoringToCliqueCover, ColorYes, "{{a,c},{b}}")]
    [InlineData(ColoringToExactCover, ColorYes, "{{b},{a,c}}")]
    [InlineData(ColoringToSat, ColorYes, "{{a,c},{b}}")]
    public async Task MapSolutionBack_OfTheForwardMap_IsAcceptedBySourceVerifier(string reduction, string instance, string answer) {
        IReduction red = Build(reduction, instance);
        Assert.True(Verifies(red.reductionFrom, instance, answer), answer);
        string target = red.mapSolutions(answer);
        Assert.True(Verifies(red.reductionTo, red.reductionTo.instance, target), target);
        var (status, body) = await Post($"mapSolutionBack?reduction={reduction}&solution={Uri.EscapeDataString(target)}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        string back = JsonSerializer.Deserialize<string>(body)!;
        Assert.True(Verifies(red.reductionFrom, instance, back), back);
    }

    [Theory]
    [InlineData(CliqueToVc, "(({a,b,c},{{a,b}}),2)", "{a,zz}")]
    [InlineData(VcToArcSet, VcYes, "{(a0,b1)}")]
    [InlineData(VcToArcSet, VcYes, "{(a0,a1,b0)}")]
    [InlineData(VcToArcSet, VcYes, "a0,a1")]
    [InlineData(VcToNodeSet, VcYes, "{zz}")]
    [InlineData(VcToSetCover, VcYes, "{{a_b,zz}}")]
    [InlineData(VcToSetCover, VcYes, "{{a_b}")]
    [InlineData(DirHamToHam, "({1,2,3},{(1,2),(2,3),(3,1)})", "{11,99}")]
    [InlineData(ColoringToCliqueCover, ColorYes, "{{a,zz}}")]
    [InlineData(ColoringToCliqueCover, ColorYes, "a,b")]
    [InlineData(ColoringToExactCover, ColorYes, "{{a,b}}")]
    [InlineData(ColoringToSat, ColorYes, "(v0_0:maybe)")]
    [InlineData(ColoringToSat, ColorYes, "(q:True)")]
    public async Task MapSolutionBack_MalformedTargetAnswer_Is400(string reduction, string instance, string answer) {
        var (status, body) = await Post($"mapSolutionBack?reduction={reduction}&solution={Uri.EscapeDataString(answer)}", instance);
        Assert.True(status == HttpStatusCode.BadRequest, body);
    }

    [Fact]
    public async Task SipserCliqueToSat3_MalformedTargetAnswer_Is400() {
        string instance = Sipser("(x1 | !x2) & (x2)");
        foreach (string answer in new[] { "(x1:maybe)", "(q:True)" }) {
            var (status, body) = await Post($"mapSolutionBack?reduction={CliqueToSat3}&solution={Uri.EscapeDataString(answer)}", instance);
            Assert.True(status == HttpStatusCode.BadRequest, body);
        }
    }

    // ---- Bad input ----

    [Theory]
    // A clique graph Sipser's construction did not build: its node names say no clause.
    [InlineData(CliqueToSat3, "(({a,b},{{a,b}}),2)")]
    [InlineData(CliqueToSat3, "not a graph")]
    [InlineData(CliqueToVc, "(({a,b,c},{{a,b}}),4)")]
    [InlineData(IsToClique, "(({a,b},{{a,b}}")]
    [InlineData(VcToArcSet, "(({a,b},{{a,c}}),1)")]
    [InlineData(VcToNodeSet, "")]
    [InlineData(VcToSetCover, "(({a,b},{{a,b}}),x)")]
    [InlineData(DirHamToHam, "({1,2},{(1,3)})")]
    [InlineData(ColoringToCliqueCover, "(({a,b},{{a,b}}))")]
    [InlineData(ColoringToExactCover, "nonsense")]
    // Graph Coloring to SAT cannot write a formula for no nodes or no colors (#827).
    [InlineData(ColoringToSat, "(({},{}),1)")]
    [InlineData(ColoringToSat, "(({a},{}),0)")]
    public async Task BadSourceInstance_Is400(string reduction, string instance) {
        var (status, body) = await Post($"reductionView?reduction={reduction}", instance);
        Assert.True(status == HttpStatusCode.BadRequest, body);
        Assert.Contains(JsonDocument.Parse(body).RootElement.GetProperty("error").GetString(), new[] { "instance_parse_error", "reduction_input_parse_error" });
    }

    [Theory]
    [InlineData(CliqueToVc, "cliquebruteforce")]
    [InlineData(VcToArcSet, "nodesetbruteforce")]
    [InlineData(VcToSetCover, "exactcoverbruteforce")]
    [InlineData(ColoringToSat, "sat3backtrackingsolver")]
    [InlineData(DirHamToHam, "nosuchsolver")]
    public async Task SolverThatDoesNotSolveTheTarget_Is400(string reduction, string solver) {
        var (status, body) = await Post($"reductionView?reduction={reduction}&solver={solver}", reduction == DirHamToHam ? "({1,2},{(1,2),(2,1)})" : VcYes.Replace(",1)", ",2)"));
        Assert.True(status == HttpStatusCode.BadRequest, body);
        Assert.Contains(JsonDocument.Parse(body).RootElement.GetProperty("error").GetString(), new[] { "solver_mismatch", "unknown_solver" });
    }

    // ---- The endpoint's JSON ----

    private static string SnapshotPath(string file) => Path.Combine(ProjectSourcePath.Value, "redux-tests", "Endpoints", "Snapshots", file);

    // One whole payload per reduction, on its first case (a small instance with an answer). Regenerate deliberately with
    // REDUX_UPDATE_SNAPSHOTS=1.
    public static TheoryData<string> Reductions() {
        var data = new TheoryData<string>();
        foreach (string r in All) data.Add(r);
        return data;
    }

    [Theory]
    [MemberData(nameof(Reductions))]
    public async Task ReductionView_Json_MatchesTheSnapshot(string reduction) {
        string instance = Cases().First(c => c.Reduction == reduction).Instance;
        var (status, body) = await Post($"reductionView?reduction={reduction}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        string pretty = JsonSerializer.Serialize(JsonDocument.Parse(body).RootElement, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n");
        string file = $"reductionview.{reduction.ToLower()}.json";
        if (Environment.GetEnvironmentVariable("REDUX_UPDATE_SNAPSHOTS") == "1") {
            File.WriteAllText(SnapshotPath(file), pretty);
            return;
        }
        Assert.Equal(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file)).Replace("\r\n", "\n"), pretty);
    }
}

// The Reduction View of the graph reductions with a 50 ms solve budget: Graph Coloring's default graph (9 nodes, 3 colors)
// gives SAT 27 variables and Clique Cover 3^9 ways to split the nodes, far past what their brute forces finish (Exact
// Cover's solvers prune too well to be stopped on it), so the solve phase ends with its stopped frame and the map phase says
// there is nothing to map back.
public class ReductionView_Graph_TimeLimit_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public ReductionView_Graph_TimeLimit_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData(ReductionView_Graph_Tests.ColoringToSat, "SAT")]
    [InlineData(ReductionView_Graph_Tests.ColoringToCliqueCover, "Clique Cover")]
    public async Task DefaultGraph_StoppedSolve_EndsWithTheStoppedFrame_AndSkipsTheMap(string reduction, string target) {
        string instance = new API.Problems.NPComplete.NPC_GRAPHCOLORING.GRAPHCOLORING().defaultInstance;
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/reductionView?reduction={reduction}", content, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        var build = frames.Where(f => f.GetProperty("phase").GetString() == "build").ToList();
        Assert.True(build[^1].GetProperty("ok").GetBoolean());
        var solve = frames.Where(f => f.GetProperty("phase").GetString() == "solve").ToList();
        Assert.False(solve[^1].GetProperty("ok").GetBoolean());
        Assert.StartsWith("Stopped after 0.05 s and ", solve[^1].GetProperty("caption").GetString());
        // The stopped Done frame is blank: nothing is drawn as an answer.
        Assert.Empty(ReductionView_Graph_Tests.States(solve[^1].GetProperty("target")));
        var map = Assert.Single(frames, f => f.GetProperty("phase").GetString() == "map");
        Assert.False(map.GetProperty("ok").GetBoolean());
        Assert.Equal($"The solver had not finished, so there is no {target} answer to map back.", map.GetProperty("caption").GetString());
    }
}
