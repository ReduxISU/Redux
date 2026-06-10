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
    public string[] contributors {get;} = { "Garret Stouffer", "Caleb Eardley"};
    public const string InstanceGrammar = "{(S,T) | S is set, T is int}";
    public static string _defaultInstance { get; } = "({1,7,12,15},28)";
    public string defaultInstance {get;} = _defaultInstance;
    public string instance {get;set;} = string.Empty;
    public string instanceFormat {get;} = $"Format: {InstanceGrammar} Example: {_defaultInstance}";
    public string certificateFormat {get;} =
        $"Format: {SubsetSumVerifier.CertificateGrammar} Example: {SubsetSumVerifier.CertificateExample}";
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

        StringParser parser = new(InstanceGrammar);
        List<string> parsedS;
        string tStr;
        try {
            parser.parse(instance);
            // Materialize inside the try: SPADE may parse without error and only
            // fail when the result is enumerated or a missing key is accessed.
            parsedS = parser["S"].ToList().Select(x => x.ToString()).ToList();
            tStr = parser["T"].ToString();
        } catch (Exception ex) {
            throw new ProblemParseException(problemName, instance, ex.Message);
        }

        foreach (string element in parsedS) {
            if (!int.TryParse(element, out _))
                throw new ProblemParseException(problemName, instance,
                    $"'{element}' is not a valid integer in S");
        }
        S = parsedS;

        if (!int.TryParse(tStr, out int t))
            throw new ProblemParseException(problemName, instance,
                $"'{tStr}' is not a valid integer for T");
        T = t;
    }
}
