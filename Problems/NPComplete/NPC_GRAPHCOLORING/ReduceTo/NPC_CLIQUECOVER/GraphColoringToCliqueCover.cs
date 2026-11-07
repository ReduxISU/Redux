using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System.Text.Json.Serialization;
using API.Interfaces;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_CLIQUECOVER;
using SPADE;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo.NPC_CLIQUECOVER;

// Karp's Graph Coloring to Clique Cover: the same nodes, an edge for every pair of nodes the graph does not join (its
// complement), and the same K. It records the construction steps (the nodes copied, one per node for the complement edges
// its turn adds, then K; see Build) and the backward map's steps (one per node of the graph; see MapBack).
class GraphColoringToCliqueCover : IReversibleReduction<GRAPHCOLORING, CLIQUECOVER, Groups>, IRecordedReduction<GRAPHCOLORING, CLIQUECOVER> {

    // --- Fields ---
    public string reductionName { get; } = "Clique Cover Reduction";
    public string reductionDefinition { get; } = "Karp's Reduction from Graph Coloring to Clique Cover";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    // reduce() enumerates all non-adjacent node pairs (a complement-graph-like
    // structure) — up to O(n^2) pairs even when the input graph is sparse.
    public ReductionCost cost { get; } = ReductionCost.Quadratic;

    // Declared, not derived. Same certificate reinterpreted on the complement graph --
    // coloring becomes a partition into independent sets, i.e. a clique cover of the
    // complement -- a restriction, not a gadget construction.
    public ReductionType reductionType { get; } = ReductionType.Restriction;
    // Declared, not derived. Nested node x node loop enumerating non-adjacent pairs.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Polynomial;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(n^2), n = |GRAPHCOLORING.nodes|";
    public List<Gadget> gadgets { get; }
    private GRAPHCOLORING _reductionFrom;
    private CLIQUECOVER _reductionTo;


