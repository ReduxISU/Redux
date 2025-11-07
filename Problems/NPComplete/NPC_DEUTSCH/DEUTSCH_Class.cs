using API.Interfaces;
using API.Problems.NPComplete.NPC_DEUTSCH.Solvers;
using API.Problems.NPComplete.NPC_DEUTSCH.Verifiers;

namespace API.Problems.NPComplete.NPC_DEUTSCH;

class DEUTSCH : IProblem<ProblemSolver, ProblemVerifier> {

    // --- Fields ---
    public string problemName {get;} = "{NAME}";
    public string formalDefinition {get;} = "TODO";
    public string problemDefinition {get;} = "TODO";
    public string source {get;} = "TODO";
    private static readonly string _defaultInstance = "{(0,1)}";
    public string defaultInstance {get;} = _defaultInstance;
    public string instance {get;set;} = string.Empty;
    public string wikiName {get;} = "";
    public ProblemSolver defaultSolver {get;} = new ProblemSolver();
    public ProblemVerifier defaultVerifier {get;} = new ProblemVerifier();
    public string[] contributers = { "TODO" };

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

    public DEUTSCH(string instance) {
        _instance = instance;



        // TODO: implement parsing of string instance of {NAME}. SPADE is a class meant to help with this step, see https://github.com/Jetison333/SPADE/blob/main/Documentation/index.md for more information.
        //
        StringParser parser = new("{(i, w) | i is int, w is int}");
        parser.parse(instance);
        items = parser["i"];
        I = int.Parse(parser["i"].ToString());
        W = int.Parse(parser["w"].ToString());


    }

}
