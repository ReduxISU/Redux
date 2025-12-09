using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_PRIMEFACTOR.Solvers;
using API.Problems.NPComplete.NPC_PRIMEFACTOR.Verifiers;
using SPADE;

namespace API.Problems.NPComplete.NPC_PRIMEFACTOR;

class PRIMEFACTOR : IProblem<PrimeFactorSolver, PrimeFactorVerifier, DummyVisualization> {

    // --- Fields ---
    public string problemName { get; } = "Prime Factorization"; // Name as it appears in the dropdown selection panel
    public string problemLink {get;} = "https://en.wikipedia.org/wiki/Fundamental_theorem_of_arithmetic";
    public string formalDefinition {get;} =  "Prime Factorization = {<i> | i is int}"; // Mathematical description of the problem (todo later)
    public string problemDefinition { get; } = "The prime factorization algorithm solves the decomposition of a positive integer into a product of prime integers."; // plaintext description of the problem
    public string source { get; } = "Gauss, Carl Friedrich (1801), Disquisitiones Arithmeticae (in Latin), Leipzig: Gerh. Fleischer"; // Academic paper proper citation
    public string sourceLink { get; } = "https://archive.org/details/disquisitionesa00gaus/page/330/mode/2up"; // Link to the academic paper
    private static readonly string _defaultInstance = "123456";
    public string defaultInstance {get;} = _defaultInstance;
    public string instance {get;set;} = string.Empty;
    public string wikiName {get;} = "https://en.wikipedia.org/wiki/Fundamental_theorem_of_arithmetic"; // Wiki name or link? - not used yet
    public PrimeFactorSolver defaultSolver {get;} = new PrimeFactorSolver();
    public PrimeFactorVerifier defaultVerifier { get; } = new PrimeFactorVerifier();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();
    public string[] contributors {get;} = { "Paul Gilbreath", "Alex Svancara" };

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
    public PRIMEFACTOR() : this(_defaultInstance) {

    }

    public PRIMEFACTOR(string input) {
        instance = input;



        // TODO: implement parsing of string instance of {NAME}. SPADE is a class meant to help with this step, see https://github.com/Jetison333/SPADE/blob/main/Documentation/index.md for more information.
        //


        StringParser parser = new("{ i | i is int}");

        parser.parse(instance);

        // items = parser["i"];
        int I = int.Parse(parser["i"].ToString());

        // This parsing is left over from the template, but we're pretty sure it works for extracting the values from the input
        // Good luck solver team :) - problems team


    }

}
