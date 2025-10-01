using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_ExactCover.Solvers;
using API.Problems.NPComplete.NPC_ExactCover.Verifiers;

namespace API.Problems.NPComplete.NPC_ExactCover;

class ExactCover : IProblem<ExactCoverBruteForce, ExactCoverVerifier>
{

    // --- Fields ---
    public string problemName { get; } = "Exact Cover";
    public string formalDefinition { get; } = "Exact Cover = {<S, X> | S is a collection of subsets of a set X where S* exists such that S* is a subcollection of S and an exact cover, of S. This means that each element of X is in exactly one subset of S*.} ";
    public string problemDefinition { get; } = "The exact cover problem is a decision problem to determine if an exact cover exists for some <S, X>";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string[] contributors { get; } = { "Caleb Eardley", "Alex Diviney" };


    public string defaultInstance { get; } = "{{1,2,3},{2,3},{4,1} : {1,2,3,4}}";
    public string instance { get; set; } = string.Empty;

    public string wikiName { get; } = "";
    public ExactCoverBruteForce defaultSolver { get; } = new ExactCoverBruteForce();
    public ExactCoverVerifier defaultVerifier { get; } = new ExactCoverVerifier();
    List<List<string>> _S = new List<List<string>>();
    List<string> _X = new List<string>();

    // --- Properties ---
    public List<List<string>> S
    {
        get
        {
            return _S;
        }
        set
        {
            _S = value;
        }
    }

    public List<string> X
    {
        get
        {
            return _X;
        }
        set
        {
            _X = value;
        }
    }
    // --- Methods Including Constructors ---

    private List<List<string>> GetS(string instance)
    {
        List<List<string>> S = new List<List<string>>();
        List<string> S_stringList = instance.Replace(" ", "").Split(":")[0].Split("},{").ToList();
        foreach (string stringSet in S_stringList)
        {
            List<string> subset = GraphParser.parseNodeListWithStringFunctions(stringSet);
            S.Add(subset);
        }
        return S;


    }
    private List<string> GetX(string instance)
    {
        // First remove all whitespace and split on ":"
        string cleaned = instance.Replace(" ", "").Replace("\n", "");
        string xPart = cleaned.Split(":")[1];

        // Remove braces and split elements
        xPart = xPart.Trim('{', '}');
        return xPart.Split(',').ToList();
    }
    public ExactCover()
    {
        instance = defaultInstance;
        _S = GetS(instance);
        _X = GetX(instance);
    }
    public ExactCover(string instance)
    {
        this.instance = instance;
        _S = GetS(instance);
        _X = GetX(instance);
    }


}