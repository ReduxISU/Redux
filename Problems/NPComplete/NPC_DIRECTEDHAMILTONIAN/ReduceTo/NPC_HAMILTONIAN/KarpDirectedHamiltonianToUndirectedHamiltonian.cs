using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_HAMILTONIAN;

namespace API.Problems.NPComplete.NPC_DIRECTEDHAMILTONIAN.ReduceTo.NPC_HAMILTONIAN;

// Karp's Directed Hamiltonian Cycle to Hamiltonian Cycle: each node v becomes a path v1 - v2 - v3, and each arc u -> v the
// edge {u3, v1}. It records the construction steps (one per node, one per arc, then the closing step; see Build) and the
// backward map's steps (one per edge of the cycle between two paths; see MapBack).
class KarpDirectedHamiltonianToUndirectedHamiltonian : IReversibleReduction<DIRECTEDHAMILTONIAN, HAMILTONIAN, Tour>, IRecordedReduction<DIRECTEDHAMILTONIAN, HAMILTONIAN> {

    // --- Fields ---
    public string reductionName { get; } = "Karp's Hamiltonian Reduction";
    public string reductionDefinition { get; } = "TODO";
    public string source { get; } = "TODO";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    // reduce() emits exactly 3 gadget nodes + 2 gadget edges per input node, and 1
    // converted edge per input edge — O(n+m), no cross-product terms.
    public ReductionCost cost { get; } = ReductionCost.Linear;
    // Declared, not derived. Each node is independently replaced by a fixed 3-node
    // gadget, and each directed edge independently by a fixed connector edge.
    public ReductionType reductionType { get; } = ReductionType.LocalReplacement;
    // Declared, not derived. One pass over nodes, one pass over edges.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Linear;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(n + m), n = |nodes|, m = |edges|";
    public List<Gadget> gadgets { get; }
    private DIRECTEDHAMILTONIAN _reductionFrom;
    private HAMILTONIAN _reductionTo;


