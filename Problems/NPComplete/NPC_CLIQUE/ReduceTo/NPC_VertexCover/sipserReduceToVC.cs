using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_VERTEXCOVER;

namespace API.Problems.NPComplete.NPC_CLIQUE.ReduceTo.NPC_VertexCover;

class sipserReduction : IReduction<CLIQUE, VERTEXCOVER> {


    // --- Fields ---
    private string _reductionName = "Sipser's Vertex Cover Reduction";
    private string _reductionDefinition = @"This Sipsers reduction converts the Clique problem into a Vertex Cover problem.
                                            This is done by first taking all possible edges in the original clique graph, and removing
                                            the edges that are actually in the clique graph from that set.";
    private string _source = "Sipser, Michael. Introduction to the Theory of Computation.ACM Sigact News 27.1 (1996): 27-29.";
    private string[] _contributors = {"Janita Aamir","Alex Diviney","Caleb Eardley"};

    private Dictionary<Object,Object> _gadgetMap = new Dictionary<Object,Object>();
    private CLIQUE _reductionFrom;
    private VERTEXCOVER _reductionTo;

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
       public string[] contributors{
        get{
            return _contributors;
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
    public sipserReduction(CLIQUE from) {
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
    public VERTEXCOVER reduce() {
        CLIQUE CLIQUEInstance = _reductionFrom;
        VERTEXCOVER reducedVERTEXCOVER = new VERTEXCOVER();

        // Assign clique nodes to vertexcover nodes.
        reducedVERTEXCOVER.nodes = CLIQUEInstance.nodes;

        List<KeyValuePair<string, string>> edges = new List<KeyValuePair<string, string>>();

        //this nested loop creates every possible combination of edges that aren't self edges between the nodes in the set and adds them to a list. 
        // ie. nodes 1,2,3 become edges {1,2},{2,1},{1,3},{3,1},{2,3},{3,2}
        for (int i = 0; i < reducedVERTEXCOVER.nodes.Count; i++){
            for (int j = 0; j < reducedVERTEXCOVER.nodes.Count; j++){
                if (reducedVERTEXCOVER.nodes[i] != reducedVERTEXCOVER.nodes[j]){
                    KeyValuePair<string,string> fullEdge = new KeyValuePair<string,string>(reducedVERTEXCOVER.nodes[i], reducedVERTEXCOVER.nodes[j]);
                    edges.Add(fullEdge);
                }
            }
        }
        
        //for every edge in clique, removes the edge from the total list of edges.
        for (int i = 0; i < CLIQUEInstance.edges.Count; i++){
            edges.Remove(new KeyValuePair<string,string>(CLIQUEInstance.edges[i].Key, CLIQUEInstance.edges[i].Value));
            edges.Remove(new KeyValuePair<string,string>(CLIQUEInstance.edges[i].Value, CLIQUEInstance.edges[i].Key));
        }

        //For every edge in the remaining set, removes any edge that would be redundant. So if we have {1,3} and {3,1} then we only leave {1,3}
        for (int i = 0; i < edges.Count; i++){
            for (int j = 0; j < edges.Count; j++){
                if (edges[i].Key == edges[j].Value && edges[i].Value == edges[j].Key){
                    edges.Remove(new KeyValuePair<string,string>(edges[j].Key, edges[j].Value));
                }
            }
        }

        // --- Generate G string for new CLIQUE ---
        string nodesString = "";
        foreach (string nodes in CLIQUEInstance.nodes) {
            nodesString += nodes + ",";
        }
        nodesString = nodesString.Trim(',');
        string edgesString = "";
        foreach (KeyValuePair<string,string> edge in edges) {
            edgesString += "{" + edge.Key + "," + edge.Value + "}" + ",";
        }
        edgesString = edgesString.Trim(',');
        int vertexKInt = (CLIQUEInstance.nodes.Count - CLIQUEInstance.K); //N - K where N is number of nodes of k-clique and K is the k of k-clique
        string G = "(({" + nodesString + "},{" + edgesString + "})," + vertexKInt.ToString() + ")";

        reducedVERTEXCOVER = new VERTEXCOVER(G);
        reductionTo = reducedVERTEXCOVER;
        return reducedVERTEXCOVER;

    }

    public string mapSolutions(CLIQUE problemFrom, VERTEXCOVER problemTo, string problemFromSolution){
        //Check if the colution is correct
        if(!problemFrom.defaultVerifier.verify(problemFrom,problemFromSolution)){
            return "Clique solution is incorect " + problemFromSolution;
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
