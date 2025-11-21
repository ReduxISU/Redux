using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_DEUTSCH.Solvers;
using API.Problems.NPComplete.NPC_DEUTSCH.Verifiers;
using SPADE;

namespace API.Problems.NPComplete.NPC_DEUTSCH;

class DEUTSCH : IProblem<DeutschSolver, DeutschVerifier, DummyVisualization> {

    // --- Fields ---
    public string problemName {get;} = "Deutsch"; // Name as it appears in the dropdown selection panel
    public string problemLink {get;} = "https://en.wikipedia.org/wiki/Deutsch%E2%80%93Jozsa_algorithm#Deutsch's_algorithm"; // Link to the Wikipedia page for the problem
    public string formalDefinition {get;} =  "Deutsch = {<i, w> | i is int, w is int}"; // Mathematical description of the problem (todo later)
    public string problemDefinition { get; } = "Deutsch's algorithm determines whether a given function f: {0,1} -> {0,1} is constant or balanced. The problem has four possible input functions and is represented to the ordered list of outputs, i.e. (f(0), f(1))."; // plaintext description of the problem
    public string source { get; } = "Deutsch, David. 1985. Quantum theory, the Church-Turing principle and the universal quantum computer. Proc. R. Soc. Lond. A40097-117"; // Academic paper proper citation
    public string sourceLink { get; } = "https://royalsocietypublishing.org/doi/10.1098/rspa.1985.0070"; // Link to the academic paper
    private static readonly string _defaultInstance = "(0,1)";
    public string defaultInstance {get;} = _defaultInstance;
    public string instance {get;set;} = string.Empty;
    public string wikiName {get;} = ""; // Wiki name or link? - not used yet
    public DeutschSolver defaultSolver {get;} = new DeutschSolver();
    public DeutschVerifier defaultVerifier { get; } = new DeutschVerifier();
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
    public DEUTSCH() : this(_defaultInstance) {

    }

    public DEUTSCH(string input) {
        instance = input;



        // TODO: implement parsing of string instance of {NAME}. SPADE is a class meant to help with this step, see https://github.com/Jetison333/SPADE/blob/main/Documentation/index.md for more information.
        //


        StringParser parser = new("{(i, w) | i is int, w is int}");

        parser.parse(instance);

        // items = parser["i"];
        int I = int.Parse(parser["i"].ToString());
        int W = int.Parse(parser["w"].ToString());

        // This parsing is left over from the template, but we're pretty sure it works for extracting the values from the input
        // Good luck solver team :) - problems team


    }

}
