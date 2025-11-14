using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_DEUTSCH.Solvers;
using API.Problems.NPComplete.NPC_DEUTSCH.Verifiers;
using SPADE;

namespace API.Problems.NPComplete.NPC_DEUTSCH;

class DEUTSCH : IProblem<DeutschClassicalSolver, DeutschVerifier, DummyVisualization> {

    // --- Fields ---
    public string problemName {get;} = "Deutsch"; // Name as it appears in the dropdown selection panel
    public string formalDefinition {get;} =  "Deutsch = {<i, w> | i is int, w is int}"; // Mathematical description of the problem (todo later)
    public string problemDefinition { get; } = "Deutsch's algorithm solves the parity problem for the special case that n=1. It is represented by an ordered list......"; // plaintext description of the problem
    public string source { get; } = "todo"; // Academic paper proper citation
    public string sourceLink { get; } = "https://royalsocietypublishing.org/doi/10.1098/rspa.1985.0070"; // Link to the academic paper
    private static readonly string _defaultInstance = "(0,1)";
    public string defaultInstance {get;} = _defaultInstance;
    public string instance {get;set;} = string.Empty;
    public string wikiName {get;} = ""; // Wiki name or link? - not used yet
    public DeutschClassicalSolver defaultSolver {get;} = new DeutschClassicalSolver();
    public DeutschVerifier defaultVerifier { get; } = new DeutschVerifier();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();
    public string[] contributors {get;} = { "Eric Hill", "Paul Gilbreath", "Max Gruenwoldt", "Alex Svancara", "Jason L. Wright" };

    // --- Methods and Constructors ---
    public DEUTSCH() : this(_defaultInstance) {

    }

    static bool f0(bool x) {
        return false;
    }
    static bool f1(bool x) {
        return x;
    }
    static bool f2(bool x) {
        return !x;
    }

    static bool f3(bool x)
    {
        return true;
    }

    private Func<bool, bool> _f = f0;

    public Func<bool, bool> FUNC {
        get {
            return _f;
        }
    }

    public DEUTSCH(string input)
    {
        instance = input;

        StringParser parser = new("{(i, w) | i is int, w is int}");

        parser.parse(instance);

        // items = parser["i"];
        int I = int.Parse(parser["i"].ToString());
        int W = int.Parse(parser["w"].ToString());

        // determine which function to use

        _f = f0;
        if (I == 0 && W == 0)
        {
            _f = f0;
        }
        else if (I == 0 && W != 0)
        {
            _f = f1;
        }
        else if (I != 0 && W == 0)
        {
            _f = f2;
        }
        else if (I != 0 && W != 0)
        {
            _f = f3;
        }
    }
}
