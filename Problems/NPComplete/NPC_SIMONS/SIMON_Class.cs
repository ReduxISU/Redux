using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_SIMON.Solvers;
using API.Problems.NPComplete.NPC_SIMON.Verifiers;
using SPADE;

namespace API.Problems.NPComplete.NPC_SIMON;

class SIMON : IProblem<SIMONSOLVER, SIMONVERIFIER, DummyVisualization> {

    // --- Fields ---
    public string problemName {get;} = "Simon's Algorithm"; // Name as it appears in the dropdown selection panel
    public string problemLink {get;} = "https://en.wikipedia.org/wiki/Simon%27s_problem"; // Link to the Wikipedia page for the problem
    public string formalDefinition {get;} =  "Simon = {(<w_1, w_2, ... , w_(2^n - 1), w_(2^n)> | w_i is bit string of length m, with n being the input dimension and m being the output dimension of the function}"; // Mathematical description of the problem (todo later)
    public string problemDefinition { get; } = "Simon's problem is defined by a black-box function f: {0,1}^n -> {0,1}^m. For this function the following is promised: f(x) = f(y) if and only if x = y or x = y ⊕ s for some secret string s ∈ {0,1}^n. The goal is to find the string s"; // plaintext description of the problem
    public string source { get; } = "Simon, Daniel R. On the power of quantum computation. SIAM journal on computing, 1997, 26. Jg., Nr. 5, S. 1474-1483."; // Academic paper proper citation
    public string sourceLink { get; } = "https://epubs.siam.org/doi/abs/10.1137/S0097539796298637?casa_token=q1_RWPmvpQ0AAAAA:vmai1NwqSJEUGwydbsrdvH1tsKxcE_MoWfiTwQda9yJKhC0prizshyidP4VcDZK8n5CuqoeaqlQ"; // Link to the academic paper
    private static readonly string _defaultInstance = "(00, 01, 10, 11, 01, 00, 11, 10)"; 
    public string instance {get;set;} = string.Empty;
    public string wikiName {get;} = ""; // Wiki name or link? - not used yet
    public SIMONSolver defaultSolver {get;} = new SIMONSolver();
    public SIMONVerifier defaultVerifier { get; } = new SIMONVerifier();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();
    public string[] contributors {get;} = { "Eric Hill", "Paul Gilbreath", "Max Gruenwoldt", "Alex Svancara" };

    // TODO: implement properties if {NAME} is a graphing problem
    // private List<string> _nodes = new List<string>();
    // private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    // private int _K ;
    // private ProblemGraph _{NAME_CAMEL_CASE}AsGraph;

    // --- Properties ---

    // TODO: implement properties if {NAME} is a graphing problem
    // public List<string> nodes {
    //     get {
    //         return _nodes;
    //     }
    //     set {
    //         _nodes = value;
    //     }
    // }
    // public List<KeyValuePair<string, string>> edges {
    //     get {
    //         return _edges;
    //     }
    //     set {
    //         _edges = value;
    //     }
    // }
    // public int K {
    //     get {
    //         return _K;
    //     }
    //     set {
    //         _K = value;
    //     }
    // }
    // public ProblemGraph {NAME_CAMEL_CASE}AsGraph {
    //     get{
    //         return _{NAME_CAMEL_CASE}AsGraph;
    //     }
    //     set{
    //         _{NAME_CAMEL_CASE}AsGraph = value;
    //     }
    // }

    // --- Methods and Constructors ---
    public SIMON() : this(_defaultInstance) {

    }

    public SIMON(string input) {
        instance = input;



        // TODO: implement parsing of string instance of {NAME}. SPADE is a class meant to help with this step, see https://github.com/Jetison333/SPADE/blob/main/Documentation/index.md for more information.
        //


        StringParser parser = new("{i | i is list}");

        parser.parse(instance);

        // items = parser["i"];

        //int n = int.Parse(parser["n"].ToString());
        //int m = int.Parse(parser["m"].ToString());
        SPADE.UtilCollection bitslist = parser["i"];
        //Console.WriteLine(bitslist);

        // This parsing is left over from the template, but we're pretty sure it works for extracting the values from the input
        // Good luck solver team :) - problems team


    }

}
