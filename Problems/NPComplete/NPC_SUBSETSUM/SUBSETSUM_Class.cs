using API.Interfaces;
using API.Problems.NPComplete.NPC_SUBSETSUM.Solvers;
using API.Problems.NPComplete.NPC_SUBSETSUM.Verifiers;

namespace API.Problems.NPComplete.NPC_SUBSETSUM;

class SUBSETSUM : IProblem<SubsetSumBruteForce,SubsetSumVerifier> {

    // --- Fields ---
    private string _problemName = "Subset Sum";
    private string _formalDefinition = "Subset Sum = <S, T> | S is a set of positive integers and there exists a subset of S, K where the sum of K's elements equals T";
    private string _problemDefinition = "The problem is to determine whether there exists a sum of elements that totals to the number T.";
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string[] _contributors = { "Garret Stouffer, Caleb Eardley"};

    //{{10,20,30},{(10,60),(20,100),(30,120)},50}
    //{{}, {}, 28}

    private string _defaultInstance = "{{1,7,12,15} : 28}";
    private string _instance = string.Empty;
    private List<string> _S = new List<string>();
    private int _T;

    private string _wikiName = "";
    private SubsetSumBruteForce _defaultSolver = new SubsetSumBruteForce();
    private SubsetSumVerifier _defaultVerifier = new SubsetSumVerifier();

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

    public string[] contributors{
        get{
            return _contributors;
        }
    }

    public string source {
        get {
            return _source;
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
    public List<string> S {
        get {
            return _S;
        }
        set {
            _S = value;
        }
    }
    public int T {
        get {
            return _T;
        }
        set {
            _T = value;
        }
    }
    public SubsetSumBruteForce defaultSolver {
        get {
            return _defaultSolver;
        }
    }
    public SubsetSumVerifier defaultVerifier {
        get {
            return _defaultVerifier;
        }
    }
    // --- Methods Including Constructors ---
    public SUBSETSUM() {
        _instance = defaultInstance;
        S = getIntegers(_instance);
        T = getT(_instance);
    }
    public SUBSETSUM(string instance) {
        _instance = instance;
        S = getIntegers(_instance);
        T = getT(_instance);
    }
    public List<string> getIntegers(string instance) {

        List<string> allIntegers = new List<string>();
        string strippedInput = instance.Replace("{", "").Replace("}", "").Replace(" ", "");
        
        // [0] is integers,  [1] is T.
        string[] SSsections = strippedInput.Split(':');
        string[] SSintegers = SSsections[0].Split(',');
        
        foreach(string integer in SSintegers) {
            allIntegers.Add(integer);
        }

        return allIntegers;
    }
    

    public int getT(string instance) {
        string strippedInput = instance.Replace("{", "").Replace("}", "").Replace(" ", "");
        
        // [0] is integers,  [1] is T.
        string[] SSsections = strippedInput.Split(':');
        
        return Int32.Parse(SSsections[1]);
    }


}