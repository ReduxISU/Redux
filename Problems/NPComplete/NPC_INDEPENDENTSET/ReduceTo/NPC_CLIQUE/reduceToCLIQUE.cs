using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_INDEPENDENTSET;
using SPADE;

namespace API.Problems.NPComplete.NPC_INDEPENDENTSET.ReduceTo.NPC_CLIQUE;

// Independent Set to Clique: the same nodes, an edge for every pair of nodes the graph does not join (its complement), and
// the same K. It records the construction steps (the nodes copied, one per node for the complement edges its turn adds,
// then K; see Build) and the backward map's steps (one per clique node read; see MapBack).
class reduceToCLIQUE : IReversibleReduction<INDEPENDENTSET, CLIQUE, NodeSet>, IRecordedReduction<INDEPENDENTSET, CLIQUE> {


    // --- Fields ---
    public string reductionName { get; } = "Clique Reduction";
    public string reductionDefinition { get; } = @"This reduction converts an independent set problem into a clique problem, 
                                            by taking the complement of the graph, or inverting all the edges.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Independent_set_(graph_theory)#Relationship_to_other_graph_parameters";
    public string[] contributors { get; } = { "Russell Phillips" };
    // reduce() materializes the complement graph (all node pairs that are NOT edges
    // in the input) — up to O(n^2) edges even when the input only encodes O(n+m).
    public ReductionCost cost { get; } = ReductionCost.Quadratic;

    // Declared, not derived. Same certificate reinterpreted on the complement graph --
    // the canonical Garey & Johnson restriction between Independent Set and Clique.
    public ReductionType reductionType { get; } = ReductionType.Restriction;
    // Declared, not derived. Nested node x node loop building the complement edge set.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Polynomial;

    public List<Gadget> gadgets { get; }
    private INDEPENDENTSET _reductionFrom;
    private CLIQUE _reductionTo;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(n^2), n = |INDEPENDENTSET.nodes|";


