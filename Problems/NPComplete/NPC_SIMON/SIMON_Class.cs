using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_SIMON.Solvers;
using API.Problems.NPComplete.NPC_SIMON.Verifiers;
using SPADE;

namespace API.Problems.NPComplete.NPC_SIMON;

class SIMON : IProblem<SimonSolver, SimonVerifier, DummyVisualization>
{

    // --- Fields ---
    public string problemName {get;} = "Simon's Algorithm"; // Name as it appears in the dropdown selection panel
    public string problemLink {get;} = "https://en.wikipedia.org/wiki/Simon%27s_problem"; // Link to the Wikipedia page for the problem
    public string formalDefinition {get;} =  "Simon = {(<w_1, w_2, ... , w_(2^n - 1), w_(2^n)> | w_i is bit string of length m converted to int, with n being the input dimension and m being the output dimension of the function}"; // Mathematical description of the problem (todo later)
    public string problemDefinition { get; } = "Simon's problem is defined by a black-box function f: {0,1}^n -> {0,1}^m. For this function the following is promised: f(x) = f(y) if and only if x = y or x = y ⊕ s for some secret string s ∈ {0,1}^n. The goal is to find the string s"; // plaintext description of the problem
    public string source { get; } = "Simon, Daniel R. On the power of quantum computation. SIAM journal on computing, 1997, 26. Jg., Nr. 5, S. 1474-1483."; // Academic paper proper citation
    public string sourceLink { get; } = "https://epubs.siam.org/doi/abs/10.1137/S0097539796298637?casa_token=q1_RWPmvpQ0AAAAA:vmai1NwqSJEUGwydbsrdvH1tsKxcE_MoWfiTwQda9yJKhC0prizshyidP4VcDZK8n5CuqoeaqlQ"; // Link to the academic paper
    private static readonly string _defaultInstance = "(5, 6, 5, 6, 3, 2, 3, 2)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance {get;set;} = string.Empty;
    public string wikiName {get;} = ""; // Wiki name or link? - not used yet
    public SimonSolver defaultSolver { get; } = new SimonSolver();
    public SimonVerifier defaultVerifier { get; } = new SimonVerifier();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();
    public string[] contributors {get;} = { "Eric Hill", "Max Gruenwoldt"};

    private int[] _funcValues = new int[2] { 0, 1 };

    public int[] funcValues
    {
        get
        {
            return _funcValues;
        }
        set
        {
            _funcValues = value;
        }
    }

    public int Func(int x)
    {
        if (x >= 0 && x < _funcValues.Length)
            return _funcValues[x];
        throw new IndexOutOfRangeException($"funcVaues[{x}]");
    }

    // --- Methods and Constructors ---
    public SIMON() : this(_defaultInstance) {

    }

    public SIMON(string input) {
        instance = input;

        StringParser parser = new("{i | i is list}");

        parser.parse(instance);

        SPADE.UtilCollection bitslist = parser["i"];
        var ilist = new List<int>();
        foreach (var i in Enumerable.Range(0, bitslist.Count()))
        {
            ilist.Add(bitslist[i].parseInt());
        }
        _funcValues = ilist.ToArray();
    }

    static public int PowerOfTwo(int n)
    {
        if (n > 0 && (n & (n - 1)) == 0)
            return (int)Math.Log2(n);
        throw new ArithmeticException("not a power of two");
    }
}
