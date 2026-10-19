using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_NODESET;
using API.Problems.NPComplete.NPC_VERTEXCOVER;
using SPADE;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.ReduceTo.NPC_NODESET;

class KarpVertexCoverToNodeSet : IReversibleReduction<VERTEXCOVER, NODESET, NodeSet>, IRecordedReduction<VERTEXCOVER, NODESET> {

    // --- Fields ---
    public string reductionName { get; } = "Karp's Node Set Reduction";
    public string reductionDefinition { get; } = "Karp's Reduction from Vertex Cover to Feedback Node Set";
    public string source { get; } = "This reduction was found by the Algorithms Seminar at the Cornell University Computer Science Department. Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    // The node x node loop only emits an instance entry when the pair IS an existing
    // edge (not its complement) — despite the O(n^2)-iteration scan, real output is
    // bounded by O(m) (each existing edge emitted in both directions).
    public ReductionCost cost { get; } = ReductionCost.Linear;

    // Declared, not derived. Reformats the input's own node/edge structure directly
    // into NODESET's shape -- the same graph re-expressed, no new gadgetry built.
    public ReductionType reductionType { get; } = ReductionType.Restriction;
    // Declared, not derived. Nested node x node loop scanning for existing edges.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Polynomial;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(n^2), n = |VERTEXCOVER.nodes|";
    public List<Gadget> gadgets { get; }
    private VERTEXCOVER _reductionFrom;
    private NODESET _reductionTo;