    // --- Properties ---
    public GRAPHCOLORING reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public CLIQUECOVER reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }



    // --- Methods Including Constructors ---
    public GraphColoringToCliqueCover(GRAPHCOLORING from) : this(from, StepRecorder<BuildDelta>.Off) { }

    // The one place the target is built; rec is Off except when the construction steps are asked for.
    private GraphColoringToCliqueCover(GRAPHCOLORING from, StepRecorder<BuildDelta> rec) {
        gadgets = new();
        _reductionFrom = from;
        _reductionTo = Build(rec);

    }
    public GraphColoringToCliqueCover(string instance) : this(new GRAPHCOLORING(instance)) { }
    // Builds from the instance with rec recording the construction as it goes (IRecordedReduction).
    public GraphColoringToCliqueCover(string instance, StepRecorder<BuildDelta> rec) : this(new GRAPHCOLORING(instance), rec) { }
    public GraphColoringToCliqueCover() : this(new GRAPHCOLORING()) { }

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new GraphColoringToCliqueCover(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    public CLIQUECOVER reduce() => Build(StepRecorder<BuildDelta>.Off);

    // How the construction steps show the nodes copied: every node of the graph, the same nodes added to the new graph, and
    // their element gadgets (the first elementGadgets gadgets, one per node). The state is the graph's nodes.
    private static RunSteps<List<string>, BuildDelta> NewCopySteps(StepRecorder<BuildDelta> rec, int elementGadgets) => new(rec,
        // What the Reduction View shows: the nodes in both graphs, and the gadgets that copy them.
        partial: nodes => new BuildDelta { SourceFocus = nodes.ToArray(), Nodes = nodes.ToArray(), Gadgets = Enumerable.Range(0, elementGadgets).ToArray() },
        focus: nodes => nodes.ToArray());

    // How the construction steps show a node's turn: the node in the graph, the complement edges its turn adds, and its edge
    // rule. The state is (Node, Later, Rule): the node, the nodes it gets an edge to, and its edge rule's index (-1 when its
    // turn adds no edge).
    private static RunSteps<(string Node, List<string> Later, int Rule), BuildDelta> NewNodeSteps(StepRecorder<BuildDelta> rec) => new(rec,
        // What the Reduction View shows: the node, the edges added, and the edge rule that adds them.
        partial: s => new BuildDelta {
            SourceFocus = [s.Node],
            Edges = BuildEdge.Sorted(s.Later.Select(v => new BuildEdge(s.Node, v))),
            Gadgets = s.Rule >= 0 ? [s.Rule] : [],
        },
        focus: s => [s.Node, .. s.Later]);

    // Builds the clique cover graph, recording the construction steps as it goes (see IRecordedReduction). Its steps:
    //   - an Accept for the nodes: every node of the graph is copied, with the same name;
    //   - one step per node, in the graph's order: an Accept when its turn adds edges (to the nodes it has not met yet and
    //     has no edge to), or a Reject when it adds none (it is the last node, or it has an edge to every node not met yet);
    //   - the Done: K stays the same.
    // A construction has no verdicts, so Accept means "these pieces go into the target" and Reject "this node's turn adds
    // nothing to it". The steps come from the very decisions below, so what is recorded is what is built.
    // Gadgets: an element per node, an edge rule per node whose turn adds edges, then the bound (K unchanged).
    private CLIQUECOVER Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
        CLIQUECOVER reducedCLIQUECOVER = new CLIQUECOVER();

        string instance = "(({";
        foreach (var node in reductionFrom.nodes) {
            instance += node + ',';
        }

        // The complement: every pair of distinct nodes that G does not join, in either order.
        var pairs = new List<string>();
        // The complement edges each node's turn adds, each pair once (the first time it is listed).
        var perNode = new List<(string Node, List<string> Later)>();
        var joined = new HashSet<string>(StringComparer.Ordinal);
        instance = instance.TrimEnd(',') + "},{";
        foreach (var node in reductionFrom.nodes) {
            var later = new List<string>();
            foreach (var node2 in reductionFrom.nodes) {
                KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(node, node2);
                KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(node2, node);
                if (!(reductionFrom.edges.Contains(pairCheck1) || reductionFrom.edges.Contains(pairCheck2)) && node != node2) {
                    pairs.Add("{" + node + ',' + node2 + "}");
                    if (joined.Add(GraphSubsetFrames.EdgeId(node, node2))) later.Add(node2);
                }
            }
            perNode.Add((node, later));
        }

        foreach (UtilCollection node in reductionFrom.graph.Nodes) {
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string>() { node.ToString() }, new List<string>() { node.ToString() }));
        }
        var copySteps = NewCopySteps(rec, gadgets.Count);
        var allNodes = reductionFrom.nodes;
        if (allNodes.Count > 0)
            copySteps.Accept(allNodes, $"Copy every node of the graph ({BuildCaptions.Names(allNodes)}) into the new graph, with the same names. Its edges will be the pairs the graph is missing.");
        else
            copySteps.Accept(allNodes, $"The graph has no nodes, so the new graph has none either.");

        // An edge rule per node whose turn adds complement edges: the pairs the graph is missing, filed under the node whose
        // turn added them. Each node's turn is then one step.
        var nodeSteps = NewNodeSteps(rec);
        var edgeRuleOf = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int n = 0; n < perNode.Count; n++) {
            var (u, later) = perNode[n];
            if (later.Count == 0) {
                if (n == perNode.Count - 1)
                    nodeSteps.Reject((u, later, -1), $"Node {u} is the last node: the steps above already gave it its edges in the complement.");
                else
                    nodeSteps.Reject((u, later, -1), $"Node {u} is already joined to every node it has not met yet, so it gets no new edges.");
                continue;
            }
            if (!edgeRuleOf.TryGetValue(u, out int rule)) {
                edgeRuleOf[u] = rule = gadgets.Count;
                gadgets.Add(new Gadget(GadgetKind.EdgeRule, new List<string> { u }, later.Select(v => new BuildEdge(u, v).Id).ToList(),
                    note: $"{u} is joined to the nodes it has no edge to in the graph."));
            }
            nodeSteps.Accept((u, later, rule),
                $"Node {u} has no edge to {BuildCaptions.Names(later)} in the graph, so it gets {(later.Count > 1 ? "those edges" : "that edge")} in the complement.");
        }
        // Then the bound: K stays the same. The new graph has no piece of its own for it.
        int bound = gadgets.Count;
        gadgets.Add(new Gadget(GadgetKind.Bound, new List<string>(), new List<string>(), note: $"K stays {reductionFrom.K}"));
        // --- Generate G string for new CLIQUE ---

        instance += string.Join(",", pairs) + "})," + reductionFrom.K.ToString() + ')';

        // Parsed from its own text, so its graph is this one and not the default instance's.
        reducedCLIQUECOVER = new CLIQUECOVER(instance);

        // The closing step, on either recorder (both record to rec).
        int k = reductionFrom.K;
        nodeSteps.Done(new BuildDelta { Bound = k, Gadgets = [bound] }, true,
            $"K stays {k}. A color class has no edge in the graph, so its nodes are a clique in the complement: the graph can be colored with {k} colors exactly when the complement can be covered by {k} cliques.");

        reductionTo = reducedCLIQUECOVER;
        return reducedCLIQUECOVER;
    }

    // A K-coloring is a cover of the complement by K cliques: a color class has no edge in G, so every two of its nodes are
    // joined in the complement. Each color class is one clique.
    public string mapSolutions(string problemFromSolution) {
        Dictionary<string, int> color = ColoringAnswer.Read(this, reductionFrom, problemFromSolution);
        var cliques = color.GroupBy(p => p.Value).OrderBy(g => g.Key).Select(g => "{" + string.Join(",", g.Select(p => p.Key)) + "}");
        return "{" + string.Join(",", cliques) + "}";
    }

    public Groups EmptyAnswer() => new Groups([]);

    // "{}" on either side keeps the default (the verifiers): only a graph with no nodes is covered by no clique, and
    // colored by no color.

    // How the map-back steps show a node read: the coloring so far, and the node.
    private static RunSteps<string, Groups> NewMapSteps(StepRecorder<Groups> rec, Dictionary<string, int> color) => new(rec,
        // What every picture shows: the colors given so far, and the node the step is about.
        partial: _ => new Groups(color),
        focus: node => [node]);

    /// <summary>
    /// Backward map: a clique of the complement has no edge in the graph, so it is one color class. Each node, in the
    /// graph's order, takes the color of the clique it is in (the n-th clique the answer lists with a node in it is color
    /// n); a node in no clique gets no color (a Reject), and the Done step reports what Graph Coloring's verifier says.
    /// Its steps, one per node of the graph: an Accept for a node in a clique (it takes that clique's color), a Reject for a
    /// node in none. The caller adds the Done.
    /// </summary>
    public (string Answer, Groups Partial) MapBack(string problemToSolution, StepRecorder<Groups> rec) {
        string text = problemToSolution.Trim();
        if (text.Length < 2 || text[0] != '{' || text[^1] != '}')
            throw new FormatException($"'{problemToSolution}' is not a set of cliques in braces like {{{{a,b}},{{c}}}}");
        Groups listed = GraphGroupFrames.ParseClasses(text);
        foreach (string node in listed.Of.Keys)
            if (!reductionTo.nodes.Contains(node)) throw new FormatException($"'{node}' is not a node of the graph");
        // Number the cliques that hold a node, in the order listed.
        var colorOf = listed.Of.Values.Distinct().Order().Select((g, c) => (g, c)).ToDictionary(p => p.g, p => p.c);

        var color = new Dictionary<string, int>(StringComparer.Ordinal);
        var steps = NewMapSteps(rec, color);
        foreach (string node in reductionFrom.nodes) {
            if (listed.Of.TryGetValue(node, out int clique)) {
                int c = colorOf[clique];
                color[node] = c;
                steps.Accept(node, $"Node {node} is in clique {c + 1} of the complement, so it takes color {c + 1}.");
            } else {
                steps.Reject(node, $"Node {node} is in no clique, so it gets no color.");
            }
        }
        string answer = "{" + string.Join(",", Enumerable.Range(0, colorOf.Count)
            .Select(c => "{" + string.Join(",", reductionFrom.nodes.Where(n => color.TryGetValue(n, out int x) && x == c)) + "}")) + "}";
        return (answer, new Groups(color));
    }
}
// return an instance of what you are reducing to