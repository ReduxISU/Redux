using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_VERTEXCOVER;
using SPADE;

namespace API.Problems.NPComplete.NPC_CLIQUE.ReduceTo.NPC_VertexCover;

// Sipser's Clique to Vertex Cover: the same nodes, an edge for every pair of nodes the clique graph does not join (its
// complement), and K' = N - K. It records the construction steps (the nodes copied, one per node for the complement edges
// its turn adds, then K'; see Build) and the backward map's steps (one per node of the graph; see MapBack).
class sipserReductionVertexCover : IReversibleReduction<CLIQUE, VERTEXCOVER, NodeSet>, IRecordedReduction<CLIQUE, VERTEXCOVER> {


    // --- Fields ---
    public string reductionName { get; } = "Sipser's Vertex Cover Reduction";
    public string reductionDefinition { get; } = @"This Sipsers reduction converts the Clique problem into a Vertex Cover problem.
                                            This is done by first taking all possible edges in the original clique graph, and removing
                                            the edges that are actually in the clique graph from that set.";
    public string source { get; } = "Sipser, Michael. Introduction to the Theory of Computation.ACM Sigact News 27.1 (1996): 27-29.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Janita Aamir", "Alex Diviney", "Caleb Eardley" };
    // reduce() explicitly materializes the complement graph (all node pairs minus the
    // clique's own edges) — up to O(n^2) edges even when the input only encodes O(n+m).
    public ReductionCost cost { get; } = ReductionCost.Quadratic;

    // Declared, not derived. Same certificate reinterpreted on the complement graph --
    // the canonical Garey & Johnson restriction between Clique/Independent Set/Vertex
    // Cover, not a gadget construction.
    public ReductionType reductionType { get; } = ReductionType.Restriction;
    // Declared, not derived. Two nested node x node passes to build then dedupe the
    // complement edge set.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Polynomial;

    public List<Gadget> gadgets { get; }
    private CLIQUE _reductionFrom;
    private VERTEXCOVER _reductionTo;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(n^2), n = |CLIQUE.nodes|";

    public CLIQUE reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public VERTEXCOVER reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }

    // --- Methods Including Constructors ---
    public sipserReductionVertexCover(CLIQUE from) : this(from, StepRecorder<BuildDelta>.Off) { }

    // The one place the target is built; rec is Off except when Rebuild asks for the construction steps.
    private sipserReductionVertexCover(CLIQUE from, StepRecorder<BuildDelta> rec) {
        gadgets = new();
        _reductionFrom = from;
        _reductionTo = Build(rec);
    }

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new sipserReductionVertexCover(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    public sipserReductionVertexCover(string instance) : this(new CLIQUE(instance)) { }
    // Builds from the instance with rec recording the construction as it goes (IRecordedReduction): the Reduction View
    // and /buildSteps use it so that one build gives both the reduction and its steps.
    public sipserReductionVertexCover(string instance, StepRecorder<BuildDelta> rec) : this(new CLIQUE(instance), rec) { }
    public sipserReductionVertexCover() : this(new CLIQUE()) { }

    /// <summary>
    /// Reduces a CLIQUE instance to a VERTEXCOVER instance.
    /// </summary>
    /// <returns> A Vertexcover instance</returns>
    /// <remarks>
    /// authored by Janita Aamir. Contributed to by Alex Diviney.
    /// </remarks>
    public VERTEXCOVER reduce() => Build(StepRecorder<BuildDelta>.Off);

    // How the construction steps show the nodes copied: every node of the clique graph, the same nodes added to the new
    // graph, and their element gadgets (the first elementGadgets gadgets, one per node). The state is the graph's nodes.
    private static RunSteps<List<string>, BuildDelta> NewCopySteps(StepRecorder<BuildDelta> rec, int elementGadgets) => new(rec,
        // What the Reduction View shows: the nodes in both graphs, and the gadgets that copy them.
        partial: nodes => new BuildDelta { SourceFocus = nodes.ToArray(), Nodes = nodes.ToArray(), Gadgets = Enumerable.Range(0, elementGadgets).ToArray() },
        focus: nodes => nodes.ToArray());

    // How the construction steps show a node's turn: the node in the clique graph, the complement edges its turn adds, and
    // its edge rule. The state is (Node, Later, Rule): the node, the nodes it gets an edge to, and its edge rule's index (-1
    // when its turn adds no edge).
    private static RunSteps<(string Node, string[] Later, int Rule), BuildDelta> NewNodeSteps(StepRecorder<BuildDelta> rec) => new(rec,
        // What the Reduction View shows: the node, the edges added, and the edge rule that adds them.
        partial: s => new BuildDelta {
            SourceFocus = [s.Node],
            Edges = BuildEdge.Sorted(s.Later.Select(v => new BuildEdge(s.Node, v))),
            Gadgets = s.Rule >= 0 ? [s.Rule] : [],
        },
        focus: s => [s.Node, .. s.Later]);

    // Builds the vertex cover graph, recording the construction steps as it goes (see IRecordedReduction). Its steps:
    //   - an Accept for the nodes: every node of the clique graph is copied, with the same name;
    //   - one step per node, in the graph's order: an Accept when its turn adds edges (to the later nodes it has no edge
    //     to), or a Reject when it adds none (it is the last node, or it already has an edge to every later node);
    //   - the Done: K' = N - K.
    // A construction has no verdicts, so Accept means "these pieces go into the target" and Reject "this node's turn adds
    // nothing to it". The steps come from the very decisions below, so what is recorded is what is built.
    // Gadgets: an element per node, an edge rule per node whose turn adds edges, then the bound (K').
    private VERTEXCOVER Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
        CLIQUE CLIQUEInstance = _reductionFrom;
        RequireKWithinNodeCount(CLIQUEInstance);
        VERTEXCOVER reducedVERTEXCOVER = new VERTEXCOVER();

        // Assign clique nodes to vertexcover nodes.
        reducedVERTEXCOVER.nodes = CLIQUEInstance.nodes;

        List<KeyValuePair<string, string>> edges = new List<KeyValuePair<string, string>>();

        //this nested loop creates every possible combination of edges that aren't self edges between the nodes in the set and adds them to a list. 
        // ie. nodes 1,2,3 become edges {1,2},{2,1},{1,3},{3,1},{2,3},{3,2}
        for (int i = 0; i < reducedVERTEXCOVER.nodes.Count; i++) {
            for (int j = 0; j < reducedVERTEXCOVER.nodes.Count; j++) {
                if (reducedVERTEXCOVER.nodes[i] != reducedVERTEXCOVER.nodes[j]) {
                    KeyValuePair<string, string> fullEdge = new KeyValuePair<string, string>(reducedVERTEXCOVER.nodes[i], reducedVERTEXCOVER.nodes[j]);
                    edges.Add(fullEdge);
                }
            }
        }

        //for every edge in clique, removes the edge from the total list of edges.
        for (int i = 0; i < CLIQUEInstance.edges.Count; i++) {
            edges.Remove(new KeyValuePair<string, string>(CLIQUEInstance.edges[i].Key, CLIQUEInstance.edges[i].Value));
            edges.Remove(new KeyValuePair<string, string>(CLIQUEInstance.edges[i].Value, CLIQUEInstance.edges[i].Key));
        }

        //For every edge in the remaining set, removes any edge that would be redundant. So if we have {1,3} and {3,1} then we only leave {1,3}
        for (int i = 0; i < edges.Count; i++) {
            for (int j = 0; j < edges.Count; j++) {
                if (edges[i].Key == edges[j].Value && edges[i].Value == edges[j].Key) {
                    edges.Remove(new KeyValuePair<string, string>(edges[j].Key, edges[j].Value));
                }
            }
        }

        foreach (UtilCollection node in CLIQUEInstance.graph.Nodes) {
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string>() { node.ToString() }, new List<string>() { node.ToString() }));
        }
        var copySteps = NewCopySteps(rec, gadgets.Count);
        var allNodes = CLIQUEInstance.nodes;
        if (allNodes.Count > 0)
            copySteps.Accept(allNodes, $"Copy every node of the clique graph ({BuildCaptions.Names(allNodes)}) into the new graph, with the same names. Its edges will be the pairs the clique graph is missing.");
        else
            copySteps.Accept(allNodes, $"The clique graph has no nodes, so the new graph has none either.");

        // An edge rule per node that gets complement edges: the pairs the clique graph is missing, filed under their first
        // node. Each node's turn is then one step.
        var nodeSteps = NewNodeSteps(rec);
        var edgeRuleOf = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int n = 0; n < allNodes.Count; n++) {
            string u = allNodes[n];
            string[] later = edges.Where(e => e.Key == u).Select(e => e.Value).ToArray();
            if (later.Length == 0) {
                if (n == allNodes.Count - 1)
                    nodeSteps.Reject((u, later, -1), $"Node {u} is the last node: the steps above already gave it its edges in the complement.");
                else
                    nodeSteps.Reject((u, later, -1), $"Node {u} is already joined to every later node in the clique graph, so it gets no new edges.");
                continue;
            }
            if (!edgeRuleOf.TryGetValue(u, out int rule)) {
                edgeRuleOf[u] = rule = gadgets.Count;
                gadgets.Add(new Gadget(GadgetKind.EdgeRule, new List<string> { u }, later.Select(v => new BuildEdge(u, v).Id).ToList(),
                    note: $"{u} is joined to the nodes it has no edge to in the clique graph."));
            }
            nodeSteps.Accept((u, later, rule),
                $"Node {u} has no edge to {BuildCaptions.Names(later)} in the clique graph, so it gets {(later.Length > 1 ? "those edges" : "that edge")} in the complement.");
        }
        // Then the bound, K' = N - K. The new graph has no piece of its own for it.
        int boundGadget = gadgets.Count;
        int coverSize = CLIQUEInstance.nodes.Count - CLIQUEInstance.K;
        gadgets.Add(new Gadget(GadgetKind.Bound, new List<string>(), new List<string>(),
            note: $"K' = N - K = {CLIQUEInstance.nodes.Count} - {CLIQUEInstance.K} = {coverSize}"));
        // --- Generate G string for new CLIQUE ---
        string nodesString = "";
        foreach (string nodes in CLIQUEInstance.nodes) {
            nodesString += nodes + ",";
        }
        nodesString = nodesString.Trim(',');
        string edgesString = "";
        foreach (KeyValuePair<string, string> edge in edges) {
            edgesString += "{" + edge.Key + "," + edge.Value + "}" + ",";
        }
        edgesString = edgesString.Trim(',');
        int vertexKInt = (CLIQUEInstance.nodes.Count - CLIQUEInstance.K); //N - K where N is number of nodes of k-clique and K is the k of k-clique
        string G = "(({" + nodesString + "},{" + edgesString + "})," + vertexKInt.ToString() + ")";

        reducedVERTEXCOVER = new VERTEXCOVER(G);
        reductionTo = reducedVERTEXCOVER;

        // The closing step, on either recorder (both record to rec).
        nodeSteps.Done(new BuildDelta { Bound = vertexKInt, Gadgets = [boundGadget] }, true,
            $"K' is N - K = {CLIQUEInstance.nodes.Count} - {CLIQUEInstance.K} = {vertexKInt}. The clique graph has a clique of size {CLIQUEInstance.K} exactly when the complement has a vertex cover of size {vertexKInt}.");
        return reducedVERTEXCOVER;

    }

    // The Vertex Cover budget is K' = N - K, which is only right for 0 <= K <= N. A K above N would give a
    // negative budget, and a negative K a budget above N (every graph has such a cover, though no clique has a
    // negative size). Either is refused as input this reduction cannot take (a 400 from the endpoints) rather
    // than built into a Vertex Cover instance that does not mean the same thing.
    private void RequireKWithinNodeCount(CLIQUE clique) {
        int n = clique.nodes.Count;
        if (clique.K < 0 || clique.K > n)
            throw new ReductionInputException(this, clique.instance, clique.instanceFormat,
                $"K must be between 0 and the number of nodes ({n}) for this reduction, but it is {clique.K}: the vertex cover size N - K would be {n - clique.K}.");
    }

    public string mapSolutions(string problemFromSolution) {
        //Parse problemFromSolution into a list of nodes
        List<string> solutionList = GraphParser.parseNodeListWithStringFunctions(problemFromSolution);

        //Map solution
        List<string> mappedSolutionList = new List<string>();
        foreach (string node in reductionFrom.nodes) {
            if (!solutionList.Contains(node)) {
                mappedSolutionList.Add(node);
            }
        }
        string problemToSolution = "";
        foreach (string node in mappedSolutionList) {
            problemToSolution += node + ',';
        }
        return '{' + problemToSolution.TrimEnd(',') + '}';

    }

    public NodeSet EmptyAnswer() => new NodeSet([]);

    // "{}" on either side keeps the default (the verifiers, which read it as the empty set): the empty cover is
    // real when B's graph has no edges (K = N in A, so B's budget is 0), the empty clique only when A's K = 0.

    // How the map-back steps show a node read: the clique so far, and the node.
    private static RunSteps<string, NodeSet> NewMapSteps(StepRecorder<NodeSet> rec, List<string> clique) => new(rec,
        // What every picture shows: the clique so far, and the node the step is about.
        partial: _ => new NodeSet(clique),
        focus: node => [node]);

    // Backward map: the clique is the nodes the cover leaves out (an independent set in the complement
    // graph is a clique in the original). A cover smaller than N-K leaves out more than K nodes; any K
    // of them are still a clique, so the first K (in the graph's node order) are kept.
    // Its steps, one per node of the graph in its order: a Reject for a node in the cover, an Accept for a node outside it
    // (it goes in the clique), and a Reject for a node outside it once the clique has K nodes. The caller adds the Done.
    public (string Answer, NodeSet Partial) MapBack(string problemToSolution, StepRecorder<NodeSet> rec) {
        var cover = new HashSet<string>(ReductionBack.ParseNodeSet(problemToSolution, reductionTo.nodes));
        int k = reductionFrom.K;

        var clique = new List<string>();
        var steps = NewMapSteps(rec, clique);
        foreach (string node in reductionFrom.nodes) {
            if (cover.Contains(node)) {
                steps.Reject(node, $"Node {node} is in the vertex cover, so it is left out of the clique.");
            } else if (clique.Count < k) {
                clique.Add(node);
                steps.Accept(node, $"Node {node} is not in the vertex cover, so it is in the clique.");
            } else {
                steps.Reject(node, $"Node {node} is not in the cover, but the clique already has {k} nodes, so it is left out.");
            }
        }
        return (ReductionBack.FormatNodeSet(clique), new NodeSet(clique));
    }
}