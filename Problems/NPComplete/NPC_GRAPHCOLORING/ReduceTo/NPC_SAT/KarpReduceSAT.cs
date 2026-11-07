using API.Interfaces;
using API.Interfaces.Logic;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo;
using API.Problems.NPComplete.NPC_SAT;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo.NPC_SAT;

// Karp's Graph Coloring to SAT: a variable per node and color (true when the node takes that color); per node a clause that it
// takes a color and one per pair of colors that it takes only one; per edge one clause per color that its two ends do not both
// take it. It records the construction steps (one per node, one per edge, then the Done; see BuildPieces) and the backward
// map's steps (one per node read; see MapBack).
class KarpReduceSAT : IReversibleReduction<GRAPHCOLORING, SAT, Groups>, IRecordedReduction<GRAPHCOLORING, SAT> {

    #region Fields
    public string reductionName { get; } = "Karp's SAT Reduction";
    public string reductionDefinition { get; } = "Karp's reduction converts each clause from a 3CNF into an OR gadgets to establish the truth assignments using labels.";
    public string source { get; } = "http://cs.bme.hu/thalg/3sat-to-3col.pdf.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "http://cs.bme.hu/thalg/3sat-to-3col.pdf.";
    public string[] contributors { get; } = { "Daniel Igbokwe" };
    // Dictionary<Object,List<string>> _gadgetMap = new Dictionary<object, List<string>>();

    // Per node, the nested "for i in 0..K: for j in 0..K" loop unconditionally emits
    // ~O(K^2) "not both colors i,j" clauses (the dedup check only trims by roughly
    // half), giving O(n * K^2) total clauses. Not the same thing as the pre-existing
    // `complexity` field below (that's an unrelated, unverified runtime-complexity note).
    public ReductionCost cost { get; } = ReductionCost.Cubic;

    // Declared, not derived. Per-node "exactly one color" OR-gadgets and per-edge
    // "not both same color" clauses are coordinated to enforce a global proper
    // coloring, not built independently per literal.
    public ReductionType reductionType { get; } = ReductionType.ComponentDesign;
    // Declared, not derived. Nested K x K loop per node plus a K x K loop per edge.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Polynomial;

    private GRAPHCOLORING _reductionFrom;
    private SAT _reductionTo;
    private string _complexity = "O(n^2)";

    #endregion

    #region Properties
    public string complexity {
        get {
            return _complexity;
        }

        set {
            _complexity = value;
        }
    }

