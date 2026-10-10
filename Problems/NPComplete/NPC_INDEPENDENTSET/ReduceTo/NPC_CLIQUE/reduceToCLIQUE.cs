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

    /// <summary>
    /// Reduces a CLIQUE instance to a VERTEXCOVER instance.
    /// </summary>
    /// <returns> A Vertexcover instance</returns>
    /// <remarks>
    /// authored by Janita Aamir. Contributed to by Alex Diviney.
    /// </remarks>
    public CLIQUE reduce() => Build(StepRecorder<BuildDelta>.Off);

    // Construction steps (see IRecordedReduction): the nodes copied, one step per node for the complement
    // edges it gets, then K unchanged. The per-node edges are collected inside the loop that builds them.
    private CLIQUE Build(StepRecorder<BuildDelta> rec) {
        INDEPENDENTSET INDPENDENTSETInstance = _reductionFrom;
        CLIQUE reducedCLIQUE = new CLIQUE();
        reducedCLIQUE.nodes = INDPENDENTSETInstance.nodes;

        List<KeyValuePair<string, string>> edges = new List<KeyValuePair<string, string>>();

        bool recording = rec.Enabled;
        List<(string Node, string[] Later)>? perNode = recording ? new() : null;
        foreach (var i in INDPENDENTSETInstance.nodes) {
            int before = edges.Count;
            foreach (var j in INDPENDENTSETInstance.nodes) {
                KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(i, j);
                KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(j, i);
                if (!(INDPENDENTSETInstance.edges.Contains(pairCheck1) || INDPENDENTSETInstance.edges.Contains(pairCheck2) || i.Equals(j) || edges.Contains(pairCheck1) || edges.Contains(pairCheck2))) {
                    edges.Add(pairCheck1);
                }
            }
            if (recording) perNode!.Add((i, edges.Skip(before).Select(e => e.Value).ToArray()));
        }

        //set up gadgets
        foreach (UtilCollection node in INDPENDENTSETInstance.graph.Nodes) {
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string>() { node.ToString() }, new List<string>() { node.ToString() }));
        }

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

        if (recording) {
            string[] all = INDPENDENTSETInstance.nodes.ToArray();
            int[] gadgetIdx = Enumerable.Range(0, gadgets.Count).ToArray();
            rec.Accept(
                () => new BuildDelta { SourceFocus = all, Nodes = all, Gadgets = gadgetIdx },
                () => $"Copy every node of the graph ({BuildCaptions.Names(all)}) into the clique graph, with the same names. Its edges will be the pairs the graph is missing.",
                all);
            foreach (var (node, later) in perNode!) {
                string u = node;
                string[] added = later.Select(v => BuildCaptions.Edge(u, v)).ToArray();
                Func<BuildDelta> delta = () => new BuildDelta { SourceFocus = new[] { u }, Edges = added };
                Func<string> caption = () => later.Length > 0
                    ? $"Node {u} has no edge to {BuildCaptions.Names(later)} in the original graph, so it gets {(later.Length > 1 ? "those edges" : "that edge")} in the complement."
                    : $"Node {u} is already joined to every later node in the original graph, so it gets no new edges.";
                string[] focus = new[] { u }.Concat(later).ToArray();
                if (later.Length > 0) rec.Accept(delta, caption, focus); else rec.Reject(delta, caption, focus);
            }
            int k = INDPENDENTSETInstance.K;
            rec.Done(new BuildDelta { Bound = k }, true,
                $"K stays {k}. {k} nodes with no edge between them in the original graph are {k} nodes joined to each other in the complement.");
        }
        return reducedCLIQUE;

    }

    public string mapSolutions(string problemFromSolution) {
        return problemFromSolution;
    }

    public NodeSet EmptyAnswer() => new NodeSet([]);

    // Backward map: reduce() keeps the nodes and complements the edges, so a clique of the complement
    // graph is exactly an independent set of the original. The same nodes are the answer; if the clique
    // is larger than K, any K of its nodes are still independent, so the first K are kept.
    public string MapSolutionBack(string problemToSolution, StepRecorder<NodeSet> rec) {
        List<string> cliqueNodes = ReductionBack.ParseNodeSet(problemToSolution, reductionTo.nodes);
        int k = reductionFrom.K;

        var independent = new List<string>();
        foreach (string node in cliqueNodes) {
            if (independent.Contains(node)) continue;
            if (independent.Count < k) {
                independent.Add(node);
                rec.Accept(() => new NodeSet(independent), () => $"Node {node} is in the clique of the complement graph, so it shares no edge with the others in the original: it is in the independent set.", node);
            } else {
                rec.Reject(() => new NodeSet(independent), () => $"Node {node} is in the clique, but the independent set already has {k} nodes, so it is left out.", node);
            }
        }

        bool ok = independent.Count == k;
        rec.Done(new NodeSet(independent), ok, ok
            ? $"The {k} clique nodes are an independent set of size {k}."
            : $"The clique has only {independent.Count} nodes, fewer than the {k} the independent set needs.");
        return ReductionBack.FormatNodeSet(independent);
    }
}
