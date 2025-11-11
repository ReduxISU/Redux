using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_HITTINGSET.Solvers;
using API.Problems.NPComplete.NPC_HITTINGSET.Verifiers;
using SPADE;

namespace API.Problems.NPComplete.NPC_HITTINGSET;

class HITTINGSET : IProblem<HittingSetBruteForce, HittingSetVerifier, DummyVisualization> {


    #region Fields
    public string problemName {get;} = "Hitting Set";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Set_cover_problem#Hitting_set_formulation";
    public string formalDefinition {get;} = "Hitting set family of subsets {U_i} of a set {S_j} where there is a set W such that, for each i, |W union U_i| = 1.";
    public string problemDefinition {get;} = "Hitting set is the problem of finding a set where it shares exactly one element with each subset U_i. ";

    public string source {get;} = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors {get;} = { "Russell Phillips" };

    private static string _defaultInstance {get;} = "({1,2,3,4},{{1,3},{2,3,4},{1,4}})";
    public string defaultInstance {get;} = _defaultInstance;

    public string instance {get;set;} = string.Empty;

    public string wikiName {get;} = "";

    public HittingSetBruteForce defaultSolver {get;} = new HittingSetBruteForce();

    public HittingSetVerifier defaultVerifier { get; } = new HittingSetVerifier();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();

    public UtilCollection _universalSet;

    public UtilCollection _subsets;


    #endregion


    #region Properties

    public UtilCollection universalSet {
        get {
            return _universalSet;
        }
        set {
            _universalSet = value;
        }
    }

    public UtilCollection subSets {
        get {
            return _subsets;
        }
        set {
            _subsets = value;
        }
    }
    #endregion

    public HITTINGSET() : this(_defaultInstance)
    {
        
    }

    public HITTINGSET(string instanceStr)
    {
        instance = instanceStr;
        StringParser HittingSet = new("{(U,S) | U is set, S subset {a | a subset U}}");
        HittingSet.parse(instance);
        _universalSet = HittingSet["U"];
        _subsets = HittingSet["S"];
    }
}