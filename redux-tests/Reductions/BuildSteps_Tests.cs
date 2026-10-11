using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_VERTEXCOVER;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Construction steps (#691): what a reduction records while it builds its target.
public class BuildSteps_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public BuildSteps_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private const string Sat = "SipserReduceToCliqueStandard", Vc = "sipserReductionVertexCover", Is = "reduceToCLIQUE";

    private static IReduction Build(string reduction, string instance) =>
        (IReduction)Activator.CreateInstance(ProblemProvider.Reductions[reduction.ToLower()], instance)!;

    // The same instances the /reduce and /gadgets snapshots pin.
    public static TheoryData<string, string> Cases() {
        var data = new TheoryData<string, string>();
        foreach (var (reduction, instance) in Reduction_Output_Snapshot_Tests.Cases()) data.Add(reduction, instance);
        return data;
    }

    private static string GadgetJson(IEnumerable<Gadget> gadgets) =>
        JsonSerializer.Serialize(gadgets, new JsonSerializerOptions { WriteIndented = true });

    private static string Pair(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;

    // Target nodes, edges (as unordered pairs) and bound, whichever problem the reduction targets.
    private static (List<string> Nodes, List<string> Edges, int K) TargetOf(IProblem target) => target switch {
        CLIQUE c => (c.nodes, c.edges.Select(e => Pair(e.Key, e.Value)).ToList(), c.K),
        VERTEXCOVER v => (v.nodes, v.edges.Select(e => Pair(e.Key, e.Value)).ToList(), v.K),
        _ => throw new NotSupportedException(target.GetType().Name)
    };

    // The Graph picture's edge id (GraphSubsetFrames.EdgeId): ends ordered by ordinal, '\', '-' and '>' escaped.
    // Written out here rather than calling GraphSubsetFrames.EdgeId, which BuildEdge now calls itself: the test would
    // otherwise compare that function with itself.
    private static string PictureEdgeId(string a, string b) {
        static string Esc(string n) => n.Replace(@"\", @"\\").Replace("-", @"\-").Replace(">", @"\>");
        return string.CompareOrdinal(a, b) <= 0 ? Esc(a) + "-" + Esc(b) : Esc(b) + "-" + Esc(a);
    }

    // Replays the steps in order, checking as it goes that every edge joins two nodes already built and is announced once.
    private static (List<string> Nodes, List<string> Edges, int? Bound) Replay(IEnumerable<SolverStep<BuildDelta>> steps) {
        var nodes = new List<string>();
        var built = new HashSet<string>(StringComparer.Ordinal);
        var edges = new List<string>();
        int? bound = null;
        foreach (var step in steps) {
            foreach (string n in step.Partial.Nodes) { nodes.Add(n); built.Add(n); }
            foreach (var e in step.Partial.Edges) {
                Assert.True(built.Contains(e.A) && built.Contains(e.B), $"edge {e.Id} is added before its ends: {step.Caption}");
                edges.Add(Pair(e.A, e.B));
            }
            bound = step.Partial.Bound ?? bound;
        }
        return (nodes, edges, bound);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void RecordingOnOrOff_BuildsTheSameTargetAndGadgets(string reduction, string instance) {
        IReduction red = Build(reduction, instance);
        Assert.True(red.hasBuildSteps());
        ConstructionRun on = red.BuildRun(true)!;
        ConstructionRun off = red.BuildRun(false)!;

        Assert.Equal(red.reductionTo.instance, on.Target.instance);
        Assert.Equal(red.reductionTo.instance, off.Target.instance);
        Assert.Equal(GadgetJson(red.gadgets), GadgetJson(on.Gadgets));
        Assert.Equal(GadgetJson(red.gadgets), GadgetJson(off.Gadgets));
        Assert.Empty(off.Steps);
        Assert.NotEmpty(on.Steps);
        // Asking for steps rebuilds on a copy: the reduction's own gadgets must not grow.
        Assert.Equal(GadgetJson(Build(reduction, instance).gadgets), GadgetJson(red.gadgets));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ReplayingTheSteps_ReproducesTheTarget(string reduction, string instance) {
        IReduction red = Build(reduction, instance);
        ConstructionRun run = red.BuildRun(true)!;
        var (nodes, edges, k) = TargetOf(red.reductionTo);
        var (builtNodes, builtEdges, bound) = Replay(run.Steps);
        Assert.Equal(nodes.Distinct().OrderBy(n => n, StringComparer.Ordinal), builtNodes.Distinct().OrderBy(n => n, StringComparer.Ordinal));
        // No edge is announced twice, and none is missing.
        Assert.Equal(builtEdges.Count, builtEdges.Distinct().Count());
        Assert.Equal(edges.Distinct().OrderBy(e => e, StringComparer.Ordinal), builtEdges.OrderBy(e => e, StringComparer.Ordinal));
        Assert.Equal(k, bound);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryGadgetIsAnnouncedByAStep(string reduction, string instance) {
        IReduction red = Build(reduction, instance);
        ConstructionRun run = red.BuildRun(true)!;
        var announced = run.Steps.SelectMany(s => s.Partial.Gadgets).ToList();
        Assert.Equal(Enumerable.Range(0, run.Gadgets.Count), announced.Distinct().OrderBy(i => i));
        // And the step's focus, or the edges it adds (an edge rule's targets, #694), holds the target ids of the gadgets it announces.
        foreach (var step in run.Steps.Where(s => s.Partial.Gadgets.Length > 0)) {
            var announcedTargets = step.Partial.Gadgets.SelectMany(i => run.Gadgets[i].targetIds);
            var shown = step.Focus.Concat(step.Partial.Edges.Select(e => e.Id)).ToHashSet();
            Assert.All(announcedTargets, id => Assert.Contains(id, shown));
        }
    }

    // Source ids a step may name: clauses "c{i}", literals "c{i}-{j}", variables "v:{name}"; graph node names.
    private static HashSet<string> SourceIds(IProblem source) {
        var ids = new HashSet<string>();
        switch (source) {
            case SAT3 s:
                for (int i = 0; i < s.clauses.Count; i++) {
                    ids.Add("c" + i);
                    for (int j = 0; j < s.clauses[i].Count; j++) ids.Add("c" + i + "-" + j);
                }
                break;
            case CLIQUE c:
                ids.UnionWith(c.nodes);
                break;
            case API.Problems.NPComplete.NPC_INDEPENDENTSET.INDEPENDENTSET i:
                ids.UnionWith(i.nodes);
                break;
            default:
                throw new NotSupportedException(source.GetType().Name);
        }
        return ids;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void FocusIds_AreRealSourceAndTargetIds(string reduction, string instance) {
        IReduction red = Build(reduction, instance);
        ConstructionRun run = red.BuildRun(true)!;
        var sourceIds = SourceIds(red.reductionFrom);
        var targetNodes = TargetOf(red.reductionTo).Nodes;
        foreach (var step in run.Steps) {
            Assert.All(step.Partial.SourceFocus, id => Assert.Contains(id, sourceIds));
            Assert.All(step.Focus, id => Assert.Contains(id, targetNodes));
            Assert.All(step.Partial.Skipped, e => { Assert.Contains(e.A, targetNodes); Assert.Contains(e.B, targetNodes); });
            // Skipped edges are exactly the ones the target does not have.
            var edges = TargetOf(red.reductionTo).Edges;
            Assert.All(step.Partial.Skipped, e => Assert.DoesNotContain(Pair(e.A, e.B), edges));
        }
        // The last step is Done, always, and the earlier ones are not.
        Assert.Equal(StepEvent.Done, run.Steps[^1].Event);
        Assert.True(run.Steps[^1].Ok);
        Assert.DoesNotContain(run.Steps.SkipLast(1), s => s.Event == StepEvent.Done);
    }

    [Fact]
    public void ThreeSat_DefaultInstance_CaptionsUseBothProblemsWords() {
        IReduction red = Build(Sat, new SAT3().defaultInstance);
        var steps = red.BuildRun(true)!.Steps;
        // 3 clauses, 3 pairs of clauses, then K.
        Assert.Equal(7, steps.Count);
        Assert.Equal("Clause C1 (x1 | !x2 | x3) becomes nodes x1_0, !x2_0, x3_0, with no edges between them.", steps[0].Caption);
        Assert.Equal(new[] { "c0", "c0-0", "c0-1", "c0-2" }, steps[0].Partial.SourceFocus);
        Assert.Equal(new[] { "x1_0", "!x2_0", "x3_0" }, steps[0].Partial.Nodes);
        Assert.Equal(new[] { 0, 1, 2, 3 }, steps[0].Partial.Gadgets);
        Assert.Equal(StepEvent.Accept, steps[0].Event);

        Assert.Equal("Join clauses C1 and C2: 8 edges. x1_0 and !x1_1 contradict each other, so no edge.", steps[3].Caption);
        Assert.Equal(new[] { "!x1_1-x1_0" }, steps[3].Partial.Skipped.Select(e => e.Id));
        Assert.Equal(("!x1_1", "x1_0"), (steps[3].Partial.Skipped[0].A, steps[3].Partial.Skipped[0].B));
        Assert.Equal(new[] { "c0", "c1" }, steps[3].Partial.SourceFocus);
        Assert.Equal(8, steps[3].Partial.Edges.Length);

        Assert.Equal("K = 3, one node per clause: a clique of 3 nodes has to take one node from every clause.", steps[^1].Caption);
        Assert.Equal(3, steps[^1].Partial.Bound);
        Assert.Equal(StepEvent.Done, steps[^1].Event);
    }

    [Fact]
    public void ThreeSat_PairWithNoCompatibleLiterals_IsRejectedAndSaysWhy() {
        IReduction red = Build(Sat, "(x1) & (!x1)");
        var steps = red.BuildRun(true)!.Steps;
        Assert.Equal(StepEvent.Reject, steps[2].Event);
        Assert.Equal("Join clauses C1 and C2: 0 edges. x1_0 and !x1_1 contradict each other, so no edge.", steps[2].Caption);
    }

    [Fact]
    public void Clique_ToVertexCover_Captions() {
        IReduction red = Build(Vc, "(({a,b,c,d},{{a,b},{b,c},{a,c},{c,d}}),3)");
        var steps = red.BuildRun(true)!.Steps;
        // nodes copied, one per node, then K'.
        Assert.Equal(6, steps.Count);
        Assert.Equal("Copy every node of the clique graph (a, b, c, d) into the new graph, with the same names. Its edges will be the pairs the clique graph is missing.", steps[0].Caption);
        Assert.Equal(new[] { 0, 1, 2, 3 }, steps[0].Partial.Gadgets);
        Assert.Equal("Node a has no edge to d in the clique graph, so it gets that edge in the complement.", steps[1].Caption);
        Assert.Equal(new[] { "a-d" }, steps[1].Partial.Edges.Select(e => e.Id));
        Assert.Equal(new[] { "a" }, steps[1].Partial.SourceFocus);
        Assert.Equal("Node b has no edge to d in the clique graph, so it gets that edge in the complement.", steps[2].Caption);
        Assert.Equal(StepEvent.Reject, steps[3].Event);
        Assert.Equal("Node c is already joined to every later node in the clique graph, so it gets no new edges.", steps[3].Caption);
        Assert.Equal("K' = N - K = 4 - 3 = 1. The clique graph has a clique of size 3 exactly when the complement has a vertex cover of size 1.", steps[^1].Caption);
        Assert.Equal(1, steps[^1].Partial.Bound);
    }

    [Fact]
    public void IndependentSet_ToClique_Captions() {
        IReduction red = Build(Is, "(({a,b,c,d},{{a,b},{b,c},{c,d}}),2)");
        var steps = red.BuildRun(true)!.Steps;
        Assert.Equal("Node a has no edge to c, d in the original graph, so it gets those edges in the complement.", steps[1].Caption);
        Assert.Equal(new[] { "a-c", "a-d" }, steps[1].Partial.Edges.Select(e => e.Id));
        Assert.Equal("Node b has no edge to d in the original graph, so it gets that edge in the complement.", steps[2].Caption);
        Assert.Equal("K stays 2. 2 nodes with no edge between them in the original graph are 2 nodes joined to each other in the complement.", steps[^1].Caption);
    }

    private static string ChainFormula(int clauses) =>
        string.Join(" & ", Enumerable.Range(0, clauses).Select(i => $"(x{i} | !x{i + 1} | x{i + 2})"));

    [Fact]
    public void ManyClauses_PastTheCap_CombineTheRestIntoOneStep_AndHideNothing() {
        var steps = Build(Sat, ChainFormula(20)).BuildRun(true)!.Steps;
        // 20 clauses + 190 pairs = 210 steps: the first 150 as they are, the other 60 combined, then Done.
        Assert.Equal(StepRecorder<BuildDelta>.DefaultCap + 2, steps.Count);
        var combined = steps[^2];
        Assert.Equal(StepEvent.Accept, combined.Event);
        Assert.StartsWith("The last 60 steps are combined into this one to keep the list short. Together they add ", combined.Caption);
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.Equal(20, steps[^1].Partial.Bound);
        Assert.Equal("K = 20, one node per clause: a clique of 20 nodes has to take one node from every clause.", steps[^1].Caption);
        Assert.DoesNotContain(steps, s => s.Caption.Contains("not shown"));
    }

    // Past the cap, on the real reductions. 3SAT passes the default cap of 150 at 17 clauses (17 clause steps + 136 pair
    // steps), so its cases go through /buildSteps itself. The complement reductions would need 149 nodes (a copy step and
    // one per node), where their legacy O(n^4) complement code takes 10 to 40 seconds per build, so their cases fold the
    // full recording with a lower cap, exactly as BuildRun does with the default one. The low-cap 3SAT case also folds
    // clause (node) steps, not only pair steps, into the combined step.
    public static TheoryData<string, string, int> LargeCases() {
        static string Graph(int n, Func<int, int, bool> joined, int k) {
            var names = Enumerable.Range(0, n).Select(i => "v" + i).ToList();
            var edges = from i in Enumerable.Range(0, n) from j in Enumerable.Range(i + 1, n - i - 1) where joined(i, j) select $"{{v{i},v{j}}}";
            return $"(({{{string.Join(",", names)}}},{{{string.Join(",", edges)}}}),{k})";
        }
        static bool Some(int i, int j) => (i * 7 + j * 13) % 3 != 0;
        int cap = StepRecorder<BuildDelta>.DefaultCap;
        return new TheoryData<string, string, int> {
            { Sat, ChainFormula(17), cap },
            { Sat, ChainFormula(20), cap },
            { Sat, string.Join(" & ", Enumerable.Range(0, 24).Select(i => $"(x{i % 4} | !x{(i + 1) % 4} | x{(i + 2) % 5})")), cap },
            { Sat, ChainFormula(12), 5 },
            { Vc, Graph(40, Some, 5), 20 },
            { Is, Graph(40, Some, 5), 20 },
        };
    }

    [Theory]
    [MemberData(nameof(LargeCases))]
    public async Task LargeInstances_ReplayingEveryKeptStep_GivesExactlyTheReduceTarget(string reduction, string instance, int cap) {
        using var reduceDoc = JsonDocument.Parse(await PostOk("reduce", reduction, instance));
        string targetInstance = reduceDoc.RootElement.GetProperty("reductionTo").GetProperty("instance").GetString()!;
        IProblem target = reduction == Vc ? new VERTEXCOVER(targetInstance) : new CLIQUE(targetInstance);
        var (nodes, edges, k) = TargetOf(target);

        IReadOnlyList<SolverStep<BuildDelta>> steps;
        if (cap == StepRecorder<BuildDelta>.DefaultCap) {
            using var stepsDoc = JsonDocument.Parse(await PostOk("buildSteps", reduction, instance));
            steps = stepsDoc.RootElement.GetProperty("steps").EnumerateArray().Select(ReadStep).ToList();
        } else {
            IReduction red = Build(reduction, instance);
            var full = (ConstructionRun)red.GetType().GetMethod("Rebuild")!.Invoke(red, new object[] { new StepRecorder<BuildDelta>(cap: int.MaxValue) })!;
            Assert.True(full.Steps.Count > cap + 2, "the case must pass the cap");
            steps = ConstructionSteps.Limit(full.Steps, cap);
        }
        Assert.Equal(cap + 2, steps.Count);
        Assert.StartsWith("The last ", steps[^2].Caption);
        Assert.Equal(StepEvent.Done, steps[^1].Event);

        var (builtNodes, builtEdges, bound) = Replay(steps);
        Assert.Equal(nodes.Distinct().OrderBy(n => n, StringComparer.Ordinal), builtNodes.Distinct().OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(builtEdges.Count, builtEdges.Distinct().Count());
        Assert.Equal(edges.Distinct().OrderBy(e => e, StringComparer.Ordinal), builtEdges.OrderBy(e => e, StringComparer.Ordinal));
        Assert.Equal(k, bound);
    }

    // A step as /buildSteps sends it; each edge's id must be the one its two ends give.
    private static SolverStep<BuildDelta> ReadStep(JsonElement step) {
        var p = step.GetProperty("partial");
        string[] Strings(JsonElement a) => a.EnumerateArray().Select(x => x.GetString()!).ToArray();
        BuildEdge[] Edges(string field) => p.GetProperty(field).EnumerateArray().Select(e => {
            var edge = new BuildEdge(e.GetProperty("a").GetString()!, e.GetProperty("b").GetString()!);
            Assert.Equal(edge.Id, e.GetProperty("id").GetString());
            return edge;
        }).ToArray();
        var delta = new BuildDelta {
            SourceFocus = Strings(p.GetProperty("sourceFocus")),
            Nodes = Strings(p.GetProperty("nodes")),
            Edges = Edges("edges"),
            Skipped = Edges("skipped"),
            Gadgets = p.GetProperty("gadgets").EnumerateArray().Select(x => x.GetInt32()).ToArray(),
            Bound = p.TryGetProperty("bound", out var b) ? b.GetInt32() : null,
        };
        return new SolverStep<BuildDelta>(delta, Enum.Parse<StepEvent>(step.GetProperty("event").GetString()!),
            Strings(step.GetProperty("focus")), step.GetProperty("caption").GetString()!);
    }

    private async Task<string> PostOk(string endpoint, string reduction, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/{endpoint}?reduction={reduction}", content, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{endpoint}: {(int)response.StatusCode} {body}");
        return body;
    }

    [Fact]
    public void Limit_LeavesShortRunsAlone_AndNeverCombinesASingleStep() {
        SolverStep<BuildDelta> Step(string node) =>
            new(new BuildDelta { Nodes = new[] { node } }, StepEvent.Accept, new[] { node }, "add " + node);
        var done = new SolverStep<BuildDelta>(new BuildDelta { Bound = 1 }, StepEvent.Done, Array.Empty<string>(), "done") { Ok = true };
        var three = new[] { Step("a"), Step("b"), Step("c"), done };
        Assert.Same(three, ConstructionSteps.Limit(three, cap: 3));
        // One step past the cap is kept as it is: "combining" it would only change its caption.
        Assert.Equal(new[] { "add a", "add b", "add c", "done" }, ConstructionSteps.Limit(three, cap: 2).Select(s => s.Caption));

        var five = new[] { Step("a"), Step("b"), Step("c"), Step("d"), Step("e"), done };
        var limited = ConstructionSteps.Limit(five, cap: 2);
        Assert.Equal(4, limited.Count);
        Assert.Equal(new[] { "c", "d", "e" }, limited[2].Partial.Nodes);
        Assert.Equal(new[] { "c", "d", "e" }, limited[2].Focus);
        Assert.Equal("The last 3 steps are combined into this one to keep the list short. Together they add 3 nodes and 0 edges.", limited[2].Caption);
        Assert.Same(done, limited[3]);
    }

    [Fact]
    public void Limit_CombinedStepOfOnlyRejects_IsAReject_AndCountsTheSkippedPairs() {
        SolverStep<BuildDelta> Rejected(string a, string b) =>
            new(new BuildDelta { Skipped = new[] { new BuildEdge(a, b) } }, StepEvent.Reject, new[] { a, b }, "skip");
        var steps = new[] { Rejected("a", "b"), Rejected("b", "c"), Rejected("a", "c") };
        var limited = ConstructionSteps.Limit(steps, cap: 1);
        Assert.Equal(2, limited.Count);
        Assert.Equal(StepEvent.Reject, limited[1].Event);
        Assert.Equal(new[] { "a-c", "b-c" }, limited[1].Partial.Skipped.Select(e => e.Id));
        Assert.Equal("The last 2 steps are combined into this one to keep the list short. Together they add 0 edges, and leave 2 pairs without an edge.", limited[1].Caption);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Reduce_CalledAgain_RebuildsTheGadgetsInsteadOfAddingMore(string reduction, string instance) {
        IReduction red = Build(reduction, instance);
        string gadgets = GadgetJson(red.gadgets);
        string target = red.reductionTo.instance;
        int count = red.gadgets.Count;
        var reduce = red.GetType().GetMethod("reduce", Type.EmptyTypes)!;
        reduce.Invoke(red, null);
        reduce.Invoke(red, null);
        Assert.Equal(count, red.gadgets.Count);
        Assert.Equal(gadgets, GadgetJson(red.gadgets));
        Assert.Equal(target, red.reductionTo.instance);
        // And the steps still name every gadget exactly by index.
        ConstructionRun run = red.BuildRun(true)!;
        Assert.Equal(gadgets, GadgetJson(run.Gadgets));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EdgeIds_AreThePictureIds_SortedInEachStep_AndUnique(string reduction, string instance) {
        var steps = Build(reduction, instance).BuildRun(true)!.Steps;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in steps) {
            foreach (var e in step.Partial.Edges.Concat(step.Partial.Skipped)) {
                Assert.True(string.CompareOrdinal(e.A, e.B) < 0, $"{e.A} before {e.B}");
                Assert.Equal(PictureEdgeId(e.A, e.B), e.Id);
                Assert.True(seen.Add(e.Id), $"{e.Id} announced twice");
            }
            Assert.Equal(step.Partial.Edges.Select(e => e.Id).OrderBy(i => i, StringComparer.Ordinal), step.Partial.Edges.Select(e => e.Id));
            Assert.Equal(step.Partial.Skipped.Select(e => e.Id).OrderBy(i => i, StringComparer.Ordinal), step.Partial.Skipped.Select(e => e.Id));
        }
    }

    [Theory]
    [InlineData(Vc, "(({a-b,c,a,b-c},{}),1)")]
    [InlineData(Is, "(({a-b,c,a,b-c},{}),1)")]
    public void NodeNamesWithSeparators_GiveUniqueEdgeIds_AndTheEdgesNameTheirEnds(string reduction, string instance) {
        IReduction red = Build(reduction, instance);
        var steps = red.BuildRun(true)!.Steps;
        var all = steps.SelectMany(s => s.Partial.Edges).ToList();
        Assert.NotEmpty(all);
        Assert.Equal(all.Count, all.Select(e => e.Id).Distinct().Count());
        // Ends are given, so a client never splits an id: they are real target nodes.
        var nodes = TargetOf(red.reductionTo).Nodes;
        Assert.All(all, e => { Assert.Contains(e.A, nodes); Assert.Contains(e.B, nodes); });
        // a-b / c and a / b-c would both be "a-b-c" unescaped. (3SAT variable names cannot hold a '-'.)
        Assert.Contains(all, e => e.Id == @"a\-b-c" && e.A == "a-b" && e.B == "c");
        Assert.Contains(all, e => e.Id == @"a-b\-c" && e.A == "a" && e.B == "b-c");
    }

    [Fact]
    public void ComplementCaptions_SayTheLastNodeHasNothingLeft_AndEmptyGraphsSaySo() {
        var vc = Build(Vc, "(({a,b,c,d},{{a,b},{b,c},{a,c},{c,d}}),3)").BuildRun(true)!.Steps;
        Assert.Equal("Node d is the last node: the steps above already gave it its edges in the complement.", vc[4].Caption);
        Assert.Equal(StepEvent.Reject, vc[4].Event);
        var empty = Build(Is, "(({},{}),0)").BuildRun(true)!.Steps;
        Assert.Equal("The graph has no nodes, so the clique graph has none either.", empty[0].Caption);
        var emptyVc = Build(Vc, "(({},{}),0)").BuildRun(true)!.Steps;
        Assert.Equal("The clique graph has no nodes, so the new graph has none either.", emptyVc[0].Caption);
    }

    [Fact]
    public void ReductionsWithoutBuildSteps_SayNoAndReturnNull() {
        int checkedCount = 0;
        foreach (var (name, type) in ProblemProvider.Reductions) {
            IReduction red;
            try { red = (IReduction)Activator.CreateInstance(type)!; } catch { continue; }
            // The three of #691, the reductions out of 3SAT and SAT (#693), the graph reductions (#694), and the set and number
            // reductions (#695).
            bool expected = new[] { Sat, Vc, Is, "KarpReduceGRAPHCOLORING", "GareyJohnson", "KarpIntProgStandard", "KarpSATToSAT3", "SipserReduceToSAT3", "LawlerKarp", "KarpVertexCoverToNodeSet", "KarpVertexCoverToSetCover", "KarpDirectedHamiltonianToUndirectedHamiltonian", "GraphColoringToCliqueCover", "KarpGraphColorToExactCover", "KarpReduceSAT", "FengReduction", "SubsetSumToPartitionReduction", "WEIGHTEDCUTReduction", "reduceToEXACTCOVER", "KarpExactCoverToSubsetSum" }.Contains(type.Name);
            Assert.Equal(expected, red.hasBuildSteps());
            Assert.Equal(expected, red.BuildRun(true) != null);
            // Every shipped reduction records them since #695.
            if (type.Assembly == typeof(ProblemProvider).Assembly) Assert.True(expected, $"{type.Name} has no construction steps");
            checkedCount++;
        }
        Assert.True(checkedCount > 3);
        // So one without them is a test-only stand-in, which says no and builds nothing.
        IReduction without = new TestOnlyReductionWithoutSteps("({1,2},3)");
        Assert.False(without.hasBuildSteps());
        Assert.Null(without.BuildRun(true));
    }

    [Theory]
    [InlineData(Sat)]
    [InlineData(Vc)]
    [InlineData(Is)]
    public void Reduce_Json_GainsNothing(string reduction) {
        IReduction red = (IReduction)Activator.CreateInstance(ProblemProvider.Reductions[reduction.ToLower()])!;
        string json = JsonSerializer.Serialize(red);
        foreach (string leaked in new[] { "BuildRun", "buildRun", "hasBuildSteps", "buildSteps", "Rebuild", "steps" })
            Assert.DoesNotContain(leaked, json);
    }

    private async Task<HttpResponseMessage> Post(string reduction, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        return await _client.PostAsync($"/ProblemProvider/buildSteps?reduction={reduction}", content, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(Sat)]
    [InlineData(Vc)]
    [InlineData(Is)]
    public async Task Endpoint_Returns200_WithStepsOfTheDocumentedShape(string reduction) {
        string instance = ((IReduction)Activator.CreateInstance(ProblemProvider.Reductions[reduction.ToLower()])!).reductionFrom.defaultInstance;
        var response = await Post(reduction, instance);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(new[] { "steps" }, doc.RootElement.EnumerateObject().Select(p => p.Name));
        var steps = doc.RootElement.GetProperty("steps").EnumerateArray().ToList();
        Assert.True(steps.Count >= 3);
        foreach (var step in steps) {
            Assert.Equal(new[] { "partial", "event", "focus", "caption" },
                step.EnumerateObject().Select(p => p.Name).Where(n => n != "ok"));
            var partial = step.GetProperty("partial");
            foreach (string field in new[] { "sourceFocus", "nodes", "edges", "skipped", "gadgets" })
                Assert.Equal(JsonValueKind.Array, partial.GetProperty(field).ValueKind);
            foreach (string field in new[] { "edges", "skipped" })
                foreach (var edge in partial.GetProperty(field).EnumerateArray())
                    Assert.Equal(new[] { "id", "a", "b" }, edge.EnumerateObject().Select(p => p.Name));
            Assert.Equal(JsonValueKind.String, step.GetProperty("event").ValueKind);
        }
        var first = steps[0];
        Assert.Equal("Accept", first.GetProperty("event").GetString());
        Assert.False(first.GetProperty("partial").TryGetProperty("bound", out _));
        var last = steps[^1];
        Assert.Equal("Done", last.GetProperty("event").GetString());
        Assert.True(last.GetProperty("ok").GetBoolean());
        Assert.True(last.GetProperty("partial").GetProperty("bound").GetInt32() >= 0);
    }

    [Fact]
    public async Task Endpoint_Sat3ToClique_MatchesTheRecordedSteps() {
        string instance = new SAT3().defaultInstance;
        var response = await Post(Sat, instance);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var steps = doc.RootElement.GetProperty("steps").EnumerateArray().ToList();
        var recorded = Build(Sat, instance).BuildRun(true)!.Steps;
        Assert.Equal(recorded.Count, steps.Count);
        Assert.Equal(recorded.Select(s => s.Caption), steps.Select(s => s.GetProperty("caption").GetString()));
        Assert.Equal(new[] { "x1_0", "!x2_0", "x3_0" }, steps[0].GetProperty("focus").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Endpoint_ForAReductionWithoutBuildSteps_Is400NoBuildSteps() {
        // Every shipped reduction records construction steps since #695, so a test-only one stands in for one that does not.
        string name = nameof(TestOnlyReductionWithoutSteps);
        var response = await Post(name, "({1,2},3)");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("no_build_steps", doc.RootElement.GetProperty("error").GetString());
        Assert.Equal(name, doc.RootElement.GetProperty("reduction").GetString());
    }

    [Fact]
    public async Task Endpoint_UnknownReduction_Is400() {
        var unknown = await Post("NoSuchReduction", "x");
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        using var unknownDoc = JsonDocument.Parse(await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("unknown_reduction", unknownDoc.RootElement.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData(Sat)]
    [InlineData(Vc)]
    [InlineData(Is)]
    public async Task Endpoint_MalformedInstance_Is400NotAServerError(string reduction) {
        var malformed = await Post(reduction, "(((");
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    // #760: a Clique K outside 0..N would make K' = N - K negative (or above N); the build refuses it with the
    // reduction-input 400 that /reduce gives, before recording anything.
    [Theory]
    [InlineData("(({a,b,c},{{a,b}}),4)")]
    [InlineData("(({a,b,c},{{a,b}}),-1)")]
    public async Task Endpoint_CliqueKOutsideZeroToN_Is400ReductionInput(string instance) {
        var response = await Post(Vc, instance);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("reduction_input_parse_error", doc.RootElement.GetProperty("error").GetString());
        Assert.Contains("K must be between 0 and the number of nodes (3)", doc.RootElement.GetProperty("detail").GetString());
    }
}
