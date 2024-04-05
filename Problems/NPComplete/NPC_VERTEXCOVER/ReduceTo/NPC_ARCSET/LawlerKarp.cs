using API.Interfaces;
using API.Problems.NPComplete.NPC_ARCSET;
using API.Problems.NPComplete.NPC_VERTEXCOVER;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Interfaces.Graphs;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.ReduceTo.NPC_ARCSET;

class LawlerKarp : IReduction<VERTEXCOVER, ARCSET> {

  

    // --- Fields ---
    private string _reductionName = "Lawler and Karp Arcset Reduction";
    private string _reductionDefinition = @"This Reduction is an implementation of Lawler and Karp's reduction as laid out in Karp's 21 NP_Complete Problems. 
                                            It takes an instance of an undirected graph (specifically an instance of VERTEXCOVER) and returns an instance of ARCSET (ie. a Directed Graph)
                                            Specifically, a reduction follows the following algorithm: 
                                            For an undirected graph H: Where H is made up of <V,E>
                                            Convert the undirected edges in E to pairs of directed edges. So an undirected edge {{A,B}} turns into the directed pair of edges {(A,B),(B,A)} 
                                            Then turn every node into a pair of nodes denoted by 0 and 1. So a node 'A' turns into the two nodes '<A,0>' and '<A,1>'
                                            Now looks at the pairs of edges in E and maps from 1 to 0. So an edge (A,B) turns into (<A,1>, <B,0>) and edge (B,A) becomes (<B,1>,<A,0>)
                                            Then add directed edges from every 0 node 'u' to 1 node 'u'. ie. creates edges from <A,0> to <A,1>, <B,0> to <B,1> … <Z,0> to <Z,1>
                                            Now the algorithm has created an ARCSET instance (in other words, a Digraph). ";
    private string _source = "http://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf"; //Alex NOTE: Change later to real citation.
    private string[] _contributors = { "Daniel Igbokwe","Caleb Eardley"};
    private VERTEXCOVER _reductionFrom;
    private ARCSET _reductionTo;
    private Dictionary<Object,Object> _gadgetMap = new Dictionary<Object,Object>();


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
    public VERTEXCOVER reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public ARCSET reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }

    // --- Methods Including Constructors ---

    public LawlerKarp(){

        _reductionFrom = new VERTEXCOVER();
        _reductionTo = new ARCSET();
    }
    public LawlerKarp(VERTEXCOVER from) {
         _reductionFrom = from;
        _reductionTo = reduce();
        var options = new JsonSerializerOptions { WriteIndented = true };
        String jsonString = JsonSerializer.Serialize(reduce(),options);
        
    }
    /// <summary>
    ///  Uses the VertexCover object's reduction utility to convert to a Arcset Graph and returns that equivalent object.
    /// </summary>
    /// <returns>
    /// An Arcset Object
    /// </returns>
    public ARCSET reduce() {
        VertexCoverGraph ug = new VertexCoverGraph(_reductionFrom.instance,true);
        string dgString = ug.reduction();
        //ArcsetGraph dg = new ArcsetGraph(dgString,true);
        ARCSET arcset = new ARCSET(dgString);
        
        return arcset;
    }

    public string mapSolutions(VERTEXCOVER problemFrom, ARCSET problemTo, string problemFromSolution){
        //Check if the colution is correct
        if(!problemFrom.defaultVerifier.Verify(problemFrom,problemFromSolution)){
            return "Solution is inccorect";
        }

        //NOTE :: should we verify if the reduction is correct, if so we might as well just take the problemFrom and create the problemTo

        //Parse problemFromSolution into a list of nodes
        List<string> solutionList = GraphParser.parseNodeListWithStringFunctions(problemFromSolution);

        //Map solution 
        List<string> mappedSolutionList = new List<string>();
        foreach(string node in solutionList){
            mappedSolutionList.Add(string.Format("({0}0,{0}1)",node));
        }
        string problemToSolution = "";
        foreach(string edge in mappedSolutionList){
            problemToSolution += edge + ',';
        }
        return '{' + problemToSolution.TrimEnd(',') + '}';
    }
}
// return an instance of what you are reducing to