    public GRAPHCOLORING reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }


    public SAT reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }

    #endregion


    #region Constructors
    public KarpReduceSAT(GRAPHCOLORING from) : this(from, StepRecorder<BuildDelta>.Off) { }

    // The one place the target is built; rec is Off except when the construction steps are asked for.
    private KarpReduceSAT(GRAPHCOLORING from, StepRecorder<BuildDelta> rec) {
        _reductionFrom = from;
        _reductionTo = Build(rec);
    }
    public KarpReduceSAT(string instance) : this(new GRAPHCOLORING(instance)) { }
    // Builds from the instance with rec recording the construction as it goes (IRecordedReduction).
    public KarpReduceSAT(string instance, StepRecorder<BuildDelta> rec) : this(new GRAPHCOLORING(instance), rec) { }
    public KarpReduceSAT() : this(new GRAPHCOLORING()) { }
    #endregion


    #region Methods

    /// <summary>
    /// The SAT variable that says the node at position nodeIndex (in the graph's node list) takes color color. The name
    /// is built from the node's position, not its name, so every node name gives a valid SAT variable and no two
    /// (node, color) pairs share one: "a-b" is not a SAT variable name, and "a1" with color 0 and "a" with color
    /// 10 would both write "a10".
    /// </summary>
    public static string VariableName(int nodeIndex, int color) => $"v{nodeIndex}_{color}";

    // Each node's position in the graph's node list, by name; a repeated name keeps its first position.
    private Dictionary<string, int> NodeIndexes() {
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < _reductionFrom.nodes.Count; i++)
            index.TryAdd(_reductionFrom.nodes[i], i);
        return index;
    }

    /// <summary>
    /// The gadgets: per node a group (the node to its clauses), per node and color an element (the node to the variable
    /// that says it takes that color), and per edge a group (the edge to its clauses). Ids are the Grouped Graph picture's.
    /// </summary>
    public List<Gadget> gadgets { get; } = new();

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new KarpReduceSAT(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    public SAT reduce() => Build(StepRecorder<BuildDelta>.Off);

    // Builds the formula. Each clause is filed under the node or edge that wrote it, and the construction steps are recorded
    // where the gadgets are made from them (see BuildPieces): one per node, one per edge, then the Done. Variables are named by
    // node position (VariableName).
    private SAT Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
        RequireNodesAndColors();
        var nodeClauses = new List<(string Node, int First, int Count)>();
        var edgeClauses = new List<(string A, string B, int First, int Count)>();

        Dictionary<string, int> index = NodeIndexes();
        string V(string node, int color) => VariableName(index[node], color);

        // List<string> reducedClauses = new List<string>();
        List<string> reducedNodeClauses = new List<string>();


        // convert nodes to clauses
        foreach (string node in _reductionFrom.nodes) {
            int firstOfNode = reducedNodeClauses.Count;
            string clause = "(";
            string currentNode = "";
            List<string> gadgetNodes = new List<string>();

            //foreach node create literals for each K - 1
            for (int i = 0; i < _reductionFrom.K - 1; i++) {
                currentNode = $"{node}_{i}";
                clause += $"{V(node, i)}|";
                gadgetNodes.Add(currentNode);
            }


            // last K literals  
            currentNode = $"{node}_{_reductionFrom.K - 1}";

            clause += $"{V(node, _reductionFrom.K - 1)})";
            reducedNodeClauses.Add(clause.Trim());

            // add to gadget dictionary 


            for (int i = 0; i < _reductionFrom.K; i++) {
                for (int j = 0; j < _reductionFrom.K; j++) {
                    if (i != j) {
                        string reverseClause = $"!({V(node, j)}&{V(node, i)})";
                        if (!reducedNodeClauses.Contains(reverseClause)) {
                            clause = $"!({V(node, i)}&{V(node, j)})";
                            reducedNodeClauses.Add(clause.Trim());
                        }
                    }
                }
            }
            nodeClauses.Add((node, firstOfNode, reducedNodeClauses.Count - firstOfNode));



        }

        //convert edges to clauses
        List<string> reducedEdgesClauses = new List<string>();

        foreach (var edge in reductionFrom.edges) {
            int firstOfEdge = reducedEdgesClauses.Count;
            for (int i = 0; i < _reductionFrom.K; i++) {
                for (int j = 0; j < _reductionFrom.K; j++) {
                    if (i == j) {

                        string reverseClause = $"!({V(edge.Value, j)}&{V(edge.Key, i)})";
                        if (!reducedEdgesClauses.Contains(reverseClause)) {
                            string clauseEdge = $"!({V(edge.Key, i)}&{V(edge.Value, j)})";
                            reducedEdgesClauses.Add(clauseEdge.Trim());
                        }
                    }
                }
            }
            edgeClauses.Add((edge.Key, edge.Value, reducedNodeClauses.Count + firstOfEdge, reducedEdgesClauses.Count - firstOfEdge));
        }

        List<string> reducedClauses = new List<string>();
        reducedClauses.AddRange(reducedNodeClauses);
        reducedClauses.AddRange(reducedEdgesClauses);
        DeMorgansLaw(reducedClauses);

        string phiInstance = "";
        for (int i = 0; i < reducedClauses.Count - 1; i++) {
            phiInstance += $"{reducedClauses[i]}&";
        }
        phiInstance += $"{reducedClauses[reducedClauses.Count - 1]}";

        var sat = new SAT(phiInstance);
        BuildPieces(sat.clauses, nodeClauses, edgeClauses, rec);
        return sat;
    }

    // The formula has a clause per node saying it takes one of the K colors. With no nodes there is no clause, and with K < 1
    // the clause has no literal; SAT writes neither (an empty formula, an empty clause), so the reduction refuses them as
    // input it cannot take: a 400 that says why, not a 500 or a parse error about a formula the caller never wrote.
    private void RequireNodesAndColors() {
        GRAPHCOLORING g = _reductionFrom;
        if (g.nodes.Count == 0)
            throw new ReductionInputException(this, g.instance, g.instanceFormat,
                "This reduction needs at least one node: a graph with no nodes gives SAT a formula with no clauses, which SAT cannot write.");
        if (g.K < 1)
            throw new ReductionInputException(this, g.instance, g.instanceFormat,
                $"K must be at least 1 for this reduction, but it is {g.K}: each node's clause lists its K colors, and SAT cannot write an empty clause.");
    }

    private void DeMorgansLaw(List<string> clauses) {

        for (int i = 0; i < clauses.Count; i++) {
            if (clauses[i].Contains("!(")) {
                string clause = clauses[i].Replace(" ", "").Replace("(", "").Replace(")", "").Replace("!", "");
                string[] literals = clause.Split('&');
                clauses[i] = $"(!{literals[0]}|!{literals[1]})";
            }
        }

    }

    // Each node's "takes a color" clause, by its index in the formula. Private, so it stays out of the JSON.
    private readonly Dictionary<string, int> _nodeClause = new(StringComparer.Ordinal);

    // How the construction steps show a node: the node in the graph; its clauses, their literals, the variables no earlier
    // step mentions and the formula picture's edges, added to SAT; and its gadgets. The state is (Node, FirstGadget, Pieces):
    // the node, the index of its first gadget (its group, then one element per color), and those pieces.
    private static RunSteps<(string Node, int FirstGadget, (string[] Nodes, BuildEdge[] Edges) Pieces), BuildDelta> NewNodeSteps(StepRecorder<BuildDelta> rec, int k) => new(rec,
        // What the Reduction View shows: the node, the pieces added, and the gadgets.
        partial: s => new BuildDelta { SourceFocus = [s.Node], Nodes = s.Pieces.Nodes, Edges = s.Pieces.Edges, Gadgets = Enumerable.Range(s.FirstGadget, 1 + k).ToArray() },
        // The clauses and their literals; the variables are not looked at.
        focus: s => s.Pieces.Nodes.Where(n => !n.StartsWith("v:", StringComparison.Ordinal)).ToArray());

    // How the construction steps show an edge: the edge and its two ends in the graph; its clauses, their literals, the
    // variables no earlier step mentions and the formula picture's edges, added to SAT; and its gadget. The state is (A, B,
    // Gadgets, Pieces): the edge's two ends, its gadget's index (none for an edge that writes no clause), and those pieces.
    private static RunSteps<(string A, string B, int[] Gadgets, (string[] Nodes, BuildEdge[] Edges) Pieces), BuildDelta> NewEdgeSteps(StepRecorder<BuildDelta> rec) => new(rec,
        // What the Reduction View shows: the edge, the pieces added, and the gadget.
        partial: s => new BuildDelta {
            SourceFocus = s.A == s.B ? [s.A] : [GraphSubsetFrames.EdgeId(s.A, s.B), s.A, s.B],
            Nodes = s.Pieces.Nodes,
            Edges = s.Pieces.Edges,
            Gadgets = s.Gadgets,
        },
        // The clauses and their literals; the variables are not looked at.
        focus: s => s.Pieces.Nodes.Where(n => !n.StartsWith("v:", StringComparison.Ordinal)).ToArray());

    // Makes the gadgets from the clauses each node and edge wrote, recording the construction steps as it goes when rec
    // records (see IRecordedReduction). Its steps:
    //   - an Accept per node: its K variables, its clause that it takes a color, and its clauses that it takes only one;
    //   - an Accept per edge: its clauses that its two ends do not take the same color, or a Reject when it writes none (it
    //     was already written the other way round);
    //   - the Done: the formula can be satisfied exactly when the graph can be colored.
    // A construction has no verdicts, so Accept means "these pieces go into the target" and Reject "this edge adds nothing".
    // Each step adds the clauses it wrote, with their literals, the variables no earlier step mentions and the formula
    // picture's edges (GroupedGraphFrames.ClausePieces).
    // Gadgets: per node a group (to its clauses) and an element per color (to its variable), then per edge that writes a
    // clause a group (to its clauses).
    private void BuildPieces(List<List<string>> clauses, List<(string Node, int First, int Count)> nodeClauses,
        List<(string A, string B, int First, int Count)> edgeClauses, StepRecorder<BuildDelta> rec) {
        int k = _reductionFrom.K;
        List<string> ClauseIds(int first, int count) =>
            Enumerable.Range(first, count).SelectMany(c => GroupedGraphFrames.ClauseIds(c, clauses[c].Count)).ToList();
        Dictionary<string, int> index = NodeIndexes();
        string Variable(string node, int color) => VariableName(index[node], color);

        // The variables an earlier step already added to the formula picture.
        var builtVariables = new HashSet<string>(StringComparer.Ordinal);
        // What a step's clauses add to the formula picture. Finding their edges compares each of their literals with every
        // earlier one, and only the picture needs them, so it is called only when the steps are recorded.
        (string[] Nodes, BuildEdge[] Edges) Pieces(int first, int count) {
            var nodes = new List<string>();
            var edges = new List<BuildEdge>();
            for (int c = first; c < first + count; c++) {
                var (n, e) = GroupedGraphFrames.ClausePieces(clauses, c, builtVariables);
                nodes.AddRange(n);
                edges.AddRange(e);
            }
            return (nodes.ToArray(), BuildEdge.Sorted(edges));
        }

        // One group and K elements per node, and one step.
        var nodeSteps = NewNodeSteps(rec, k);
        _nodeClause.Clear();
        foreach (var (node, first, count) in nodeClauses) {
            _nodeClause.TryAdd(node, first);
            var ids = ClauseIds(first, count);
            int firstGadget = gadgets.Count;
            gadgets.Add(new Gadget(GadgetKind.Group, new List<string> { node }, ids, legacyToIds: GroupedGraphFrames.LegacyIds(ids, clauses)));
            for (int color = 0; color < k; color++) {
                string id = GroupedGraphFrames.VariableId(Variable(node, color));
                gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { node }, new List<string> { id },
                    legacyToIds: GroupedGraphFrames.LegacyIds([id], clauses), note: $"{Variable(node, color)} is true when {node} takes color {color}."));
            }
            var pieces = rec.Enabled ? Pieces(first, count) : default;
            // A node with only its "takes a color" clause (K = 1, or a name repeated in the graph, whose other clauses are
            // already written), or one with clauses that it takes only one color, which needs K of at least 2.
            if (count == 1)
                nodeSteps.Accept((node, firstGadget, pieces),
                    $"Node {node} becomes the {(k == 1 ? "variable" : "variables")} {BuildCaptions.Names(Enumerable.Range(0, k).Select(c => Variable(node, c)).ToList())}, one per color, and {Words.Count(count, "clause")}: {GroupedGraphFrames.ClauseText(clauses[first])} says {node} takes a color.");
            else
                nodeSteps.Accept((node, firstGadget, pieces),
                    $"Node {node} becomes the variables {BuildCaptions.Names(Enumerable.Range(0, k).Select(c => Variable(node, c)).ToList())}, one per color, and {Words.Count(count, "clause")}: {GroupedGraphFrames.ClauseText(clauses[first])} says {node} takes a color, and {BuildCaptions.Names(Enumerable.Range(first + 1, count - 1).Select(c => GroupedGraphFrames.ClauseText(clauses[c])).ToList(), 4)} {(count == 2 ? "says" : "say")} it takes only one.");
        }

        // Then a group per edge that writes a clause, and one step.
        var edgeSteps = NewEdgeSteps(rec);
        foreach (var (a, b, first, count) in edgeClauses) {
            int[] used = [];
            if (count > 0) {
                var ids = ClauseIds(first, count);
                used = [gadgets.Count];
                gadgets.Add(new Gadget(GadgetKind.Group, new List<string> { a == b ? a : GraphSubsetFrames.EdgeId(a, b) }, ids,
                    legacyToIds: GroupedGraphFrames.LegacyIds(ids, clauses)));
            }
            var pieces = rec.Enabled ? Pieces(first, count) : default;
            if (count == 0)
                edgeSteps.Reject((a, b, used, pieces), $"Edge {{{a},{b}}} was already written the other way round, so it adds no clause.");
            else
                edgeSteps.Accept((a, b, used, pieces),
                    $"Edge {{{a},{b}}} becomes {Words.Count(count, "clause")}, {BuildCaptions.Names(Enumerable.Range(first, count).Select(c => GroupedGraphFrames.ClauseText(clauses[c])).ToList(), 4)}: {a} and {b} never take the same color.");
        }

        // The closing step, on either recorder (both record to rec).
        edgeSteps.Done(new BuildDelta(), true,
            $"The formula can be satisfied exactly when the graph can be colored with {Words.Count(k, "color")}: a node takes the color whose variable is true.");
    }

    public Groups EmptyAnswer() => new Groups([]);

    // A SAT answer is an assignment, never "{}": the target keeps the default (no answer). The graph has a node, so the
    // empty coloring is never a Graph Coloring answer either.

    // How the map-back steps show a node read: the coloring so far, and the id the step is about (the variable that colors
    // the node, or, for a node with none, its "takes a color" clause).
    private static RunSteps<string, Groups> NewMapSteps(StepRecorder<Groups> rec, Dictionary<string, int> color) => new(rec,
        // What every picture shows: the coloring so far, and the piece the step is about.
        partial: _ => new Groups(color),
        focus: id => [id]);

    // Backward map: each node, in the graph's order, takes the first color whose variable the assignment makes true. Graph
    // Coloring's verifier decides whether the coloring is a real one.
    // Its steps: an Accept per node with a true color variable (it takes that color), and a Reject per node with none (it
    // gets no color). The caller adds the Done.
    public (string Answer, Groups Partial) MapBack(string problemToSolution, StepRecorder<Groups> rec) {
        var clauses = reductionTo.clauses;
        var values = SAT.ReadAssignment(problemToSolution, CnfFormula.VariablesOf(clauses), "SAT");
        int k = _reductionFrom.K;
        var color = new Dictionary<string, int>(StringComparer.Ordinal);
        var steps = NewMapSteps(rec, color);
        Dictionary<string, int> index = NodeIndexes();
        foreach (string node in _reductionFrom.nodes) {
            string Variable(int c) => VariableName(index[node], c);
            int own = _nodeClause[node];
            int c = Enumerable.Range(0, k).FirstOrDefault(i => values.TryGetValue(Variable(i), out bool v) && v, -1);
            if (c < 0) {
                steps.Reject(GroupedGraphFrames.ClauseId(own), $"No variable of {node} is true, so {node} gets no color.");
                continue;
            }
            color[node] = c;
            steps.Accept(GroupedGraphFrames.VariableId(Variable(c)), $"{Variable(c)} = True, so node {node} takes color {c}.");
        }
        string answer = "{" + string.Join(",", Enumerable.Range(0, k)
            .Select(c => _reductionFrom.nodes.Where(n => color.TryGetValue(n, out int x) && x == c).ToList())
            .Where(cls => cls.Count > 0)
            .Select(cls => "{" + string.Join(",", cls) + "}")) + "}";
        return (answer, new Groups(color));
    }

    // A K-coloring sets the variable of each node and its color True and the node's other color variables False: every
    // node then has exactly one color (its clause and the at-most-one clauses hold), and no edge has both ends True for one
    // color.
    public string mapSolutions(string problemFromSolution) {
        Dictionary<string, int> color = ColoringAnswer.Read(this, reductionFrom, problemFromSolution);
        var values = new List<string>();
        Dictionary<string, int> index = NodeIndexes();
        foreach (string node in reductionFrom.nodes)
            for (int i = 0; i < reductionFrom.K; i++)
                values.Add($"{VariableName(index[node], i)}:{(color[node] == i ? "True" : "False")}");
        return "(" + string.Join(",", values) + ")";
    }
    #endregion

}






