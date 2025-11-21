using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI.Solvers;
using API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI.Verifiers;
using SPADE;

namespace API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI;

class BERNSTEINVAZIRANI : IProblem<BernsteinVaziraniSolver, BernsteinVaziraniVerifier, DummyVisualization> {

    // --- Fields ---
    public string problemName {get;} = "Bernstein Vazirani"; // Name as it appears in the dropdown selection panel
    public string problemLink {get;} = "https://en.wikipedia.org/wiki/Bernstein%E2%80%93Vazirani_algorithm"; // Link to the Wikipedia page for the problem
    public string formalDefinition {get;} =  "Bernstein Vazirani = {(n, <w_1, w_2, ... , w_(2^n - 1), w_(2^n)> | n is int, w_i is bit (0 or 1)}"; // Mathematical description of the problem (todo later)
    public string problemDefinition { get; } = "The Bernstein-Vazirani problem asks for the identification of an unknown bit string s that defines a linear Boolean function f(x)= s*x (mod 2). The task is to determine the hidden string s using as few queries as possible"; // plaintext description of the problem
    public string source { get; } = "Bernstein, Ethan, and Umesh, Vazirani. Quantum complexity theory. Proceedings of the twenty-fifth annual ACM symposium on Theory of computing. 1993."; // Academic paper proper citation
    public string sourceLink { get; } = "https://dl.acm.org/doi/pdf/10.1145/167088.167097"; // Link to the academic paper
    private static readonly string _defaultInstance = "(2,(1, 1, 1, 1))";
    public string defaultInstance {get;} = _defaultInstance;
    public string instance {get;set;} = string.Empty;
    public string wikiName {get;} = ""; // Wiki name or link? - not used yet
    public BernsteinVaziraniSolver defaultSolver {get;} = new BernsteinVaziraniSolver();
    public BernsteinVaziraniVerifier defaultVerifier { get; } = new BernsteinVaziraniVerifier();
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
    public BERNSTEINVAZIRANI() : this(_defaultInstance) {

    }

    public BERNSTEINVAZIRANI(string input) {
        instance = input;



        // TODO: implement parsing of string instance of {NAME}. SPADE is a class meant to help with this step, see https://github.com/Jetison333/SPADE/blob/main/Documentation/index.md for more information.
        //


        StringParser parser = new("{(n, S) | n is int, S is list}");

        parser.parse(instance);

        // items = parser["i"];

        int n = int.Parse(parser["n"].ToString());
        SPADE.UtilCollection bitslist = parser["S"];
        Console.WriteLine(bitslist);

        // This parsing is left over from the template, but we're pretty sure it works for extracting the values from the input
        // Good luck solver team :) - problems team


    }

}
