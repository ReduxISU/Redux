using API.Interfaces;
using API.Problems.NPComplete.NPC_SAT;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo.NPC_SAT;

class KarpReduceSAT : IReduction<GRAPHCOLORING, SAT>
{

    #region Fields
    private string _reductionName = "Karp's SAT Reduction";
    private string _reductionDefinition = "Karp's reduction converts each clause from a 3CNF into an OR gadgets to establish the truth assignments using labels.";
    private string _source = "http://cs.bme.hu/thalg/3sat-to-3col.pdf.";
    private string[] _contributors = {"Daniel Igbokwe"};
    // Dictionary<Object,List<string>> _gadgetMap = new Dictionary<object, List<string>>();
    
    private Dictionary<Object,Object> _gadgetMap = new Dictionary<Object,Object>();
    private GRAPHCOLORING _reductionFrom;
    private SAT _reductionTo;
    private string _complexity = "O(n^2)";

    #endregion

    #region Properties
    public string reductionName
    {
        get
        {
            return _reductionName;
        }
    }
    public string reductionDefinition
    {
        get
        {
            return _reductionDefinition;
        }
    }
    public string source
    {
        get
        {
            return _source;
        }

    }
    public string[] contributors{
        get{
            return _contributors;
        }
    }
    public string complexity
    {
        get
        {
            return _complexity;
        }

        set
        {
            _complexity = value;
        }
    }

    public Dictionary<Object,Object> gadgetMap {
        get{
            return _gadgetMap;
        }
        set{
            _gadgetMap = value;
        }
    }
    public GRAPHCOLORING reductionFrom
    {
        get
        {
            return _reductionFrom;
        }
        set
        {
            _reductionFrom = value;
        }
    }


    public SAT reductionTo
    {
        get
        {
            return _reductionTo;
        }
        set
        {
            _reductionTo = value;
        }
    }

    #endregion


    #region Constructors
    public KarpReduceSAT(GRAPHCOLORING from)
    {
        _reductionFrom = from;
        _reductionTo = reduce();
    }
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
            _gadgetMap[node] = gadgetNodes;
            

            for (int i = 0; i < _reductionFrom.K; i++)
            {
                for (int j = 0; j < _reductionFrom.K; j++)
                {
                    if (i != j)
                    {
                        string reverseClause = $"!({node}{j}&{node}{i})";
                        if (!reducedNodeClauses.Contains(reverseClause))
                        {
                            clause = $"!({node}{i}&{node}{j})";
                            reducedNodeClauses.Add(clause.Trim());
                        }
                    }
                }
            }

         

        }

        //convert edges to clauses
        List<string> reducedEdgesClauses = new List<string>();

        foreach (var edge in reductionFrom.edges)
        {
            for (int i = 0; i < _reductionFrom.K; i++)
            {
                for (int j = 0; j < _reductionFrom.K; j++)
                {
                    if (i == j)
                    {

                        string reverseClause = $"!({edge.Value}{j}&{edge.Key}{i})";
                        if (!reducedEdgesClauses.Contains(reverseClause))
                        {
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
        for (int i = 0; i < reducedClauses.Count - 1; i++){
            phiInstance += $"{reducedClauses[i]}&";
        }
        phiInstance += $"{reducedClauses[reducedClauses.Count - 1]}";

        return new SAT(phiInstance);
    }

    private void DeMorgansLaw(List<string> clauses)
    {

        for (int i = 0; i < clauses.Count; i++)
        {
            if (clauses[i].Contains("!("))
            {
                string clause = clauses[i].Replace(" ", "").Replace("(", "").Replace(")", "").Replace("!", "");
                string[] literals = clause.Split('&');
                clauses[i] = $"(!{literals[0]}|!{literals[1]})";
            }
        }

    }

    public string mapSolutions(GRAPHCOLORING problemFrom, SAT problemTo, string problemFromSolution){
        return "No mapping currently implemented.";
    }
    #endregion

}


   



