using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_SUBSETSUM.Solvers;
using API.Problems.NPComplete.NPC_SUBSETSUM.Verifiers;
using SPADE;

namespace API.Problems.NPComplete.NPC_SUBSETSUM;

class SUBSETSUM : IProblem<SubsetSumBruteForce,SubsetSumVerifier, DummyVisualization> {

    // --- Fields ---
    public string problemName {get;} = "Subset Sum";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Subset_sum_problem";
    public string formalDefinition {get;} = "Subset Sum = <S, T> | S is a set of positive integers and there exists a subset of S, K where the sum of K's elements equals T";
    public string problemDefinition {get;} = "The problem is to determine whether there exists a sum of elements that totals to the number T.";
    public string source {get;} = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors {get;} = { "Garret Stouffer, Caleb Eardley"};
    public static string _defaultInstance { get; } = "{{1,7,12,15} : 28}";
    public string defaultInstance {get;} = _defaultInstance;
    public string instance {get;set;} = string.Empty;
    private List<string> _S = new List<string>();
    private int _T;

    public string wikiName {get;} = "";
    public SubsetSumBruteForce defaultSolver {get;} = new SubsetSumBruteForce();
    public SubsetSumVerifier defaultVerifier { get; } = new SubsetSumVerifier();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();

    // --- Properties ---
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

    // --- Methods Including Constructors ---
    public SUBSETSUM() : this(_defaultInstance)
    {
        
    }

    public SUBSETSUM(string instance) {
        this.instance = instance;
        S = getIntegers(instance);
        T = getT(instance);
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