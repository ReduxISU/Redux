using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System.Text.Json.Serialization;
using API.Interfaces;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_CLIQUECOVER;
using SPADE;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo.NPC_CLIQUECOVER;

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

    // Construction steps (see IRecordedReduction): the nodes copied, one step per node for the complement edges its turn
    // adds, then K. Gadgets (#694): an element per node, an edge rule per node whose turn adds edges, and the bound.
    private CLIQUECOVER Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
        CLIQUECOVER reducedCLIQUECOVER = new CLIQUECOVER();

        string instance = "(({";
        foreach (var node in reductionFrom.nodes) {
            instance += node + ',';
        }

        // The complement: every pair of distinct nodes that G does not join, in either order (#822).
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
        int elementGadgets = gadgets.Count;
        var edgeRuleOf = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (node, later) in perNode) {
            if (later.Count == 0 || edgeRuleOf.ContainsKey(node)) continue;
            edgeRuleOf[node] = gadgets.Count;
            gadgets.Add(new Gadget(GadgetKind.EdgeRule, new List<string> { node }, later.Select(v => new BuildEdge(node, v).Id).ToList(),
                note: $"{node} is joined to the nodes it has no edge to in the graph."));
        }
        int bound = gadgets.Count;
        gadgets.Add(new Gadget(GadgetKind.Bound, new List<string>(), new List<string>(), note: $"K stays {reductionFrom.K}"));
        // --- Generate G string for new CLIQUE ---

        instance += string.Join(",", pairs) + "})," + reductionFrom.K.ToString() + ')';

        // Parsed from its own text, so its graph is this one and not the default instance's (#823).
        reducedCLIQUECOVER = new CLIQUECOVER(instance);

        if (rec.Enabled) {
            string[] all = reductionFrom.nodes.ToArray();
            int[] elementIndex = Enumerable.Range(0, elementGadgets).ToArray();
            rec.Accept(
                () => new BuildDelta { SourceFocus = all, Nodes = all, Gadgets = elementIndex },
                () => all.Length > 0
                    ? $"Copy every node of the graph ({BuildCaptions.Names(all)}) into the new graph, with the same names. Its edges will be the pairs the graph is missing."
                    : "The graph has no nodes, so the new graph has none either.",
                all);
            for (int n = 0; n < perNode.Count; n++) {
                var (u, later) = perNode[n];
                bool lastNode = n == perNode.Count - 1;
                BuildEdge[] added = BuildEdge.Sorted(later.Select(v => new BuildEdge(u, v)));
                int[] rule = edgeRuleOf.TryGetValue(u, out int r) && later.Count > 0 ? [r] : [];
                Func<BuildDelta> delta = () => new BuildDelta { SourceFocus = [u], Edges = added, Gadgets = rule };
                Func<string> caption = () => later.Count > 0
                    ? $"Node {u} has no edge to {BuildCaptions.Names(later)} in the graph, so it gets {(later.Count > 1 ? "those edges" : "that edge")} in the complement."
                    : lastNode
                        ? $"Node {u} is the last node: the steps above already gave it its edges in the complement."
                        : $"Node {u} is already joined to every node it has not met yet, so it gets no new edges.";
                string[] focus = new[] { u }.Concat(later).ToArray();
                if (later.Count > 0) rec.Accept(delta, caption, focus); else rec.Reject(delta, caption, focus);
            }
            int k = reductionFrom.K;
            rec.Done(new BuildDelta { Bound = k, Gadgets = [bound] }, true,
                $"K stays {k}. A color class has no edge in the graph, so its nodes are a clique in the complement: "
                + $"the graph can be colored with {k} colors exactly when the complement can be covered by {k} cliques.");
        }

        reductionTo = reducedCLIQUECOVER;
        return reducedCLIQUECOVER;
    }

    // A K-coloring is a cover of the complement by K cliques: a color class has no edge in G, so every two of its nodes are
    // joined in the complement. Each color class is one clique (#823).
    public string mapSolutions(string problemFromSolution) {
        Dictionary<string, int> color = ColoringAnswer.Read(this, reductionFrom, problemFromSolution);
        var cliques = color.GroupBy(p => p.Value).OrderBy(g => g.Key).Select(g => "{" + string.Join(",", g.Select(p => p.Key)) + "}");
        return "{" + string.Join(",", cliques) + "}";
    }

    public Groups EmptyAnswer() => new Groups([]);

    // "{}" on either side keeps the default (the verifiers): only a graph with no nodes is covered by no clique, and
    // colored by no color.

    /// <summary>
    /// Backward map (#694): a clique of the complement has no edge in the graph, so it is one color class. Each node, in the
    /// graph's order, takes the color of the clique it is in (the n-th clique the answer lists with a node in it is color
    /// n); a node in no clique gets no color (a Reject), and the Done step reports what Graph Coloring's verifier says.
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
        foreach (string node in reductionFrom.nodes) {
            if (listed.Of.TryGetValue(node, out int clique)) {
                int c = colorOf[clique];
                color[node] = c;
                rec.Accept(() => new Groups(color), () => $"Node {node} is in clique {c + 1} of the complement, so it takes color {c + 1}.", node);
            } else {
                rec.Reject(() => new Groups(color), () => $"Node {node} is in no clique, so it gets no color.", node);
            }
        }
        string answer = "{" + string.Join(",", Enumerable.Range(0, colorOf.Count)
            .Select(c => "{" + string.Join(",", reductionFrom.nodes.Where(n => color.TryGetValue(n, out int x) && x == c)) + "}")) + "}";
        return (answer, new Groups(color));
    }
}
// return an instance of what you are reducing to