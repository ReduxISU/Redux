using API.Interfaces;
using API.Problems.NPComplete.NPC_STEINERTREE.Solvers;
using API.Problems.NPComplete.NPC_STEINERTREE.Verifiers;

namespace API.Problems.NPComplete.NPC_STEINERTREE;

class STEINERTREE : IProblem<SteinerTreeBruteForce, SteinerTreeVerifier>
{

    // --- Fields ---
    private string _problemName = "Steiner Tree";
    private string _formalDefinition = "Steiner = {<G,R,k> | G has a subtree of weight <= k containing the set of nodes in R}";
    private string _problemDefinition = "Steiner tree problem in graphs requires a tree of minimum weight that contains all terminals";
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string _defaultInstance = "(({1,2,3,4,5,6,7,8},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5},{6,7},{7,8},{6,8},{6,1}}),{5,2,8},6)";
    private string _instance = string.Empty;

    private string _wikiName = "";
    private int _K;
    private List<string> _nodes = new List<string>();
    private List<string> _terminals = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    private SteinerTreeBruteForce _defaultSolver = new SteinerTreeBruteForce();
    private SteinerTreeVerifier _defaultVerifier = new SteinerTreeVerifier();
    private SteinerGraph _steinerAsGraph;
    private string[] _contributors = { "Andrija Sevaljevic" };

    // --- Properties ---
    public string problemName
    {
        get
        {
            return _problemName;
        }
    }
    public string formalDefinition
    {
        get
        {
            return _formalDefinition;
        }
    }
    public string problemDefinition
    {
        get
        {
            return _problemDefinition;
        }
    }

    public string source
    {
        get
        {
            return _source;
        }
    }

    public string[] contributors
    {
        get
        {
            return _contributors;
        }
    }
    public string defaultInstance
    {
        get
        {
            return _defaultInstance;
        }
    }
    public string instance
    {
        get
        {
            return _instance;
        }
        set
        {
            _instance = value;
        }
    }
    public string wikiName
    {
        get
        {
            return _wikiName;
        }
    }
    public List<string> nodes
    {
        get
        {
            return _nodes;
        }
        set
        {
            _nodes = value;
        }
    }
    public List<string> terminals
    {
        get
        {
            return _terminals;
        }
        set
        {
            _terminals = value;
        }
    }
    public List<KeyValuePair<string, string>> edges
    {
        get
        {
            return _edges;
        }
        set
        {
            _edges = value;
        }
    }
    public SteinerTreeBruteForce defaultSolver
    {
        get
        {
            return _defaultSolver;
        }
    }
    public SteinerTreeVerifier defaultVerifier
    {
        get
        {
            return _defaultVerifier;
        }
    }
    public int K
    {
        get
        {
            return _K;
        }
        set
        {
            _K = value;
        }
    }

    public SteinerGraph steinerAsGraph
    {
        get
        {
            return _steinerAsGraph;
        }
        set
        {
            _steinerAsGraph = value;
        }
    }

    // --- Methods Including Constructors ---
    public STEINERTREE()
    {
        _instance = defaultInstance;
        _steinerAsGraph = new SteinerGraph(_instance,true);
        nodes = _steinerAsGraph.nodesStringList;
        terminals = getTerminals(_instance);
        edges = _steinerAsGraph.edgesKVP;
        K = _steinerAsGraph.K;

    }
    public STEINERTREE(string GInput)
    {
        _steinerAsGraph = new SteinerGraph(GInput, true);
        nodes = _steinerAsGraph.nodesStringList;
        edges = _steinerAsGraph.edgesKVP;
        K = _steinerAsGraph.K;
        _instance = _steinerAsGraph.ToString();
        terminals = getTerminals(GInput);

    }
    public List<string> getTerminals(string Ginput)
    {

        List<string> seperate = Ginput.Split("}}),{").ToList();
        List<string> allGNodes = seperate[1].Split("},").ToList();
        allGNodes = allGNodes[0].Split(",").ToList();

        return allGNodes;
    }


}