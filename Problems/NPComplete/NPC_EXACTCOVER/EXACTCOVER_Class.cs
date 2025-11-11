using API.DummyClasses;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_EXACTCOVER.Solvers;
using API.Problems.NPComplete.NPC_EXACTCOVER.Verifiers;
using SPADE;

namespace API.Problems.NPComplete.NPC_EXACTCOVER;

class EXACTCOVER : IProblem<ExactCoverBruteForce,ExactCoverVerifier,DummyVisualization> {

    // --- Fields ---
    public string problemName {get;} = "Exact Cover";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Exact_cover";
    public string formalDefinition {get;} = "Exact Cover = {<S, X> | S is a collection of subsets of a set X where S* exists such that S* is a subcollection of S and an exact cover, of S. This means that each element of X is in exactly one subset of S*.} ";
    public string problemDefinition {get;} = "The exact cover problem is a decision problem to determine if an exact cover exists for some <S, X>";
    public string source {get;} = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors {get;} = { "Caleb Eardley", "Alex Diviney" };

    
    private static string _defaultInstance = "({1,2,3,4},{{1,2,3},{2,3},{4,1}})";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance {get;set;} = string.Empty;

    public string wikiName {get;} = "";
    public ExactCoverBruteForce defaultSolver {get;} = new ExactCoverBruteForce();
    public ExactCoverVerifier defaultVerifier { get; } = new ExactCoverVerifier();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();
    List<List<string>> _S = new List<List<string>>();
    List<string> _X = new List<string>();

    // --- Properties ---
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

    public EXACTCOVER() : this(_defaultInstance) {
    }
    public EXACTCOVER(string input) {
        instance = input;

        StringParser exactcover = new("{(U,S) | U is set, S subset {a | a subset U}}");
        exactcover.parse(input);
        X = exactcover["U"].ToList().Select(node => node.ToString()).ToList();
        S = exactcover["S"].ToList().Select(subset => subset.ToList().Select(item => item.ToString()).ToList()).ToList();
    }


}