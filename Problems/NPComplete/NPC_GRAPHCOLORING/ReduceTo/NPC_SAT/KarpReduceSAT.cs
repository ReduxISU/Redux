using API.Interfaces;
using API.Problems.NPComplete.NPC_SAT;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo.NPC_SAT;

class KarpReduceSAT : IReduction<GRAPHCOLORING, SAT> {

    #region Fields
    public string reductionName { get; } = "Karp's SAT Reduction";
    public string reductionDefinition { get; } = "Karp's reduction converts each clause from a 3CNF into an OR gadgets to establish the truth assignments using labels.";
    public string source { get; } = "http://cs.bme.hu/thalg/3sat-to-3col.pdf.";
    public string sourceLink { get; } = "http://cs.bme.hu/thalg/3sat-to-3col.pdf.";
    public string[] contributors { get; } = { "Daniel Igbokwe" };
    // Dictionary<Object,List<string>> _gadgetMap = new Dictionary<object, List<string>>();

    // Per node, the nested "for i in 0..K: for j in 0..K" loop unconditionally emits
    // ~O(K^2) "not both colors i,j" clauses (the dedup check only trims by roughly
    // half), giving O(n * K^2) total clauses. Not the same thing as the pre-existing
    // `complexity` field below (that's an unrelated, unverified runtime-complexity note).
    public ReductionCost cost { get; } = ReductionCost.Cubic;

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

    private void DeMorgansLaw(List<string> clauses) {

        for (int i = 0; i < clauses.Count; i++) {
            if (clauses[i].Contains("!(")) {
                string clause = clauses[i].Replace(" ", "").Replace("(", "").Replace(")", "").Replace("!", "");
                string[] literals = clause.Split('&');
                clauses[i] = $"(!{literals[0]}|!{literals[1]})";
            }
        }

    }

    public string mapSolutions(string problemFromSolution) {
        return "";
    }
    #endregion

}