    // --- Properties ---
    public VERTEXCOVER reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public NODESET reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }



    // --- Methods Including Constructors ---
    public KarpVertexCoverToNodeSet(VERTEXCOVER from) : this(from, StepRecorder<BuildDelta>.Off) { }

    // The one place the target is built; rec is Off except when the construction steps are asked for.
    private KarpVertexCoverToNodeSet(VERTEXCOVER from, StepRecorder<BuildDelta> rec) {
        gadgets = new();
        _reductionFrom = from;
        _reductionTo = Build(rec);
    }
    public KarpVertexCoverToNodeSet(string instance) : this(new VERTEXCOVER(instance)) { }
    // Builds from the instance with rec recording the construction as it goes (IRecordedReduction).
    public KarpVertexCoverToNodeSet(string instance, StepRecorder<BuildDelta> rec) : this(new VERTEXCOVER(instance), rec) { }
    public KarpVertexCoverToNodeSet() : this(new VERTEXCOVER()) { }

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new KarpVertexCoverToNodeSet(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    public NODESET reduce() => Build(StepRecorder<BuildDelta>.Off);

    // Construction steps (see IRecordedReduction): the nodes copied, one step per edge (its two arcs), then K. Gadgets (#694):
    // an element per node and per edge, and the bound.
    private NODESET Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
        VERTEXCOVER VERTEXCOVERInstance = _reductionFrom;
        NODESET reducedNODESET = new NODESET();

        string instance = "(({";
        foreach (var node in reductionFrom.nodes) {
            instance += node + ',';
        }

        foreach (UtilCollection node in VERTEXCOVERInstance.graph.Nodes) {
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string>() { node.ToString() }, new List<string>() { LayeredFrames.NodeId(node.ToString()) },
                legacyToIds: new List<string>() { node.ToString() }));
        }
        int nodeGadgets = gadgets.Count;
        // --- Generate G string for new CLIQUE ---

        var edgePairs = new List<(string U, string V)>();
        instance = instance.TrimEnd(',') + "},{(";
        foreach (var node in reductionFrom.nodes) {
            foreach (var node2 in reductionFrom.nodes) {
                KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(node, node2);
                KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(node2, node);
                if ((reductionFrom.edges.Contains(pairCheck1) || reductionFrom.edges.Contains(pairCheck1)) && node != node2) {
                    instance += node + ',' + node2 + "),(";
                    instance += node2 + ',' + node + "),(";
                    edgePairs.Add((node, node2));
                }
            }
        }

        instance = instance.TrimEnd('(').TrimEnd(',') + "})," + reductionFrom.K.ToString() + ')';

        // Parsed from its own text, so its graph is this one and not the default instance's (#823).
        reducedNODESET = new NODESET(instance);

        // An element per edge (listed once, either way round): the edge to its two arcs, u -> v and v -> u.
        var edgeGadget = new List<(string U, string V, int Gadget, BuildEdge[] Arcs)>();
        var seenEdges = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (u, v) in edgePairs) {
            if (!seenEdges.Add(GraphSubsetFrames.EdgeId(u, v))) continue;
            BuildEdge[] arcs = [LayeredFrames.BuildArc(u, v), LayeredFrames.BuildArc(v, u)];
            edgeGadget.Add((u, v, gadgets.Count, arcs));
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { GraphSubsetFrames.EdgeId(u, v) }, arcs.Select(a => a.Id).ToList()));
        }
        int bound = gadgets.Count;
        gadgets.Add(new Gadget(GadgetKind.Bound, new List<string>(), new List<string>(), note: $"K stays {reductionFrom.K}"));

        if (rec.Enabled) {
            string[] all = VERTEXCOVERInstance.graph.Nodes.ToList().Select(n => n.ToString()).ToArray();
            string[] ids = all.Select(LayeredFrames.NodeId).ToArray();
            int[] nodeIndex = Enumerable.Range(0, nodeGadgets).ToArray();
            rec.Accept(
                () => new BuildDelta { SourceFocus = all, Nodes = ids, Gadgets = nodeIndex },
                () => all.Length > 0
                    ? $"Copy every node of the graph ({BuildCaptions.Names(all)}) into the directed graph, with the same names. Its arcs will come from the edges."
                    : "The graph has no nodes, so the directed graph has none either.",
                ids);
            foreach (var (u, v, index, arcs) in edgeGadget) {
                rec.Accept(
                    () => new BuildDelta { SourceFocus = [GraphSubsetFrames.EdgeId(u, v), u, v], Edges = BuildEdge.Sorted(arcs), Gadgets = [index] },
                    () => $"Edge {{{u},{v}}} becomes the arcs {u} → {v} and {v} → {u}: a cycle of two, which only removing {u} or {v} breaks, as only {u} or {v} covers the edge.",
                    [LayeredFrames.NodeId(u), LayeredFrames.NodeId(v)]);
            }
            int k = reductionFrom.K;
            rec.Done(new BuildDelta { Bound = k, Gadgets = [bound] }, true,
                $"K stays {k}: every cycle of the directed graph goes along some edge, so removing at most {BuildCaptions.Count(k, "node")} leaves no cycle exactly when they cover every edge.");
        }

        reductionTo = reducedNODESET;
        return reducedNODESET;
    }

    // A vertex cover is a feedback node set of the target: every edge became a cycle of two arcs, which only removing one of
    // its ends breaks, and the target has no other cycle. So the same nodes are the answer (#823).
    public string mapSolutions(string problemFromSolution) {
        List<string> cover = GraphParser.parseNodeListWithStringFunctions(problemFromSolution);
        return "{" + string.Join(",", cover) + "}";
    }

    public NodeSet EmptyAnswer() => new NodeSet([]);

    // "{}" on either side keeps the default (the verifiers): removing no node is a real answer when the graph has no edge,
    // and so is the empty cover.

    /// <summary>
    /// Backward map (#694): the target has a cycle of two arcs per edge and no other cycle, so a feedback node set is a vertex
    /// cover with the same nodes. Each node of the graph, in order, is in the cover exactly when the answer removes it.
    /// </summary>
    public (string Answer, NodeSet Partial) MapBack(string problemToSolution, StepRecorder<NodeSet> rec) {
        var removed = new HashSet<string>(ReductionBack.ParseNodeSet(problemToSolution, reductionTo.nodes), StringComparer.Ordinal);
        var cover = new List<string>();
        foreach (string node in reductionFrom.nodes) {
            string id = LayeredFrames.NodeId(node);
            if (removed.Contains(node)) {
                cover.Add(node);
                rec.Accept(() => new NodeSet(cover), () => $"Node {node} is in the feedback node set, so it is in the vertex cover.", id);
            } else {
                rec.Reject(() => new NodeSet(cover), () => $"Node {node} is not in the feedback node set, so it is left out of the cover.", id);
            }
        }
        return (ReductionBack.FormatNodeSet(cover), new NodeSet(cover));
    }
}
