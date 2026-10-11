using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Packing;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Reduction View (#692) of the set and number reductions (#695), on the real /ProblemProvider/reductionView output. Their
// panes use three pictures: Packing (Subset Sum, Knapsack, Partition), Bipartite Graph (Exact Cover, Hitting Set) and Graph
// (Weighted Cut), so these tests read each payload by its kind, as ReductionView_Graph_Tests does.
public class ReductionView_SetNumber_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public ReductionView_SetNumber_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    public const string EcToSs = "KarpExactCoverToSubsetSum", HsToEc = "reduceToEXACTCOVER", PartitionToCut = "WEIGHTEDCUTReduction",
        SsToKnapsack = "FengReduction", SsToPartition = "SubsetSumToPartitionReduction";

    public static readonly string[] All = [EcToSs, HsToEc, PartitionToCut, SsToKnapsack, SsToPartition];

    // Each reduction's source and target picture types.
    public static readonly Dictionary<string, (string Source, string Target)> Pictures = new(StringComparer.OrdinalIgnoreCase) {
        [EcToSs] = ("BipartiteGraph", "Packing"),
        [HsToEc] = ("BipartiteGraph", "BipartiteGraph"),
        [PartitionToCut] = ("Packing", "Graph"),
        [SsToKnapsack] = ("Packing", "Packing"),
        [SsToPartition] = ("Packing", "Packing"),
    };

    // Instances small enough for every solver of the target, each with whether the source has an answer, worked out by hand:
    // yes and no instances, empty and one-piece ones, and names that hold the pictures' separators.
    public static IEnumerable<(string Reduction, string Instance, bool HasAnswer)> Cases() {
        // {2,3} and {4,1} cover 1..4 once each; three pairs from {1,2,3} always overlap or leave a gap.
        yield return (EcToSs, "({1,2,3,4},{{1,2,3},{2,3},{4,1}})", true);
        yield return (EcToSs, "({1,2,3},{{1,2},{2,3},{1,3}})", false);
        yield return (EcToSs, "({1,2,3},{{1,2},{2,3}})", false);
        yield return (EcToSs, "({a,b,c},{{a},{b,c},{a,b}})", true);
        // ~ is a separator of the Bipartite Graph's ids, escaped there. (A name with ':' would be one too, but Exact Cover's verifier
        // cuts a certificate at its first ':', #833.)
        yield return (EcToSs, "({a~b,c},{{a~b},{c}})", true);
        // 1 and 2 hit each subset of the default once; any items of {1,2,3} hit some pair twice or not at all; a and b are in
        // the same subsets (one set in Exact Cover) and d in none (the empty set); with no subsets the empty set hits them all.
        yield return (HsToEc, "({1,2,3,4},{{1,3},{2,3,4},{1,4}})", true);
        yield return (HsToEc, "({1,2,3},{{1,2},{2,3},{1,3}})", false);
        yield return (HsToEc, "({a,b,c,d},{{a,b},{c}})", true);
        yield return (HsToEc, "({1,2},{{1},{2},{1,2}})", false);
        yield return (HsToEc, "({1},{})", true);
        // 3 against 1 + 2; an odd total; one number; 6 against 1 + 5; 14 with no half from 2, 4 and 8.
        yield return (PartitionToCut, "{1,2,3}", true);
        yield return (PartitionToCut, "{1,2}", false);
        yield return (PartitionToCut, "{4}", false);
        yield return (PartitionToCut, "{1,5,6}", true);
        yield return (PartitionToCut, "{2,4,8}", false);
        // 1 + 12 + 15 = 28; 3 + 5 = 8; nothing adds to 4; T = 0 is the empty subset.
        yield return (SsToKnapsack, "({1,7,12,15},28)", true);
        yield return (SsToKnapsack, "({3,5,9},8)", true);
        yield return (SsToKnapsack, "({3,5,9},4)", false);
        yield return (SsToKnapsack, "({5},0)", true);
        // 1 + 12 + 15 = 28; 2 + 9 = 11; nothing adds to 10; T = 0; T past the sum (sum - T + 1 is negative).
        yield return (SsToPartition, "({1,7,12,15},28)", true);
        yield return (SsToPartition, "({2,5,9},11)", true);
        yield return (SsToPartition, "({2,5,9},10)", false);
        yield return (SsToPartition, "({4,6},0)", true);
        yield return (SsToPartition, "({3,5},20)", false);
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
    // what is drawn between two of those (edges). The Packing picture's items and containers all stand on their own.
    internal static (List<string> Nodes, List<string> Edges) Pieces(JsonElement payload) => payload.GetProperty("kind").GetString() switch {
        "packing" => (Ids(payload, "items").Concat(Ids(payload, "containers")).ToList(), []),
        "graph" => (Ids(payload, "nodes").ToList(), Ids(payload, "edges").ToList()),
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

    private static string Braces(IEnumerable<string> items) => "{" + string.Join(",", items) + "}";

    // The answer a source pane shows, as certificate text, read from the picture (not from the backward map): the chosen items of
    // a Subset Sum, the two sides of a Partition (by the pane's assign), the chosen sets of an Exact Cover or the chosen elements of
    // a Hitting Set (by their Solution state).
    private static string ShownAnswer(JsonElement payload, JsonElement pane) {
        switch (payload.GetProperty("kind").GetString()) {
            case "packing": {
                    var labels = payload.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("label").GetString()!).ToList();
                    var assign = pane.GetProperty("assign").EnumerateArray().Select(a => a.GetInt32()).ToList();
                    IEnumerable<string> In(int c) => labels.Where((_, i) => assign[i] == c);
                    return payload.GetProperty("rule").GetString() == "partition"
                        ? "{(" + string.Join(",", In(0)) + "),(" + string.Join(",", In(1)) + ")}"
                        : Braces(In(0));
                }
            case "bipartiteGraph": {
                    var solution = pane.GetProperty("nodes").EnumerateObject().Where(p => p.Value.GetProperty("state").GetString() == "Solution")
                        .Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
                    var label = payload.GetProperty("elements").EnumerateArray().ToDictionary(e => e.GetProperty("id").GetString()!, e => e.GetProperty("label").GetString()!);
                    if (payload.GetProperty("pickSide").GetString() == "elements")
                        return Braces(label.Where(e => solution.Contains(e.Key)).Select(e => e.Value));
                    return Braces(payload.GetProperty("sets").EnumerateArray().Where(s => solution.Contains(s.GetProperty("id").GetString()!))
                        .Select(s => Braces(Strings(s.GetProperty("members")).Select(m => label[m]))));
                }
            default:
                throw new NotSupportedException(payload.GetProperty("kind").GetString());
        }
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
        Assert.Equal(Pictures[reduction].Source, view.GetProperty("source").GetProperty("picture").GetProperty("type").GetString());
        Assert.Equal(Pictures[reduction].Target, view.GetProperty("target").GetProperty("picture").GetProperty("type").GetString());
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

    // Every gadget is built by exactly one build step and names the source pieces it came from. Every one names its target pieces
    // too, except Weighted Cut's bound: K is a number of the instance, which the Graph picture draws as no piece of its own.
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
            Assert.True(g.GetProperty("sourceIds").GetArrayLength() > 0, g.GetRawText());
            Assert.True(g.GetProperty("targetIds").GetArrayLength() > 0 || (bound && reduction == PartitionToCut), g.GetRawText());
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

    // The steps' pieces, worked out here from the target problem itself (its own instance text) rather than from its picture.
    private static (List<string> Nodes, List<string> Edges) TargetPieces(IProblem target) {
        static List<string> Items(int count, int containers) =>
            Enumerable.Range(0, count).Select(PackingFrames.ItemId).Concat(Enumerable.Range(0, containers).Select(PackingFrames.ContainerId)).ToList();
        switch (target) {
            case API.Problems.NPComplete.NPC_SUBSETSUM.SUBSETSUM p:
                return (Items(new API.Problems.NPComplete.NPC_SUBSETSUM.SUBSETSUM(p.instance).S.Count, 1), []);
            case API.Problems.NPComplete.NPC_KNAPSACK.KNAPSACK p:
                return (Items(new API.Problems.NPComplete.NPC_KNAPSACK.KNAPSACK(p.instance).items.ToList().Count, 1), []);
            case API.Problems.NPComplete.NPC_PARTITION.PARTITION p:
                return (Items(new API.Problems.NPComplete.NPC_PARTITION.PARTITION(p.instance).S.Count, 2), []);
            case API.Problems.NPComplete.NPC_WEIGHTEDCUT.WEIGHTEDCUT p: {
                    var parsed = new API.Problems.NPComplete.NPC_WEIGHTEDCUT.WEIGHTEDCUT(p.instance);
                    return (parsed.nodes, parsed.edges.Where(e => e.source != e.destination).Select(e => GraphSubsetFrames.EdgeId(e.source, e.destination)).Distinct(StringComparer.Ordinal).ToList());
                }
            case API.Problems.NPComplete.NPC_EXACTCOVER.EXACTCOVER p: {
                    var parsed = new API.Problems.NPComplete.NPC_EXACTCOVER.EXACTCOVER(p.instance);
                    return (parsed.X.Select(BipartiteFrames.ElementId).Concat(parsed.S.Select((_, i) => BipartiteFrames.SetId(i))).ToList(),
                        parsed.S.SelectMany((set, i) => set.Distinct(StringComparer.Ordinal).Select(e => BipartiteFrames.EdgeId(BipartiteFrames.ElementId(e), BipartiteFrames.SetId(i)))).ToList());
                }
            default:
                throw new NotSupportedException(target.GetType().Name);
        }
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
        // The bound, when the target has one, is the target's own.
        int? bound = steps.LastOrDefault(s => s.Partial.Bound != null)?.Partial.Bound;
        switch (red.reductionTo) {
            case API.Problems.NPComplete.NPC_SUBSETSUM.SUBSETSUM p: Assert.Equal(p.T, bound); break;
            case API.Problems.NPComplete.NPC_KNAPSACK.KNAPSACK p: Assert.Equal(p.W, bound); Assert.Equal(p.V, bound); break;
            case API.Problems.NPComplete.NPC_WEIGHTEDCUT.WEIGHTEDCUT p: Assert.Equal(p.K, bound); break;
            default: Assert.Null(bound); break;
        }
    }

    // While the target is built nothing is chosen or judged: no element is Solution or Rejected, and the focus is highlighted.
    [Theory]
    [MemberData(nameof(CaseData))]
    public async Task Build_PanesOnlyHighlight(string reduction, string instance) {
        var view = await View(reduction, instance);
        foreach (var frame in Phase(view, "build")) {
            foreach (string pane in new[] { "source", "target" })
                Assert.All(States(frame.GetProperty(pane)), s => Assert.DoesNotContain(s.State, new[] { "Solution", "Rejected", "Blocked" }));
            var highlighted = States(frame.GetProperty("target")).Where(s => s.State == "ElementHighlight").Select(s => s.Id).ToHashSet();
            Assert.All(Strings(frame.GetProperty("targetFocus")), id => Assert.Contains(id, highlighted));
        }
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
    [InlineData(EcToSs, "({1,2,3,4},{{1,2,3},{2,3},{4,1}})", "build", "Set S2 {2,3} becomes the number 20: 0110 in base 4, one digit per element of X (4, 3, 2, 1 from the left), a 1 where S2 holds that element.")]
    [InlineData(EcToSs, "({1,2,3,4},{{1,2,3},{2,3},{4,1}})", "map", "Number 20 is in the subset, so set S2 {2,3} is in the exact cover: 20 is S2 written in base 4.")]
    [InlineData(HsToEc, "({1,2,3,4},{{1,3},{2,3,4},{1,4}})", "build", "Item 1 is in subsets S1, S3, so it becomes the set S1 {1,3} of their elements: taking 1 into the hitting set covers each of them once.")]
    [InlineData(HsToEc, "({1,2,3,4},{{1,3},{2,3,4},{1,4}})", "map", "Set S1 {1,3} is in the exact cover. It is item 1's set, the subsets that hold 1, so 1 is in the hitting set.")]
    [InlineData(PartitionToCut, "{1,2,3}", "build", "Number 3 becomes node 3, joined to nodes 1, 2 by edges weighing the product of their numbers: 1 * 3 = 3, 2 * 3 = 6.")]
    [InlineData(PartitionToCut, "{1,2,3}", "map", "Edge {1,3} is cut, so node 3 is across from node 1: 3 goes to side S2.")]
    [InlineData(SsToKnapsack, "({1,7,12,15},28)", "build", "Number 7 becomes the item (7,7): weight 7 and value 7, so it adds as much to the knapsack's weight as to its value.")]
    [InlineData(SsToKnapsack, "({1,7,12,15},28)", "map", "Item (12,12) is in the knapsack, so 12 is in the subset.")]
    [InlineData(SsToPartition, "({1,7,12,15},28)", "build", "Partition gets the number sum - T + 1 = 8. On its side it needs numbers of S adding up to (sum + 1) - (sum - T + 1) = 28: a subset that adds up to T.")]
    [InlineData(SsToPartition, "({1,7,12,15},28)", "map", "Number 7 is on side S1 with 29 (T + 1), so 7 is left out of the subset.")]
    public async Task Captions_SayWhatHappened(string reduction, string instance, string phase, string caption) {
        var view = await View(reduction, instance);
        Assert.Contains(caption, Phase(view, phase).Select(f => f.GetProperty("caption").GetString()));
    }

    // ---- Solve and map ----

    // The target has an answer exactly when the source does (worked out by hand for each case), and then the backward map gives a
    // correct source answer; the map's Done verdict is the source verifier's on the answer the source pane shows.
    [Theory]
    [MemberData(nameof(CaseAnswers))]
    public async Task YesMapsToYes_NoToNo_AndTheVerdictIsTheSourceVerifiers(string reduction, string instance, bool hasAnswer) {
        var view = await View(reduction, instance);
        IReduction red = Build(reduction, instance);
        Assert.Equal(hasAnswer, Phase(view, "solve")[^1].GetProperty("ok").GetBoolean());
        var map = Phase(view, "map");
        bool ok = map[^1].GetProperty("ok").GetBoolean();
        Assert.Equal(hasAnswer, ok);
        var payload = view.GetProperty("source").GetProperty("picture").GetProperty("payload");
        Assert.Equal(Verifies(red.reductionFrom, instance, ShownAnswer(payload, map[^1].GetProperty("source"))), ok);
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
    // a correct target answer maps back to a correct one. Fast Approximation (Subset Sum) can miss, so the failing path is covered
    // too.
    public static TheoryData<string, string, string> EverySolverCases() {
        var data = new TheoryData<string, string, string>();
        foreach (var (reduction, instance, _) in Cases()) {
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
        var map = Phase(view, "map");
        bool ok = map[^1].GetProperty("ok").GetBoolean();
        var payload = view.GetProperty("source").GetProperty("picture").GetProperty("payload");
        Assert.Equal(Verifies(red.reductionFrom, instance, ShownAnswer(payload, map[^1].GetProperty("source"))), ok);
        if (Phase(view, "solve")[^1].GetProperty("ok").GetBoolean()) Assert.True(ok, map[^1].GetProperty("caption").GetString());
    }

    // ---- The backward map on its own endpoint ----

    // A correct target answer, made from a correct source answer by the forward map, maps back to a correct source answer.
    [Theory]
    [InlineData(EcToSs, "({1,2,3,4},{{1,2,3},{2,3},{4,1}})", "{{2,3},{4,1}}")]
    [InlineData(EcToSs, "({a,b,c},{{a},{b,c},{a,b}})", "{{a},{b,c}}")]
    [InlineData(HsToEc, "({1,2,3,4},{{1,3},{2,3,4},{1,4}})", "{1,2}")]
    [InlineData(HsToEc, "({a,b,c,d},{{a,b},{c}})", "{b,c,d}")]
    [InlineData(PartitionToCut, "{1,2,3}", "{(3),(1,2)}")]
    [InlineData(PartitionToCut, "{4,5,6,7}", "{(4,7),(5,6)}")]
    [InlineData(SsToKnapsack, "({1,7,12,15},28)", "{1,12,15}")]
    [InlineData(SsToKnapsack, "({5},0)", "{}")]
    [InlineData(SsToPartition, "({1,7,12,15},28)", "{1,12,15}")]
    [InlineData(SsToPartition, "({4,6},0)", "{}")]
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
    [InlineData(EcToSs, "({1,2,3,4},{{1,2,3},{2,3},{4,1}})", "{99}")]
    [InlineData(EcToSs, "({1,2,3,4},{{1,2,3},{2,3},{4,1}})", "{21")]
    [InlineData(EcToSs, "({1,2,3,4},{{1,2,3},{2,3},{4,1}})", "21")]
    [InlineData(HsToEc, "({1,2,3,4},{{1,3},{2,3,4},{1,4}})", "{{9}}")]
    [InlineData(HsToEc, "({1,2,3,4},{{1,3},{2,3,4},{1,4}})", "{1}")]
    [InlineData(PartitionToCut, "{1,2,3}", "{({1,9},3)}")]
    [InlineData(PartitionToCut, "{1,2,3}", "{(1,2)}")]
    [InlineData(PartitionToCut, "{1,2,3}", "nonsense")]
    [InlineData(SsToKnapsack, "({1,7,12,15},28)", "{(2,2)}")]
    [InlineData(SsToKnapsack, "({1,7,12,15},28)", "{(1,1)")]
    [InlineData(SsToPartition, "({1,7,12,15},28)", "{(1,7),(12,15)}")]
    [InlineData(SsToPartition, "({1,7,12,15},28)", "nonsense")]
    public async Task MapSolutionBack_MalformedTargetAnswer_Is400(string reduction, string instance, string answer) {
        var (status, body) = await Post($"mapSolutionBack?reduction={reduction}&solution={Uri.EscapeDataString(answer)}", instance);
        Assert.True(status == HttpStatusCode.BadRequest, body);
    }

    // ---- Bad input ----

    [Theory]
    [InlineData(EcToSs, "nonsense")]
    [InlineData(EcToSs, "({1,2},{{3}})")]
    [InlineData(HsToEc, "({1,2},{{3}})")]
    [InlineData(HsToEc, "")]
    [InlineData(PartitionToCut, "{a,b}")]
    [InlineData(PartitionToCut, "{}")]
    [InlineData(SsToKnapsack, "({1,2},x)")]
    // Knapsack's weights are sizes and cannot be negative (#793), so a negative number has no item to become.
    [InlineData(SsToKnapsack, "({-1,2},1)")]
    [InlineData(SsToPartition, "({1,2}")]
    // T + 1 = 4 repeats a number of S, which Partition would read once (#831).
    [InlineData(SsToPartition, "({3,4},3)")]
    public async Task BadSourceInstance_Is400(string reduction, string instance) {
        var (status, body) = await Post($"reductionView?reduction={reduction}", instance);
        Assert.True(status == HttpStatusCode.BadRequest, body);
        Assert.Contains(JsonDocument.Parse(body).RootElement.GetProperty("error").GetString(), new[] { "instance_parse_error", "reduction_input_parse_error" });
    }

    // Numbers past an int: a 400 that says why, never a 500 or a number that wrapped around (#829, #830).
    [Theory]
    [InlineData(EcToSs, "({1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20},{{1,2},{3},{20}})", "20 digits in base 4")]
    [InlineData(PartitionToCut, "{60000,50000}", "does not fit Weighted Cut's integers")]
    [InlineData(SsToPartition, "({1},2147483647)", "must fit Partition's integers")]
    public async Task NumbersPastAnInt_Are400_ThatSayWhy(string reduction, string instance, string detail) {
        foreach (string endpoint in new[] { "reductionView", "buildSteps", "reduce" }) {
            var (status, body) = await Post($"{endpoint}?reduction={reduction}", instance);
            Assert.True(status == HttpStatusCode.BadRequest, $"{endpoint}: {body}");
            using var doc = JsonDocument.Parse(body);
            Assert.Equal("reduction_input_parse_error", doc.RootElement.GetProperty("error").GetString());
            Assert.Contains(detail, doc.RootElement.GetProperty("detail").GetString());
        }
    }

    [Theory]
    [InlineData(EcToSs, "knapsackbruteforce")]
    [InlineData(HsToEc, "hittingsetbruteforce")]
    [InlineData(PartitionToCut, "cutbruteforce")]
    [InlineData(SsToKnapsack, "subsetsumbruteforce")]
    [InlineData(SsToPartition, "nosuchsolver")]
    public async Task SolverThatDoesNotSolveTheTarget_Is400(string reduction, string solver) {
        string instance = Cases().First(c => c.Reduction == reduction).Instance;
        var (status, body) = await Post($"reductionView?reduction={reduction}&solver={solver}", instance);
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

// The Reduction View of the set and number reductions with a 50 ms solve budget, on instances whose brute force tries far more
// than it can in that time: 2^28 subsets of 28 numbers (Subset Sum, Knapsack, Partition) and 2^22 ways to cut 23 nodes. The solve
// phase ends with its stopped frame, and the map phase says there is nothing to map back.
public class ReductionView_SetNumber_TimeLimit_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public ReductionView_SetNumber_TimeLimit_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    // Powers of two from 1 to 2^27 add up to 2^28 - 1, and every subset has its own sum; T = 2^28 is out of reach, so a brute
    // force tries all 2^28 subsets.
    private static readonly string Powers = "{" + string.Join(",", Enumerable.Range(0, 28).Select(i => 1 << i)) + "}";

    // 28 sets of {1,...,6}, each holding 1 with some of 2..6 but never all of them: no two are disjoint and none covers X alone, so
    // there is no exact cover, and their numbers (6 digits in base 29, at most 29^6) never add up to T.
    private static readonly string Overlapping = "({1,2,3,4,5,6},{" + string.Join(",", Enumerable.Range(0, 28)
        .Select(mask => "{" + string.Join(",", new[] { 1 }.Concat(Enumerable.Range(0, 5).Where(b => (mask & (1 << b)) != 0).Select(b => b + 2))) + "}")) + "})";

    public static TheoryData<string, string, string, string> Slow() => new() {
        { ReductionView_SetNumber_Tests.EcToSs, Overlapping, "subsetsumbruteforce", "Subset Sum" },
        { ReductionView_SetNumber_Tests.SsToKnapsack, $"({Powers},{1 << 28})", "knapsackbruteforce", "Knapsack (Binary)" },
        // T = 2^28 adds 2^28 + 1 and 0, and 2^28 + 1 is more than half of Partition's total (2^29): no split, all 2^30 tried.
        { ReductionView_SetNumber_Tests.SsToPartition, $"({Powers},{1 << 28})", "partitionbruteforce", "Partition" },
        { ReductionView_SetNumber_Tests.PartitionToCut, "{" + string.Join(",", Enumerable.Range(1, 23).Select(i => 2 * i - 1).Append(1000)) + "}", "weightedcutbruteforce", "Weighted Cut" },
    };

    [Theory]
    [MemberData(nameof(Slow))]
    public async Task StoppedSolve_EndsWithTheStoppedFrame_AndSkipsTheMap(string reduction, string instance, string solver, string target) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/reductionView?reduction={reduction}&solver={solver}", content, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Where(f => f.GetProperty("phase").GetString() == "build").Last().GetProperty("ok").GetBoolean());
        var solve = frames.Where(f => f.GetProperty("phase").GetString() == "solve").ToList();
        Assert.False(solve[^1].GetProperty("ok").GetBoolean());
        Assert.StartsWith("Stopped after 0.05 s and ", solve[^1].GetProperty("caption").GetString());
        // The stopped Done frame is blank: nothing is drawn as an answer.
        Assert.Empty(ReductionView_SetNumber_Tests.States(solve[^1].GetProperty("target")));
        var map = Assert.Single(frames, f => f.GetProperty("phase").GetString() == "map");
        Assert.False(map.GetProperty("ok").GetBoolean());
        Assert.Equal($"The solver had not finished, so there is no {target} answer to map back.", map.GetProperty("caption").GetString());
    }
}
