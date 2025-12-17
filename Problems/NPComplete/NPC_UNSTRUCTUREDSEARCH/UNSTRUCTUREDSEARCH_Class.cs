using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Solvers;
using API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Verifiers;
using API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Visualizers;
using SPADE;

namespace API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH;

class UNSTRUCTUREDSEARCH : IProblem<UnstructuredSearchSolver, UnstructuredSearchVerifier, UnstructuredSearchVisualization> {

    // --- Fields ---
    public string problemName {get;} = "Unstructured Search";
    public string problemLink {get;} = "https://quantum.cloud.ibm.com/learning/en/courses/fundamentals-of-quantum-algorithms/grover-algorithm/unstructured-search";
    public string formalDefinition {get;} = "Unstructured Search = {(x, y) | x is int, y is int}";
    public string problemDefinition {get;} = "Input: a function f:Σn→Σf:Σn→Σ; Output: a string x∈Σnx∈Σn satisfying f(x)=1,f(x)=1, or \"no solution\" if no such string xx exists";
    public string source {get;} = "TODO";
    public string sourceLink {get;} = "https://dl.acm.org/doi/pdf/10.1145/237814.237866";
    private static readonly string _defaultInstance = "(0, 1, 0, 0)";
    public string defaultInstance {get;} = _defaultInstance;
    public string instance {get;set;} = string.Empty;
    public string wikiName {get;} = "";
    public UnstructuredSearchSolver defaultSolver {get;} = new UnstructuredSearchSolver();
    public UnstructuredSearchVerifier defaultVerifier {get;} = new UnstructuredSearchVerifier();
    public UnstructuredSearchVisualization defaultVisualization {get;} = new UnstructuredSearchVisualization();
    public string[] contributors { get; }= { "Alex Svancara" };

    // TODO: implement properties if {NAME} is a graphing problem
    // private List<string> _nodes = new List<string>();
    // private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    // private int _K ;
    // private {NAME_PASCEL_CASE}Graph _{NAME_CAMEL_CASE}AsGraph;

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
    // public {NAME_PASCEL_CASE}Graph {NAME_CAMEL_CASE}AsGraph {
    //     get{
    //         return _{NAME_CAMEL_CASE}AsGraph;
    //     }
    //     set{
    //         _{NAME_CAMEL_CASE}AsGraph = value;
    //     }
    // }

    // --- Methods and Constructors ---
    public UNSTRUCTUREDSEARCH() : this(_defaultInstance) {

    }

    public UNSTRUCTUREDSEARCH(string input) {
        instance = input;



        // TODO: implement parsing of string instance of {NAME}. SPADE is a class meant to help with this step, see https://github.com/Jetison333/SPADE/blob/main/Documentation/index.md for more information.
        //
        // StringParser parser = new("{(x, y) | x is int, y is {int}}");

        // parser.parse(instance);

        // int X = int.Parse(parser["x"].ToString());
        // var Y = parser["y"].AsList().Select(v => (int)v).ToList();
        
        StringParser parser = new("{(x, y) | x is int, y is int}");

        parser.parse(instance);

        // Parse x
        int x = int.Parse(parser["i"].ToString());
        int y = int.Parse(parser["w"].ToString());

        //
        // Or a unidirected unweighted graph example using SPADE
        // 
        // StringParser parser = new("{(N,E) | N is set, E subset N unorderdcross N}");
        // parser.parse(instance);
        // edges = parser["E"];
        // nodes = parser["N"];

    }
}
