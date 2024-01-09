/*
* Arcset_Class.cs
* @author Alex Diviney
*/



using API.Interfaces;
using API.Problems.NPComplete.NPC_ARCSET.Solvers;
using API.Problems.NPComplete.NPC_ARCSET.Verifiers;
namespace API.Problems.NPComplete.NPC_ARCSET;

class ARCSET : IProblem<ArcSetBruteForce,AlexArcsetVerifier>{

    // --- Fields ---
    private string _problemName = "Feedback Arc Set";
    private string _formalDefinition = "ARCSET = {<G,k> | G is a directed graph that can be rendered acyclic by removal of at most k edges}";
    private string _problemDefinition = "ARCSET, or the Feedback Arc Set satisfiability problem, is an NP-complete problem that can be described like the following. Given a directed graph, does removing a given set of edges render the graph acyclical? That is, does removing the edges break every cycle in the graph?";

    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";

    //ALEX NOTE: The standard mathematical form for a DIGRAPH is A = {x,y,z} r = {(x,y),(y,z),(z,x)} where A is a set of nodes and r is a set of pairs of edges. (r stands for relation)
    //           Since Arcset also has a k value needed, we put the digraph and k in as a ordered pair. (({x,y,z},{(x,y),(y,z),(z,x)}),1) where the first item of the pair is the graph,
    //           the second is the last number that is related to the solution value for the NP-Complete problem, in this case: 
    //           the question is "how many edges would you have to remove to break this graph's cycle?" and the answer is 1. 
    // G = {A,r} 
    //Mathmatical notation of the following default instance: "A = {1,2,3,4} r = {(4,1),(1,2),(4,3),(3,2),(2,4)} k = 1"; 
    private string _defaultInstance = "(({1,2,3,4},{(1,2),(2,4),(3,2),(4,1),(4,3)}),1)"; //final formal version. This is standard mathmatical digraph notation with a K element appended. 
    private string _instance = string.Empty;

    private string _wikiName ="";
    private ArcsetGraph _arcsetAsGraph;
    private ArcSetBruteForce _defaultSolver = new ArcSetBruteForce();
    private AlexArcsetVerifier _defaultVerifier = new  AlexArcsetVerifier(); //Verifier implements a Depth First Search. 
    
    private string[] _contributers = { "Alex Diviney" };


    // --- Properties ---
    public string problemName {
        get {
            return _problemName;
        }
    }
    public string formalDefinition {
        get {
            return _formalDefinition;
        }
    }
    public string problemDefinition {
        get {
            return _problemDefinition;
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
    public string defaultInstance {
        get {
            return _defaultInstance;
        }
    }
    public string wikiName {
        get {
            return _wikiName;
        }
    }
    public string instance {
        get {
            return _instance;
        }
        set {
            _instance = value;
        }
    }
    public ArcSetBruteForce defaultSolver {
        get {
            return _defaultSolver;
        }
    }
    public AlexArcsetVerifier defaultVerifier {
        get {
            return _defaultVerifier;
        }
    }
    public ArcsetGraph directedGraph{
        get{
            return _arcsetAsGraph;
        }
    }

    // --- Methods Including Constructors ---
    public ARCSET() {
        //Should be a graph object.
        string arcDefaultString = _defaultInstance;
        _arcsetAsGraph = new ArcsetGraph(_defaultInstance,true); //first value is the string input, second is a dummy boolean to utilize overloading.
        _instance = _arcsetAsGraph.ToString(); 
        _defaultVerifier = new AlexArcsetVerifier();
        
    }
    public ARCSET(string arcInput) {
        _arcsetAsGraph = new ArcsetGraph(arcInput,true); //first value is the string input, second is a dummy boolean to utilize overloading.
        _instance = _arcsetAsGraph.ToString();
        _defaultVerifier = new AlexArcsetVerifier(); 
    }
}