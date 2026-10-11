using API.Interfaces;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo;
using API.Problems.NPComplete.NPC_SAT;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo.NPC_SAT;

class KarpReduceSAT : IReduction<GRAPHCOLORING, SAT> {

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
    public KarpReduceSAT(GRAPHCOLORING from) {
        _reductionFrom = from;
        _reductionTo = reduce();
    }
    public KarpReduceSAT(string instance) : this(new GRAPHCOLORING(instance)) { }
    public KarpReduceSAT() : this(new GRAPHCOLORING()) { }
    #endregion


    #region Methods

    public SAT reduce() {
        RequireNodesAndColors();

        // List<string> reducedClauses = new List<string>();
        List<string> reducedNodeClauses = new List<string>();


        // convert nodes to clauses
        foreach (string node in _reductionFrom.nodes) {
            string clause = "(";
            string currentNode = "";
            List<string> gadgetNodes = new List<string>();

            //foreach node create literals for each K - 1
            for (int i = 0; i < _reductionFrom.K - 1; i++) {
                currentNode = $"{node}_{i}";
                clause += $"{node}{i}|";
                gadgetNodes.Add(currentNode);
            }


            // last K literals  
            currentNode = $"{node}_{_reductionFrom.K - 1}";

            clause += $"{node}{_reductionFrom.K - 1})";
            reducedNodeClauses.Add(clause.Trim());

            // add to gadget dictionary 


            for (int i = 0; i < _reductionFrom.K; i++) {
                for (int j = 0; j < _reductionFrom.K; j++) {
                    if (i != j) {
                        string reverseClause = $"!({node}{j}&{node}{i})";
                        if (!reducedNodeClauses.Contains(reverseClause)) {
                            clause = $"!({node}{i}&{node}{j})";
                            reducedNodeClauses.Add(clause.Trim());
                        }
                    }
                }
            }



        }

        //convert edges to clauses
        List<string> reducedEdgesClauses = new List<string>();

        foreach (var edge in reductionFrom.edges) {
            for (int i = 0; i < _reductionFrom.K; i++) {
                for (int j = 0; j < _reductionFrom.K; j++) {
                    if (i == j) {

                        string reverseClause = $"!({edge.Value}{j}&{edge.Key}{i})";
                        if (!reducedEdgesClauses.Contains(reverseClause)) {
                            string clauseEdge = $"!({edge.Key}{i}&{edge.Value}{j})";
                            reducedEdgesClauses.Add(clauseEdge.Trim());
                        }
                    }
                }
            }
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

        return new SAT(phiInstance);
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

    // A K-coloring sets the variable of each node and its color True and the node's other color variables False: every
    // node then has exactly one color (its clause and the at-most-one clauses hold), and no edge has both ends True for one
    // color (#823).
    public string mapSolutions(string problemFromSolution) {
        Dictionary<string, int> color = ColoringAnswer.Read(this, reductionFrom, problemFromSolution);
        var values = new List<string>();
        foreach (string node in reductionFrom.nodes)
            for (int i = 0; i < reductionFrom.K; i++)
                values.Add($"{node}{i}:{(color[node] == i ? "True" : "False")}");
        return "(" + string.Join(",", values) + ")";
    }
    #endregion

}






