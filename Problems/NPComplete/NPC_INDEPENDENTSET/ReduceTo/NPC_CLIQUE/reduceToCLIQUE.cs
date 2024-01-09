using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_INDEPENDENTSET;

namespace API.Problems.NPComplete.NPC_INDEPENDENTSET.ReduceTo.NPC_CLIQUE;

class CliqueReduction : IReduction<INDEPENDENTSET, CLIQUE> {


    // --- Fields ---
    private string _reductionName = "Clique reduction";
    private string _reductionDefinition = @"This reduction converts an independent set problem into a clique problem, 
                                            by taking the complement of the graph, or inverting all the edges.";
    private string _source = "";
    private string[] _contributers = {"Russell Phillips"};

    private Dictionary<Object,Object> _gadgetMap = new Dictionary<Object,Object>();
    private INDEPENDENTSET _reductionFrom;
    private CLIQUE _reductionTo;

    private string _complexity = "";


    // --- Properties ---
    public string reductionName {
        get {
            return _reductionName;
        }
    }
    public string reductionDefinition {
        get {
            return _reductionDefinition;
        }
    }
    public string source {
        get {
            return _source;
        }
    }
       public string[] contributers{
        get{
            return _contributers;
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
    public CliqueReduction(INDEPENDENTSET from) {
        _reductionFrom = from;
        _reductionTo = reduce();

    }

    /// <summary>
    /// Reduces a CLIQUE instance to a VERTEXCOVER instance.
    /// </summary>
    /// <returns> A Vertexcover instance</returns>
    /// <remarks>
    /// authored by Janita Aamir. Contributed to by Alex Diviney.
    /// </remarks>
    public CLIQUE reduce() {
        INDEPENDENTSET INDPENDENTSETInstance = _reductionFrom;
        CLIQUE reducedCLIQUE = new CLIQUE();

        reducedCLIQUE.nodes = INDPENDENTSETInstance.nodes;

        List<KeyValuePair<string, string>> edges = new List<KeyValuePair<string, string>>();

        foreach(var i in INDPENDENTSETInstance.nodes){
            foreach(var j in INDPENDENTSETInstance.nodes){
                KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(i,j);
                KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(j,i);
                if(!(INDPENDENTSETInstance.edges.Contains(pairCheck1) || INDPENDENTSETInstance.edges.Contains(pairCheck2) || i.Equals(j) || edges.Contains(pairCheck1) || edges.Contains(pairCheck2))){
                    edges.Add(pairCheck1);
                }
            }
        }
        // --- Generate G string for new CLIQUE ---
        string nodesString = "";
        foreach (string nodes in INDPENDENTSETInstance.nodes) {
            nodesString += nodes + ",";
        }
        nodesString = nodesString.Trim(',');

        string edgesString = "";
        foreach (KeyValuePair<string,string> edge in edges) {
            edgesString += "{" + edge.Key + "," + edge.Value + "}" + ",";
        }
        edgesString = edgesString.Trim(',');

        string G = "(({" + nodesString + "},{" + edgesString + "})," + INDPENDENTSETInstance.K.ToString() + ")";

        reducedCLIQUE = new CLIQUE(G);
        reductionTo = reducedCLIQUE;
        return reducedCLIQUE;

    }

    public string mapSolutions(INDEPENDENTSET problemFrom, CLIQUE problemTo, string problemFromSolution){
        //Check if the solution is correct
        if(!problemFrom.defaultVerifier.verify(problemFrom,problemFromSolution)){
            return "Indpendent Set solution is incorect " + problemFromSolution;
        }

        //Parse problemFromSolution into a list of nodes
        List<string> solutionList = GraphParser.parseNodeListWithStringFunctions(problemFromSolution);

        //Map solution
        List<string> mappedSolutionList = new List<string>();
        foreach(string node in problemFrom.nodes){
            if(!solutionList.Contains(node)){
                mappedSolutionList.Add(node);
            }
        }
        string problemToSolution = "";
        foreach(string node in mappedSolutionList){
            problemToSolution += node + ',';
        }
        return '{' + problemToSolution.TrimEnd(',') + '}';

    }
}
