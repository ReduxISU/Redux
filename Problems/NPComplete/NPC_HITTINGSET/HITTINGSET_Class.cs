using API.Interfaces;
using API.Problems.NPComplete.NPC_HITTINGSET.Solvers;
using API.Problems.NPComplete.NPC_HITTINGSET.Verifiers;
using API.Tools.UtilCollection;

namespace API.Problems.NPComplete.NPC_HITTINGSET;

class HITTINGSET : IProblem<HittingSetBruteForce, HittingSetVerifier>
{


    #region Fields
    private readonly string _problemName = "Hitting Set";
    private readonly string _formalDefinition = "Hitting set family of subsets {U_i} of a set {S_j} where there is a set W such that, for each i, |W union U_i| = 1.";
    private readonly string _problemDefinition = "Hitting set is the problem of finding a set where it shares exactly one element with each subset U_i. ";

    private readonly string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string[] _contributers = { "Russell Phillips" };

    private static string _defaultInstance = "({1,2,3,4},{{1,3},{2,3,4},{1,4}})";

    private string _instance  =  string.Empty;

    private string _wikiName = "";

    private HittingSetBruteForce _defaultSolver = new HittingSetBruteForce();

    private HittingSetVerifier _defaultVerifier = new HittingSetVerifier();

    public UtilCollection _universalSet;

    public UtilCollection _subsets;


    #endregion


    #region Properties

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

    public String instance  {
        get{
            return _instance ;
        }

        set {
            _instance  = value;
        }
    }

    public string wikiName {
        get {
            return _wikiName;
        }
    }

    public HittingSetBruteForce defaultSolver {
        get {
            return _defaultSolver;
        }
    }
    public HittingSetVerifier defaultVerifier {
        get {
            return _defaultVerifier;
        }
    }

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
        _instance = instanceStr;
        UtilCollection collection = new UtilCollection(instanceStr);
        collection.assertPair();
        _universalSet = collection[0];
        _subsets = collection[1];
    }

}