    // --- Properties ---
    public DIRECTEDHAMILTONIAN reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public HAMILTONIAN reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }

    // --- Methods Including Constructors ---

    public KarpDirectedHamiltonianToUndirectedHamiltonian(DIRECTEDHAMILTONIAN from) : this(from, StepRecorder<BuildDelta>.Off) { }

    // The one place the target is built; rec is Off except when the construction steps are asked for.
    private KarpDirectedHamiltonianToUndirectedHamiltonian(DIRECTEDHAMILTONIAN from, StepRecorder<BuildDelta> rec) {
        gadgets = new();
        _reductionFrom = from;
        _reductionTo = Build(rec);
    }
    public KarpDirectedHamiltonianToUndirectedHamiltonian(string instance) : this(new DIRECTEDHAMILTONIAN(instance)) { }
    // Builds from the instance with rec recording the construction as it goes (IRecordedReduction).
    public KarpDirectedHamiltonianToUndirectedHamiltonian(string instance, StepRecorder<BuildDelta> rec) : this(new DIRECTEDHAMILTONIAN(instance), rec) { }
    public KarpDirectedHamiltonianToUndirectedHamiltonian() : this(new DIRECTEDHAMILTONIAN()) { }

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new KarpDirectedHamiltonianToUndirectedHamiltonian(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    // Each node of the target: the node of the directed graph it is part of, and which part (1 the in-end, 2 the middle, 3
    // the out-end). Private, so it stays out of the JSON.
    private readonly Dictionary<string, (string Node, int Part)> _partOf = new(StringComparer.Ordinal);

    public HAMILTONIAN reduce() => Build(StepRecorder<BuildDelta>.Off);

    // How the construction steps show a node: the node in the directed graph, its path v1 - v2 - v3, and the gadget that
    // makes it. The state is (V, Gadget, Path): the node, its gadget's index, and the path's two edges.
    private static RunSteps<(string V, int Gadget, BuildEdge[] Path), BuildDelta> NewNodeSteps(StepRecorder<BuildDelta> rec) => new(rec,
        // What the Reduction View shows: the node, the path's nodes and edges added, and their gadget.
        partial: s => new BuildDelta {
            SourceFocus = [s.V],
            Nodes = [s.V + "1", s.V + "2", s.V + "3"],
            Edges = BuildEdge.Sorted(s.Path),
            Gadgets = [s.Gadget],
        },
        focus: s => [s.V + "1", s.V + "2", s.V + "3"]);

    // How the construction steps show an arc: the arc and its ends in the directed graph (a loop, just its node), the edge
    // that stands for it, and its gadget. The state is (U, V, Gadget, Edge): the arc's tail and head, its gadget's index,
    // and the edge {u3, v1}.
    private static RunSteps<(string U, string V, int Gadget, BuildEdge Edge), BuildDelta> NewArcSteps(StepRecorder<BuildDelta> rec) => new(rec,
        // What the Reduction View shows: the arc, the edge added, and its gadget.
        partial: s => new BuildDelta {
            SourceFocus = s.U == s.V ? [s.U] : [GraphTourFrames.EdgeId(true, s.U, s.V), s.U, s.V],
            Edges = [s.Edge],
            Gadgets = [s.Gadget],
        },
        focus: s => [s.U + "3", s.V + "1"]);

    // Builds the undirected graph, recording the construction steps as it goes (see IRecordedReduction). Its steps:
    //   - an Accept per node: it becomes the path v1 - v2 - v3;
    //   - an Accept per arc u -> v: it becomes the edge {u3, v1};
    //   - the Done: why the two graphs have a Hamiltonian cycle together.
    // A construction has no verdicts, so Accept means "these pieces go into the target". The steps come from the very
    // decisions below, so what is recorded is what is built.
    // Gadgets: an element per node and per arc.
    private HAMILTONIAN Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
        _partOf.Clear();
        // Part 1: Create necessary data structures
        DIRECTEDHAMILTONIAN DH = _reductionFrom;
        HAMILTONIAN UH = new HAMILTONIAN();

        List<string> newNodes = new List<string>();
        List<KeyValuePair<string, string>> newEdges = new List<KeyValuePair<string, string>>();

        // Part 2: Convert nodes from directed graph to undirected graph
        foreach (string v in DH.nodes) {
            string v1 = v + "1";
            string v2 = v + "2";
            string v3 = v + "3";

            newNodes.Add(v1);
            newNodes.Add(v2);
            newNodes.Add(v3);

            // Force traversal order inside the gadget
            newEdges.Add(new KeyValuePair<string, string>(v1, v2));
            newEdges.Add(new KeyValuePair<string, string>(v2, v3));
        }

        // For every directed edge (u → v), add undirected edge (u3, v1)
        foreach (var e in DH.edges) {
            string u = e.Key;
            string v = e.Value;

            string u3 = u + "3";
            string v1 = v + "1";

            newEdges.Add(new KeyValuePair<string, string>(u3, v1));
        }

        // Part 3: Construct the new HAMILTONIAN instance
        string nodesString = string.Join(",", newNodes);

        string edgesString = "";
        foreach (var edge in newEdges) {
            edgesString += "{" + edge.Key + "," + edge.Value + "},";
        }
        edgesString = edgesString.Trim(',');

        string G = "({" + nodesString + "},{" + edgesString + "})";

        UH = new HAMILTONIAN(G);

        // Gadgets: a node's path, and an arc's connecting edge (an arc listed twice is one edge, as in both pictures). Each
        // node, and each arc, is then one step.
        var nodeSteps = NewNodeSteps(rec);
        foreach (string v in DH.nodes.Distinct()) {
            for (int part = 1; part <= 3; part++) _partOf[v + part] = (v, part);
            BuildEdge[] path = [new BuildEdge(v + "1", v + "2"), new BuildEdge(v + "2", v + "3")];
            int index = gadgets.Count;
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { v },
                new List<string> { v + "1", v + "2", v + "3" }.Concat(path.Select(p => p.Id)).ToList()));
            nodeSteps.Accept((v, index, path),
                $"Node {v} becomes the path {v}1 - {v}2 - {v}3. {v}2 has no other edge, so a tour that reaches {v}1 goes on through {v}2 to {v}3: {v}1 is where arcs into {v} arrive and {v}3 where arcs out of {v} leave.");
        }
        var arcSteps = NewArcSteps(rec);
        var seenArcs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in DH.edges) {
            string arc = GraphTourFrames.EdgeId(true, e.Key, e.Value);
            if (!seenArcs.Add(arc)) continue;
            string u = e.Key, v = e.Value;
            var edge = new BuildEdge(u + "3", v + "1");
            int index = gadgets.Count;
            // A loop is not drawn as an arc, so it stands for its node.
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { u == v ? u : arc }, new List<string> { edge.Id }));
            arcSteps.Accept((u, v, index, edge),
                $"Arc {u} → {v} becomes the edge {{{u}3,{v}1}}: it leaves {u} by its out-end {u}3 and enters {v} by its in-end {v}1.");
        }

        // The closing step, on either recorder (both record to rec). Its delta adds nothing: the graph has no bound.
        arcSteps.Done(new BuildDelta(), true,
            $"The undirected graph has a Hamiltonian cycle exactly when the directed graph does: every node's path is walked from its in-end to its out-end, so the edges between paths are walked along their arcs.");

        reductionTo = UH;
        return UH;
    }

    // A directed Hamiltonian cycle v, w, ... becomes the undirected one that walks each node's path v1, v2, v3 and leaves v3
    // for the next node's w1 along the edge that stands for the arc (v, w). The directed certificate repeats its first node
    // at the end; the undirected one does not.
    public string mapSolutions(string problemFromSolution) {
        List<string> tour = GraphParser.parseNodeListWithStringFunctions(problemFromSolution);
        if (tour.Count > 1 && tour[0] == tour[^1]) tour.RemoveAt(tour.Count - 1);
        foreach (string node in tour)
            if (!reductionFrom.nodes.Contains(node))
                throw new ReductionInputException(this, problemFromSolution, reductionFrom.certificateFormat, $"'{node}' is not a node of the graph");
        return "{" + string.Join(",", tour.SelectMany(v => new[] { v + "1", v + "2", v + "3" })) + "}";
    }

    public Tour EmptyAnswer() => new Tour([]);

    // "{}" on either side keeps the default (the verifiers): only a graph with no nodes has the empty tour.

    // How the map-back steps show an edge of the cycle read: the tour so far, and the edge.
    private static RunSteps<string, Tour> NewMapSteps(StepRecorder<Tour> rec, List<string> tour) => new(rec,
        // What every picture shows: the tour so far, and the edge the step is about.
        partial: _ => new Tour(tour),
        focus: edge => [edge]);

    /// <summary>
    /// Backward map: an undirected Hamiltonian cycle walks every node's path v1 - v2 - v3 from end to end, so read in
    /// the direction that goes v1, v2, v3, each edge from a path's out-end u3 to another's in-end w1 is the arc u → w. The
    /// tour starts at the node whose path the cycle (turned that way) starts with, and follows those edges back to it. A
    /// cycle that is not of that shape still maps, edge by edge; the Done step reports what the directed verifier says.
    /// Its steps: an Accept per edge of the cycle from one path's out-end to another's in-end (the tour goes along its arc).
    /// The caller adds the Done.
    /// </summary>
    public (string Answer, Tour Partial) MapBack(string problemToSolution, StepRecorder<Tour> rec) {
        List<string> cycle = GraphParser.parseNodeListWithStringFunctions(problemToSolution);
        if (cycle.Count > 1 && cycle[0] == cycle[^1]) cycle.RemoveAt(cycle.Count - 1);
        foreach (string node in cycle)
            if (!_partOf.ContainsKey(node)) throw new FormatException($"'{node}' is not a node of the undirected graph");
        if (cycle.Count == 0) return (ReductionBack.NoAnswer, new Tour([]));

        // Turn the cycle so that a path is walked 1, 2, 3, and start it at an in-end.
        int middle = cycle.FindIndex(n => _partOf[n].Part == 2);
        if (middle >= 0) {
            string before = cycle[(middle - 1 + cycle.Count) % cycle.Count];
            if (_partOf[before].Part == 3 && _partOf[before].Node == _partOf[cycle[middle]].Node) cycle.Reverse();
        }
        int start = cycle.FindIndex(n => _partOf[n].Part == 1);
        if (start > 0) cycle = cycle.Skip(start).Concat(cycle.Take(start)).ToList();

        var tour = new List<string> { _partOf[cycle[0]].Node };
        var steps = NewMapSteps(rec, tour);
        for (int i = 0; i < cycle.Count; i++) {
            string a = cycle[i], b = cycle[(i + 1) % cycle.Count];
            if (_partOf[a].Part != 3 || _partOf[b].Part != 1) continue;
            string u = _partOf[a].Node, w = _partOf[b].Node;
            tour.Add(w);
            steps.Accept(GraphSubsetFrames.EdgeId(a, b),
                $"Edge {{{a},{b}}} is on the cycle. It leaves {u} by its out-end {a} and enters {w} by its in-end {b}, so the tour goes {u} → {w}{(i == cycle.Count - 1 ? ", back to where it started." : ".")}");
        }
        return ("{" + string.Join(",", tour) + "}", new Tour(tour));
    }
}
