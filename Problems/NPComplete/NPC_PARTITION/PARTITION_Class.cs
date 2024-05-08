using API.Interfaces;
using API.Problems.NPComplete.NPC_PARTITION.Solvers;
using API.Problems.NPComplete.NPC_PARTITION.Verifiers;

namespace API.Problems.NPComplete.NPC_PARTITION;

class PARTITION : IProblem<PartitionBruteForce,PartitionVerifier> {

    // --- Fields ---
    private string _problemName = "Partition";
    private string _formalDefinition = "Partition = <S, I> | S is a set of positive integers and there exists a subset of S, I where the sum of I's elements equals the sum of elements not in set I";
    private string _problemDefinition = "The partition problem is the task of deciding whether a given multiset S of positive integers can be partitioned into two subsets S1 and S2 such that the sum of the numbers in S1 equals the sum of the numbers in S2";
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string[] _contributors = {"Andrija Sevaljevic"};



    private string _defaultInstance = "{1,7,12,15,33,12,11,5,6,9,21,18}";
    private string _instance = string.Empty;
    private List<string> _S = new List<string>();
    

    private string _wikiName = "";
    private PartitionBruteForce _defaultSolver = new PartitionBruteForce();
    private PartitionVerifier _defaultVerifier = new PartitionVerifier();

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
    
    public PartitionBruteForce defaultSolver {
        get {
            return _defaultSolver;
        }
    }
    public PartitionVerifier defaultVerifier {
        get {
            return _defaultVerifier;
        }
    }
    // --- Methods Including Constructors ---
    public PARTITION() {
        _instance = defaultInstance;
        S = getIntegers(_instance);
    }
    public PARTITION(string instance) {
        _instance = instance;
        S = getIntegers(_instance);
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
    


}