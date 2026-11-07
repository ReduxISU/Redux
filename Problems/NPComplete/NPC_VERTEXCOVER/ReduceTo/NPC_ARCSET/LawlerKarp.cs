using API.Interfaces;
using API.Problems.NPComplete.NPC_ARCSET;
using API.Problems.NPComplete.NPC_VERTEXCOVER;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Interfaces.Graphs;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using SPADE;
using System.Collections;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.ReduceTo.NPC_ARCSET;

class LawlerKarp : IReversibleReduction<VERTEXCOVER, ARCSET, NodeSet>, IRecordedReduction<VERTEXCOVER, ARCSET> {



    // --- Fields ---
    public string reductionName { get; } = "Lawler-Karp Arc Set Reduction";
    public string reductionDefinition { get; } = @"This Reduction is an implementation of Lawler and Karp's reduction as laid out in Karp's 21 NP_Complete Problems. 
                                            It takes an instance of an undirected graph (specifically an instance of VERTEXCOVER) and returns an instance of ARCSET (ie. a Directed Graph)
                                            Specifically, a reduction follows the following algorithm: 
                                            For an undirected graph H: Where H is made up of <V,E>
                                            Convert the undirected edges in E to pairs of directed edges. So an undirected edge {{A,B}} turns into the directed pair of edges {(A,B),(B,A)} 
                                            Then turn every node into a pair of nodes denoted by 0 and 1. So a node 'A' turns into the two nodes '<A,0>' and '<A,1>'
                                            Now looks at the pairs of edges in E and maps from 1 to 0. So an edge (A,B) turns into (<A,1>, <B,0>) and edge (B,A) becomes (<B,1>,<A,0>)
                                            Then add directed edges from every 0 node 'u' to 1 node 'u'. ie. creates edges from <A,0> to <A,1>, <B,0> to <B,1> … <Z,0> to <Z,1>
                                            Now the algorithm has created an ARCSET instance (in other words, a Digraph). ";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors { get; } = { "Daniel Igbokwe", "Caleb Eardley" };
    // reduce() emits 2 gadget nodes + O(1) gadget edges per input node, and O(1)
    // directed edges per input edge — O(n+m), no cross-product terms.
    public ReductionCost cost { get; } = ReductionCost.Linear;
    // Declared, not derived. Each node is independently replaced by a fixed 0/1-split
    // gadget, and each undirected edge independently by a fixed directed pair.
    public ReductionType reductionType { get; } = ReductionType.LocalReplacement;
    // Declared, not derived. One pass over nodes, one pass over edges.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Linear;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(n + m), n = |VERTEXCOVER.nodes|, m = |edges|";
    public List<Gadget> gadgets { get; }
    private VERTEXCOVER _reductionFrom;
    private ARCSET _reductionTo;
    // --- Properties ---
    public VERTEXCOVER reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public ARCSET reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }

    // --- Methods Including Constructors ---

    public LawlerKarp(string instance) : this(new VERTEXCOVER(instance)) { }
    // Builds from the instance with rec recording the construction as it goes (IRecordedReduction).
    public LawlerKarp(string instance, StepRecorder<BuildDelta> rec) : this(new VERTEXCOVER(instance), rec) { }
    public LawlerKarp() : this(new VERTEXCOVER()) { }
    public LawlerKarp(VERTEXCOVER from) : this(from, StepRecorder<BuildDelta>.Off) { }

    // The one place the target is built; rec is Off except when the construction steps are asked for.
    private LawlerKarp(VERTEXCOVER from, StepRecorder<BuildDelta> rec) {
        gadgets = new();
        _reductionFrom = from;
        _reductionTo = Build(rec);
    }

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new LawlerKarp(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    // What each arc of the target stands for, by its picture id: a node's own arc u0 -> u1 stands for u, and an edge's arc
    // u1 -> v0 for the edge {u,v}, read from its tail u. Private, so it stays out of the JSON.
    private readonly Dictionary<string, (string Tail, string? Head)> _arcOrigin = new(StringComparer.Ordinal);

    /// <summary>
    ///  Uses the VertexCover object's reduction utility to convert to a Arcset Graph and returns that equivalent object.
    /// </summary>
    /// <returns>
    /// An Arcset Object
    /// </returns>
    public ARCSET reduce() => Build(StepRecorder<BuildDelta>.Off);

    // Construction steps (see IRecordedReduction): one per node (its two nodes and the arc between them), one per edge (its
    // two arcs), then K. Gadgets (#694): an element per node and per edge, and the bound.
    private ARCSET Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
        _arcOrigin.Clear();
        VERTEXCOVER vertexcover = new VERTEXCOVER(_reductionFrom.instance);
        List<Node> newNodes = new List<Node>();
        var nodeNames = new List<string>();
        foreach (UtilCollection n in vertexcover.graph.Nodes) {
            string name = n.ToString();
            Node newNode1 = new Node(name);
            Node newNode2 = new Node(name);
            newNode1.name = name + "0";
            newNode2.name = name + "1";
            newNodes.Add(newNode1);
            newNodes.Add(newNode2);
            nodeNames.Add(name);
            _arcOrigin[LayeredFrames.ArcId(name + "0", name + "1")] = (name, null);
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string>() { name.ToString() },
                new List<string>() { LayeredFrames.NodeId(name + "0"), LayeredFrames.NodeId(name + "1"), LayeredFrames.ArcId(name + "0", name + "1") },
                legacyToIds: new List<string>() { name + "0", name + "1" }));
        }

        //Turn undirected edges into paired directed edges.
        List<Edge> newEdges = new List<Edge>();
        List<Edge> numberedEdges = new List<Edge>();
        var edgePairs = new List<(string U, string V)>();
        foreach (UtilCollection e in vertexcover.graph.Edges) {
            List<UtilCollection> cast = e.ToList();
            Node source = new Node(cast[0].ToString());
            Node target = new Node(cast[1].ToString());
            Edge newEdge1 = new Edge(source, target);
            Edge newEdge2 = new Edge(target, source);
            newEdges.Add(newEdge1);
            newEdges.Add(newEdge2);
            edgePairs.Add((cast[0].ToString(), cast[1].ToString()));
        }

        //map edges to to nodes
        foreach (Edge e in newEdges) {
            Node newNode1 = new Node(e.source.name + "1");
            Node newNode2 = new Node(e.target.name + "0");
            Edge numberedEdge = new Edge(newNode1, newNode2);
            numberedEdges.Add(numberedEdge);
        }

        //map from every 0 to 1
        for (int i = 0; i < newNodes.Count; ++i) {
            if (i % 2 == 0) {
                Edge newEdge = new Edge(newNodes[i], newNodes[i + 1]);
                numberedEdges.Add(newEdge);
            }
        }
        newEdges.Clear(); //Getting rid of unsplit edges
        newEdges = numberedEdges;

        //"{{1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)},1}" //formatting
        string nodeListStr = "";
        foreach (Node node in newNodes) {

            nodeListStr = nodeListStr + node.name + ",";
        }
        nodeListStr = nodeListStr.TrimEnd(',');
        string edgeListStr = "";
        foreach (Edge edge in newEdges) {
            string edgeStr = edge.directedString() + ","; //this line is what makes this class distinct from Undirected Graph
            edgeListStr = edgeListStr + edgeStr + "";
        }
        edgeListStr = edgeListStr.TrimEnd(',', ' ');
        string toStr = "(({" + nodeListStr + "}" + ",{" + edgeListStr + "}" + ")," + vertexcover.K + ")";
        ARCSET arcset = new ARCSET(toStr);

        // An element per edge (listed once, either way round): the edge to its two arcs, u1 -> v0 and v1 -> u0.
        var edgeGadget = new List<(string U, string V, int Gadget, BuildEdge[] Arcs)>();
        var seenEdges = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (u, v) in edgePairs) {
            string sourceId = u == v ? u : GraphSubsetFrames.EdgeId(u, v);
            if (!seenEdges.Add(sourceId)) continue;
            var arcs = new[] { LayeredFrames.BuildArc(u + "1", v + "0"), LayeredFrames.BuildArc(v + "1", u + "0") }.DistinctBy(a => a.Id).ToArray();
            _arcOrigin.TryAdd(arcs[0].Id, (u, v));
            if (arcs.Length > 1) _arcOrigin.TryAdd(arcs[1].Id, (v, u));
            edgeGadget.Add((u, v, gadgets.Count, arcs));
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { sourceId }, arcs.Select(a => a.Id).ToList()));
        }
        int bound = gadgets.Count;
        gadgets.Add(new Gadget(GadgetKind.Bound, new List<string>(), new List<string>(), note: $"K stays {vertexcover.K}"));

        if (rec.Enabled) {
            for (int i = 0; i < nodeNames.Count; i++) {
                string name = nodeNames[i];
                int index = i;
                string[] nodes = [LayeredFrames.NodeId(name + "0"), LayeredFrames.NodeId(name + "1")];
                rec.Accept(
                    () => new BuildDelta { SourceFocus = [name], Nodes = nodes, Edges = [LayeredFrames.BuildArc(name + "0", name + "1")], Gadgets = [index] },
                    () => $"Node {name} becomes two nodes, {name}0 and {name}1, joined by the arc {name}0 → {name}1. Removing that arc is how the arc set picks {name}.",
                    nodes);
            }
            foreach (var (u, v, index, arcs) in edgeGadget) {
                string[] focus = arcs.SelectMany(a => new[] { a.A, a.B }).Distinct(StringComparer.Ordinal).ToArray();
                string[] sourceFocus = u == v ? [u] : [GraphSubsetFrames.EdgeId(u, v), u, v];
                rec.Accept(
                    () => new BuildDelta { SourceFocus = sourceFocus, Edges = BuildEdge.Sorted(arcs), Gadgets = [index] },
                    () => u == v
                        ? $"Edge {{{u},{u}}} is a loop: it becomes the arc {u}1 → {u}0, closing the cycle {u}0 → {u}1 → {u}0, which only removing {u}0 → {u}1 breaks, as only {u} covers the loop."
                        : $"Edge {{{u},{v}}} becomes the arcs {u}1 → {v}0 and {v}1 → {u}0, closing the cycle {u}0 → {u}1 → {v}0 → {v}1 → {u}0. "
                            + $"Removing {u}0 → {u}1 or {v}0 → {v}1 breaks it, as {u} or {v} covers the edge.",
                    focus);
            }
            int k = vertexcover.K;
            rec.Done(new BuildDelta { Bound = k, Gadgets = [bound] }, true,
                $"K stays {k}: removing at most {Words.Count(k, "arc")} can leave no cycle exactly when at most {Words.Count(k, "node")} can cover every edge.");
        }
        return arcset;
    }

    public string mapSolutions(string problemFromSolution) {
        //Parse problemFromSolution into a list of nodes
        List<string> solutionList = GraphParser.parseNodeListWithStringFunctions(problemFromSolution);

        //Map solution 
        List<string> mappedSolutionList = new List<string>();
        foreach (string node in solutionList) {
            mappedSolutionList.Add(string.Format("({0}0,{0}1)", node));
        }
        string problemToSolution = "";
        foreach (string edge in mappedSolutionList) {
            problemToSolution += edge + ',';
        }
        return '{' + problemToSolution.TrimEnd(',') + '}';
    }

    public NodeSet EmptyAnswer() => new NodeSet([]);

    // "{}" on either side keeps the default (the verifiers): removing no arc is a real answer when the graph has no edge, and
    // so is the empty cover.

    /// <summary>
    /// Backward map (#694): a feedback arc set to a vertex cover. A node's own arc u0 -> u1 stands for u. An edge's arc
    /// u1 -> v0 stands for the edge {u,v}; removing u0 -> u1 instead breaks every cycle through it as well, so its tail's node
    /// u goes in the cover. Each node once, in the order the arcs name them, so the cover is never larger than the arc set.
    /// </summary>
    public (string Answer, NodeSet Partial) MapBack(string problemToSolution, StepRecorder<NodeSet> rec) {
        var cover = new List<string>();
        foreach (var (from, to) in ReadArcs(problemToSolution)) {
            string id = LayeredFrames.ArcId(from, to);
            var (tail, head) = _arcOrigin[id];
            bool known = cover.Contains(tail);
            if (!known) cover.Add(tail);
            string already = known ? $" {tail} is already in the cover." : "";
            rec.Accept(() => new NodeSet(cover),
                () => head == null
                    ? $"Arc {from} → {to} is removed. It is node {tail}'s own arc, so {tail} is in the vertex cover.{already}"
                    : $"Arc {from} → {to} is removed. It stands for edge {{{tail},{head}}}, and removing {tail}0 → {tail}1 instead breaks every cycle through it too, so {tail} covers the edge.{already}",
                id);
        }
        return (ReductionBack.FormatNodeSet(cover), new NodeSet(cover));
    }

    // A set of arcs as Feedback Arc Set writes it, {(a0,a1),(b1,c0)}. Every arc must be one of the target's; malformed text
    // throws FormatException, which the endpoint reports as a 400.
    private List<(string From, string To)> ReadArcs(string certificate) {
        string text = certificate.Trim();
        if (text.Length < 2 || text[0] != '{' || text[^1] != '}')
            throw new FormatException($"'{certificate}' is not a set of arcs in braces like {{(a0,a1)}}");
        UtilCollection all;
        try {
            all = new UtilCollection(text);
        } catch (Exception ex) when (ParseGuard.IsCertificateParseFailure(ex)) {
            throw new FormatException($"'{certificate}' is not a set of arcs like {{(a0,a1)}}", ex);
        }
        var arcs = new List<(string, string)>();
        foreach (UtilCollection arc in all.ToList()) {
            var ends = arc.ToList();
            if (ends.Count != 2) throw new FormatException($"'{arc}' is not an arc (u,v)");
            string from = ends[0].ToString(), to = ends[1].ToString();
            if (!_arcOrigin.ContainsKey(LayeredFrames.ArcId(from, to)))
                throw new FormatException($"'({from},{to})' is not an arc of the graph");
            arcs.Add((from, to));
        }
        return arcs;
    }
}
// return an instance of what you are reducing to