    // --- Properties ---
    public INDEPENDENTSET reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public CLIQUE reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }

    // --- Methods Including Constructors ---
    public reduceToCLIQUE(INDEPENDENTSET from) : this(from, StepRecorder<BuildDelta>.Off) { }

    // The one place the target is built; rec is Off except when Rebuild asks for the construction steps.
    private reduceToCLIQUE(INDEPENDENTSET from, StepRecorder<BuildDelta> rec) {
        gadgets = new();
        _reductionFrom = from;
        _reductionTo = Build(rec);
    }

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new reduceToCLIQUE(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    public reduceToCLIQUE(string from) : this(new INDEPENDENTSET(from)) {

    }
    public reduceToCLIQUE() : this(new INDEPENDENTSET()) { }
    // Builds from the instance with rec recording the construction as it goes (IRecordedReduction): the Reduction View
    // and /buildSteps use it so that one build gives both the reduction and its steps.
    public reduceToCLIQUE(string instance, StepRecorder<BuildDelta> rec) : this(new INDEPENDENTSET(instance), rec) { }

    /// <summary>
    /// Reduces an INDEPENDENTSET instance to a CLIQUE instance.
    /// </summary>
    /// <returns> A Clique instance</returns>
    /// <remarks>
    /// authored by Janita Aamir. Contributed to by Alex Diviney.
    /// </remarks>
    public CLIQUE reduce() => Build(StepRecorder<BuildDelta>.Off);

    // How the construction steps show the nodes copied: every node of the graph, the same nodes added to the clique graph,
    // and their element gadgets (the first elementGadgets gadgets, one per node). The state is the graph's nodes.
    private static RunSteps<List<string>, BuildDelta> NewCopySteps(StepRecorder<BuildDelta> rec, int elementGadgets) => new(rec,
        // What the Reduction View shows: the nodes in both graphs, and the gadgets that copy them.
        partial: nodes => new BuildDelta { SourceFocus = nodes.ToArray(), Nodes = nodes.ToArray(), Gadgets = Enumerable.Range(0, elementGadgets).ToArray() },
        focus: nodes => nodes.ToArray());

    // How the construction steps show a node's turn: the node in the graph, the complement edges its turn adds, and its edge
    // rule. The state is (Node, Later, Rule): the node, the nodes it gets an edge to, and its edge rule's index (-1 when its
    // turn adds no edge).
    private static RunSteps<(string Node, string[] Later, int Rule), BuildDelta> NewNodeSteps(StepRecorder<BuildDelta> rec) => new(rec,
        // What the Reduction View shows: the node, the edges added, and the edge rule that adds them.
        partial: s => new BuildDelta {
            SourceFocus = [s.Node],
            Edges = BuildEdge.Sorted(s.Later.Select(v => new BuildEdge(s.Node, v))),
            Gadgets = s.Rule >= 0 ? [s.Rule] : [],
        },
        focus: s => [s.Node, .. s.Later]);

    // Builds the clique graph, recording the construction steps as it goes (see IRecordedReduction). Its steps:
    //   - an Accept for the nodes: every node of the graph is copied, with the same name;
    //   - one step per node, in the graph's order: an Accept when its turn adds edges (to the later nodes it has no edge
    //     to), or a Reject when it adds none (it is the last node, or it already has an edge to every later node);
    //   - the Done: K stays the same.
    // A construction has no verdicts, so Accept means "these pieces go into the target" and Reject "this node's turn adds
    // nothing to it". The steps come from the very decisions below, so what is recorded is what is built.
    // Gadgets: an element per node, an edge rule per node whose turn adds edges, then the bound (K unchanged).
    private CLIQUE Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
        INDEPENDENTSET INDPENDENTSETInstance = _reductionFrom;
        CLIQUE reducedCLIQUE = new CLIQUE();
        reducedCLIQUE.nodes = INDPENDENTSETInstance.nodes;

        List<KeyValuePair<string, string>> edges = new List<KeyValuePair<string, string>>();

        var nodeSteps = NewNodeSteps(rec);
        // The complement edges each node's turn adds: the later nodes it has no edge to in the graph.
        var perNodeEdges = new List<(string Node, string[] Later)>();
        foreach (var i in INDPENDENTSETInstance.nodes) {
            int before = edges.Count;
            foreach (var j in INDPENDENTSETInstance.nodes) {
                KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(i, j);
                KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(j, i);
                if (!(INDPENDENTSETInstance.edges.Contains(pairCheck1) || INDPENDENTSETInstance.edges.Contains(pairCheck2) || i.Equals(j) || edges.Contains(pairCheck1) || edges.Contains(pairCheck2))) {
                    edges.Add(pairCheck1);
                }
            }
            perNodeEdges.Add((i, edges.Skip(before).Select(e => e.Value).ToArray()));
        }

        //set up gadgets
        foreach (UtilCollection node in INDPENDENTSETInstance.graph.Nodes) {
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string>() { node.ToString() }, new List<string>() { node.ToString() }));
        }
        var copySteps = NewCopySteps(rec, gadgets.Count);
        var allNodes = INDPENDENTSETInstance.nodes;
        if (allNodes.Count > 0)
            copySteps.Accept(allNodes, $"Copy every node of the graph ({BuildCaptions.Names(allNodes)}) into the clique graph, with the same names. Its edges will be the pairs the graph is missing.");
        else
            copySteps.Accept(allNodes, $"The graph has no nodes, so the clique graph has none either.");

        // An edge rule per node that gets complement edges: the pairs the graph is missing, filed under the node whose turn
        // added them. Each node's turn is then one step.
        var edgeRuleOf = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int n = 0; n < perNodeEdges.Count; n++) {
            var (u, later) = perNodeEdges[n];
            if (later.Length == 0) {
                if (n == perNodeEdges.Count - 1)
                    nodeSteps.Reject((u, later, -1), $"Node {u} is the last node: the steps above already gave it its edges in the complement.");
                else
                    nodeSteps.Reject((u, later, -1), $"Node {u} is already joined to every later node in the original graph, so it gets no new edges.");
                continue;
            }
            if (!edgeRuleOf.TryGetValue(u, out int rule)) {
                edgeRuleOf[u] = rule = gadgets.Count;
                gadgets.Add(new Gadget(GadgetKind.EdgeRule, new List<string> { u }, later.Select(v => new BuildEdge(u, v).Id).ToList(),
                    note: $"{u} is joined to the nodes it has no edge to in the graph."));
            }
            nodeSteps.Accept((u, later, rule),
                $"Node {u} has no edge to {BuildCaptions.Names(later)} in the original graph, so it gets {(later.Length > 1 ? "those edges" : "that edge")} in the complement.");
        }
        // Then the bound: K stays the same. The clique graph has no piece of its own for it.
        int boundGadget = gadgets.Count;
        gadgets.Add(new Gadget(GadgetKind.Bound, new List<string>(), new List<string>(), note: $"K stays {INDPENDENTSETInstance.K}"));

        // --- Generate G string for new CLIQUE ---
        string nodesString = "";
        foreach (string nodes in INDPENDENTSETInstance.nodes) {
            nodesString += nodes + ",";
        }
        nodesString = nodesString.Trim(',');

        string edgesString = "";
        foreach (KeyValuePair<string, string> edge in edges) {
            edgesString += "{" + edge.Key + "," + edge.Value + "}" + ",";
        }
        edgesString = edgesString.Trim(',');

        string G = "(({" + nodesString + "},{" + edgesString + "})," + INDPENDENTSETInstance.K.ToString() + ")";

        reducedCLIQUE = new CLIQUE(G);
        reductionTo = reducedCLIQUE;

        // The closing step, on either recorder (both record to rec).
        int k = INDPENDENTSETInstance.K;
        nodeSteps.Done(new BuildDelta { Bound = k, Gadgets = [boundGadget] }, true,
            $"K stays {k}. {k} nodes with no edge between them in the original graph are {k} nodes joined to each other in the complement.");
        return reducedCLIQUE;

    }

    public string mapSolutions(string problemFromSolution) {
        return problemFromSolution;
    }

    public NodeSet EmptyAnswer() => new NodeSet([]);

    // "{}" is a real answer on either side only when K = 0 (B's K is A's K); both verifiers read it as the empty
    // set, so TargetAcceptsEmpty and SourceAcceptsEmpty keep the default.

    // How the map-back steps show a clique node read: the independent set so far, and the node.
    private static RunSteps<string, NodeSet> NewMapSteps(StepRecorder<NodeSet> rec, List<string> independent) => new(rec,
        // What every picture shows: the independent set so far, and the node the step is about.
        partial: _ => new NodeSet(independent),
        focus: node => [node]);

    // Backward map: reduce() keeps the nodes and complements the edges, so a clique of the complement
    // graph is exactly an independent set of the original. The same nodes are the answer; if the clique
    // is larger than K, any K of its nodes are still independent, so the first K are kept.
    // Its steps: an Accept per clique node put in the independent set, and a Reject per clique node past the first K (it is
    // left out). The caller adds the Done.
    public (string Answer, NodeSet Partial) MapBack(string problemToSolution, StepRecorder<NodeSet> rec) {
        List<string> cliqueNodes = ReductionBack.ParseNodeSet(problemToSolution, reductionTo.nodes);
        int k = reductionFrom.K;

        var independent = new List<string>();
        var steps = NewMapSteps(rec, independent);
        foreach (string node in cliqueNodes) {
            if (independent.Contains(node)) continue;
            if (independent.Count < k) {
                independent.Add(node);
                steps.Accept(node, $"Node {node} is in the clique of the complement graph, so it shares no edge with the others in the original: it is in the independent set.");
            } else {
                steps.Reject(node, $"Node {node} is in the clique, but the independent set already has {k} nodes, so it is left out.");
            }
        }
        return (ReductionBack.FormatNodeSet(independent), new NodeSet(independent));
    }
}
