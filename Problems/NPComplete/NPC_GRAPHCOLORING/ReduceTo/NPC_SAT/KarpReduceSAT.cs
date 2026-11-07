using API.Interfaces;
using API.Interfaces.Logic;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo;
using API.Problems.NPComplete.NPC_SAT;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo.NPC_SAT;

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
    /// (node, color) pairs share one (#825): "a-b" is not a SAT variable name, and "a1" with color 0 and "a" with color
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
    /// The gadgets (#694): per node a group (the node to its clauses), per node and color an element (the node to the variable
    /// that says it takes that color), and per edge a group (the edge to its clauses). Ids are the Grouped Graph picture's.
    /// </summary>
    public List<Gadget> gadgets { get; } = new();

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new KarpReduceSAT(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    public SAT reduce() => Build(StepRecorder<BuildDelta>.Off);

    // Construction steps (see IRecordedReduction): one per node (its variables and clauses), one per edge (its clauses), then
    // Done. Each clause is filed under the node or edge that wrote it; variables are named by node position (VariableName, #825).
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
        RecordPieces(sat.clauses, nodeClauses, edgeClauses, rec);
        return sat;
    }

    // The formula has a clause per node saying it takes one of the K colors. With no nodes there is no clause, and with K < 1
    // the clause has no literal; SAT writes neither (an empty formula, an empty clause), so the reduction refuses them as
    // input it cannot take: a 400 that says why, not a 500 or a parse error about a formula the caller never wrote (#827).
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

    // The gadgets, and the construction steps when recording: one per node, then one per edge, each adding the clauses it
    // wrote (with their literals, the variables they first mention and the Grouped Graph's edges, GroupedGraphFrames.ClausePieces).
    private void RecordPieces(List<List<string>> clauses, List<(string Node, int First, int Count)> nodeClauses,
        List<(string A, string B, int First, int Count)> edgeClauses, StepRecorder<BuildDelta> rec) {
        int k = _reductionFrom.K;
        List<string> ClauseIds(int first, int count) =>
            Enumerable.Range(first, count).SelectMany(c => GroupedGraphFrames.ClauseIds(c, clauses[c].Count)).ToList();
        Dictionary<string, int> index = NodeIndexes();
        string Variable(string node, int color) => VariableName(index[node], color);

        var nodeGadgets = new List<(string Node, int First, int Count, int[] Gadgets)>();
        _nodeClause.Clear();
        foreach (var (node, first, count) in nodeClauses) {
            _nodeClause.TryAdd(node, first);
            var ids = ClauseIds(first, count);
            var used = new List<int> { gadgets.Count };
            gadgets.Add(new Gadget(GadgetKind.Group, new List<string> { node }, ids, legacyToIds: GroupedGraphFrames.LegacyIds(ids, clauses)));
            for (int color = 0; color < k; color++) {
                string id = GroupedGraphFrames.VariableId(Variable(node, color));
                used.Add(gadgets.Count);
                gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { node }, new List<string> { id },
                    legacyToIds: GroupedGraphFrames.LegacyIds([id], clauses), note: $"{Variable(node, color)} is true when {node} takes color {color}."));
            }
            nodeGadgets.Add((node, first, count, used.ToArray()));
        }
        var edgeGadgets = new List<(string A, string B, int First, int Count, int[] Gadgets)>();
        foreach (var (a, b, first, count) in edgeClauses) {
            int[] used = [];
            if (count > 0) {
                var ids = ClauseIds(first, count);
                used = [gadgets.Count];
                gadgets.Add(new Gadget(GadgetKind.Group, new List<string> { a == b ? a : GraphSubsetFrames.EdgeId(a, b) }, ids,
                    legacyToIds: GroupedGraphFrames.LegacyIds(ids, clauses)));
            }
            edgeGadgets.Add((a, b, first, count, used));
        }
        if (!rec.Enabled) return;

        var builtVariables = new HashSet<string>(StringComparer.Ordinal);
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
        string Texts(int first, int count) => BuildCaptions.Names(Enumerable.Range(first, count).Select(c => GroupedGraphFrames.ClauseText(clauses[c])).ToList(), 4);

        foreach (var (node, first, count, used) in nodeGadgets) {
            var (nodes, edges) = Pieces(first, count);
            string[] focus = nodes.Where(n => !n.StartsWith("v:", StringComparison.Ordinal)).ToArray();
            string variables = BuildCaptions.Names(Enumerable.Range(0, k).Select(c => Variable(node, c)).ToList());
            rec.Accept(
                () => new BuildDelta { SourceFocus = [node], Nodes = nodes, Edges = edges, Gadgets = used },
                () => $"Node {node} becomes the {(k == 1 ? "variable" : "variables")} {variables}, one per color, and {Words.Count(count, "clause")}: "
                    + $"{GroupedGraphFrames.ClauseText(clauses[first])} says {node} takes a color"
                    + (count > 1 ? $", and {Texts(first + 1, count - 1)} {(count == 2 ? "says" : "say")} it takes only one." : "."),
                focus);
        }
        foreach (var (a, b, first, count, used) in edgeGadgets) {
            string[] sourceFocus = a == b ? [a] : [GraphSubsetFrames.EdgeId(a, b), a, b];
            if (count == 0) {
                rec.Reject(() => new BuildDelta { SourceFocus = sourceFocus },
                    () => $"Edge {{{a},{b}}} was already written the other way round, so it adds no clause.");
                continue;
            }
            var (nodes, edges) = Pieces(first, count);
            string[] focus = nodes.Where(n => !n.StartsWith("v:", StringComparison.Ordinal)).ToArray();
            rec.Accept(
                () => new BuildDelta { SourceFocus = sourceFocus, Nodes = nodes, Edges = edges, Gadgets = used },
                () => $"Edge {{{a},{b}}} becomes {Words.Count(count, "clause")}, {Texts(first, count)}: {a} and {b} never take the same color.",
                focus);
        }
        rec.Done(new BuildDelta(), true,
            $"The formula can be satisfied exactly when the graph can be colored with {Words.Count(k, "color")}: a node takes the color whose variable is true.");
    }

    public Groups EmptyAnswer() => new Groups([]);

    // A SAT answer is an assignment, never "{}": the target keeps the default (no answer). The graph has a node (#827), so the
    // empty coloring is never a Graph Coloring answer either.

    /// <summary>
    /// Backward map (#694): each node, in the graph's order, takes the first color whose variable the assignment makes
    /// true. A node with none gets no color (a Reject on its "takes a color" clause), and Graph Coloring's verifier decides.
    /// </summary>
    public (string Answer, Groups Partial) MapBack(string problemToSolution, StepRecorder<Groups> rec) {
        var clauses = reductionTo.clauses;
        var values = SAT.ReadAssignment(problemToSolution, CnfFormula.VariablesOf(clauses), "SAT");
        int k = _reductionFrom.K;
        var color = new Dictionary<string, int>(StringComparer.Ordinal);
        Dictionary<string, int> index = NodeIndexes();
        foreach (string node in _reductionFrom.nodes) {
            string Variable(int c) => VariableName(index[node], c);
            int own = _nodeClause[node];
            int c = Enumerable.Range(0, k).FirstOrDefault(i => values.TryGetValue(Variable(i), out bool v) && v, -1);
            if (c < 0) {
                rec.Reject(() => new Groups(color), () => $"No variable of {node} is true, so {node} gets no color.", GroupedGraphFrames.ClauseId(own));
                continue;
            }
            color[node] = c;
            rec.Accept(() => new Groups(color), () => $"{Variable(c)} = True, so node {node} takes color {c}.", GroupedGraphFrames.VariableId(Variable(c)));
        }
        string answer = "{" + string.Join(",", Enumerable.Range(0, k)
            .Select(c => _reductionFrom.nodes.Where(n => color.TryGetValue(n, out int x) && x == c).ToList())
            .Where(cls => cls.Count > 0)
            .Select(cls => "{" + string.Join(",", cls) + "}")) + "}";
        return (answer, new Groups(color));
    }

    // A K-coloring sets the variable of each node and its color True and the node's other color variables False: every
    // node then has exactly one color (its clause and the at-most-one clauses hold), and no edge has both ends True for one
    // color (#823).
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






