using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_ExactCover.Solvers;
using API.Problems.NPComplete.NPC_ExactCover.Verifiers;

namespace API.Problems.NPComplete.NPC_ExactCover;

class ExactCover : IProblem<ExactCoverBruteForce,ExactCoverVerifier> {

    // --- Fields ---
    private string _problemName = "Exact Cover";
    private string _formalDefinition = "Exact Cover = {<S, X> | S is a collection of subsets of a set X where S* exists such that S* is a subcollection of S and an exact cover, of S. This means that each element of X is in exactly one subset of S*.} ";
    private string _problemDefinition = "The exact cover problem is a decision problem to determine if an exact cover exists for some <S, X>";
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string[] _contributors = { "Caleb Eardley", "Alex Diviney" };

    
    private string _defaultInstance = "{{1,2,3},{2,3},{4,1} : {1,2,3,4}}";
    private string _instance = string.Empty;

    private string _wikiName = "";
    private ExactCoverBruteForce _defaultSolver = new ExactCoverBruteForce();
    private ExactCoverVerifier _defaultVerifier = new ExactCoverVerifier();
    List<List<string>> _S = new List<List<string>>();
    List<string> _X = new List<string>();

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
    public string wikiName {
        get {
            return _wikiName;
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
    public string defaultInstance {
        get {
            return _defaultInstance;
        }
    }
    public ExactCoverBruteForce defaultSolver {
        get {
            return _defaultSolver;
        }
    }
    public ExactCoverVerifier defaultVerifier {
        get {
            return _defaultVerifier;
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
    public List<List<string>> S {
        get {
            return _S;
        }
        set{
            _S = value;
        }
    }

    public List<string> X {
        get{
            return _X;
        }
        set{
            _X = value;
        }
    }
    // --- Methods Including Constructors ---

    private List<List<string>> GetS(string instance){
        List<List<string>> S = new List<List<string>>();
        List<string> S_stringList = instance.Replace(" ","").Split(":")[0].Split("},{").ToList();
        foreach(string stringSet in S_stringList){
            List<string> subset = GraphParser.parseNodeListWithStringFunctions(stringSet);
            S.Add(subset);
        }
        return S;


    }
    private List<string> GetX(string instance){
        List<string> X = instance.Split(":")[1].Replace("{","").Replace("}","").Replace(" ","").Split(",").ToList();
        return X;
    }
    public ExactCover() {
        _instance = _defaultInstance;
        _S = GetS(_instance);
        _X = GetX(_instance);
    }
    public ExactCover(string instance) {
        _instance = instance;
        _S = GetS(_instance);
        _X = GetX(_instance);
    }


}