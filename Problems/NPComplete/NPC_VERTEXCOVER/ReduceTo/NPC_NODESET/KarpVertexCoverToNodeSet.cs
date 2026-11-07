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

// Karp's Vertex Cover to Feedback Node Set: the same nodes, two arcs for every edge (one each way, a cycle of two), and the
// same K. It records the construction steps (the nodes copied, one per edge, then K; see Build) and the backward map's steps
// (one per node of the graph; see MapBack).
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

    // How the construction steps show the nodes copied: every node of the graph, the same nodes added to the directed graph,
    // and their element gadgets (the first nodeGadgets gadgets, one per node). The state is the graph's node names.
    private static RunSteps<List<string>, BuildDelta> NewCopySteps(StepRecorder<BuildDelta> rec, int nodeGadgets) => new(rec,
        // What the Reduction View shows: the nodes in both graphs, and the gadgets that copy them.
        partial: names => new BuildDelta {
            SourceFocus = names.ToArray(),
            Nodes = names.Select(LayeredFrames.NodeId).ToArray(),
            Gadgets = Enumerable.Range(0, nodeGadgets).ToArray(),
        },
        focus: names => names.Select(LayeredFrames.NodeId).ToArray());

    // How the construction steps show an edge: the edge and its two ends in the graph, its two arcs, and the gadget that makes
    // them. The state is (U, V, Gadget, Arcs): the edge's ends, its gadget's index, and its arcs u -> v and v -> u.
    private static RunSteps<(string U, string V, int Gadget, BuildEdge[] Arcs), BuildDelta> NewEdgeSteps(StepRecorder<BuildDelta> rec) => new(rec,
        // What the Reduction View shows: the edge, the arcs added, and their gadget.
        partial: s => new BuildDelta { SourceFocus = [GraphSubsetFrames.EdgeId(s.U, s.V), s.U, s.V], Edges = BuildEdge.Sorted(s.Arcs), Gadgets = [s.Gadget] },
        focus: s => [LayeredFrames.NodeId(s.U), LayeredFrames.NodeId(s.V)]);

    // Builds the directed graph, recording the construction steps as it goes (see IRecordedReduction). Its steps:
    //   - an Accept for the nodes: every node of the graph is copied, with the same name;
    //   - an Accept per edge: it becomes two arcs, one each way, a cycle of two;
    //   - the Done: K stays the same.
    // A construction has no verdicts, so Accept means "these pieces go into the target". The steps come from the very
    // decisions below, so what is recorded is what is built.
    // Gadgets: an element per node and per edge, then the bound (K unchanged).
    private NODESET Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
        VERTEXCOVER VERTEXCOVERInstance = _reductionFrom;
        NODESET reducedNODESET = new NODESET();

        string instance = "(({";
        foreach (var node in reductionFrom.nodes) {
            instance += node + ',';
        }

        // An element per node: the node to its copy.
        var nodeNames = new List<string>();
        foreach (UtilCollection node in VERTEXCOVERInstance.graph.Nodes) {
            nodeNames.Add(node.ToString());
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string>() { node.ToString() }, new List<string>() { LayeredFrames.NodeId(node.ToString()) },
                legacyToIds: new List<string>() { node.ToString() }));
        }
        var copySteps = NewCopySteps(rec, gadgets.Count);
        if (nodeNames.Count > 0)
            copySteps.Accept(nodeNames, $"Copy every node of the graph ({BuildCaptions.Names(nodeNames)}) into the directed graph, with the same names. Its arcs will come from the edges.");
        else
            copySteps.Accept(nodeNames, $"The graph has no nodes, so the directed graph has none either.");
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

        // Parsed from its own text, so its graph is this one and not the default instance's.
        reducedNODESET = new NODESET(instance);

        // An element per edge (listed once, either way round): the edge to its two arcs, u -> v and v -> u. Each edge is
        // then one step.
        var edgeSteps = NewEdgeSteps(rec);
        var seenEdges = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (u, v) in edgePairs) {
            if (!seenEdges.Add(GraphSubsetFrames.EdgeId(u, v))) continue;
            BuildEdge[] arcs = [LayeredFrames.BuildArc(u, v), LayeredFrames.BuildArc(v, u)];
            int index = gadgets.Count;
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { GraphSubsetFrames.EdgeId(u, v) }, arcs.Select(a => a.Id).ToList()));
            edgeSteps.Accept((u, v, index, arcs),
                $"Edge {{{u},{v}}} becomes the arcs {u} → {v} and {v} → {u}: a cycle of two, which only removing {u} or {v} breaks, as only {u} or {v} covers the edge.");
        }
        // Then the bound: K stays the same. The directed graph has no piece of its own for it.
        int bound = gadgets.Count;
        gadgets.Add(new Gadget(GadgetKind.Bound, new List<string>(), new List<string>(), note: $"K stays {reductionFrom.K}"));

        // The closing step, on either recorder (both record to rec).
        int k = reductionFrom.K;
        edgeSteps.Done(new BuildDelta { Bound = k, Gadgets = [bound] }, true,
            $"K stays {k}: every cycle of the directed graph goes along some edge, so removing at most {Words.Count(k, "node")} leaves no cycle exactly when they cover every edge.");

        reductionTo = reducedNODESET;
        return reducedNODESET;
    }

    // A vertex cover is a feedback node set of the target: every edge became a cycle of two arcs, which only removing one of
    // its ends breaks, and the target has no other cycle. So the same nodes are the answer.
    public string mapSolutions(string problemFromSolution) {
        List<string> cover = GraphParser.parseNodeListWithStringFunctions(problemFromSolution);
        return "{" + string.Join(",", cover) + "}";
    }

    public NodeSet EmptyAnswer() => new NodeSet([]);

    // "{}" on either side keeps the default (the verifiers): removing no node is a real answer when the graph has no edge,
    // and so is the empty cover.

    // How the map-back steps show a node read: the cover so far, and the node's copy in the directed graph.
    private static RunSteps<string, NodeSet> NewMapSteps(StepRecorder<NodeSet> rec, List<string> cover) => new(rec,
        // What every picture shows: the cover so far, and the node the step is about.
        partial: _ => new NodeSet(cover),
        focus: node => [LayeredFrames.NodeId(node)]);

    /// <summary>
    /// Backward map: the target has a cycle of two arcs per edge and no other cycle, so a feedback node set is a vertex
    /// cover with the same nodes. Each node of the graph, in order, is in the cover exactly when the answer removes it.
    /// Its steps, one per node of the graph: an Accept for a node the answer removes (it goes in the cover), a Reject for one
    /// it keeps (it is left out). The caller adds the Done.
    /// </summary>
    public (string Answer, NodeSet Partial) MapBack(string problemToSolution, StepRecorder<NodeSet> rec) {
        var removed = new HashSet<string>(ReductionBack.ParseNodeSet(problemToSolution, reductionTo.nodes), StringComparer.Ordinal);
        var cover = new List<string>();
        var steps = NewMapSteps(rec, cover);
        foreach (string node in reductionFrom.nodes) {
            if (removed.Contains(node)) {
                cover.Add(node);
                steps.Accept(node, $"Node {node} is in the feedback node set, so it is in the vertex cover.");
            } else {
                steps.Reject(node, $"Node {node} is not in the feedback node set, so it is left out of the cover.");
            }
        }
        return (ReductionBack.FormatNodeSet(cover), new NodeSet(cover));
    }
}
