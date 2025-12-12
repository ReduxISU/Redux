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
    public string problemLink {get;} = "TODO";
    public string formalDefinition {get;} = "Unstructured Search = {(x, y) | x is int, y is int}";
    public string problemDefinition {get;} = "TODO";
    public string source {get;} = "TODO";
    public string sourceLink {get;} = "TODO";
    private static readonly string _defaultInstance = "(0, 1, 0, 0)";
    public string defaultInstance {get;} = _defaultInstance;
    public string instance {get;set;} = string.Empty;
    public string wikiName {get;} = "";
    public UnstructuredSearchSolver defaultSolver {get;} = new UnstructuredSearchSolver();
    public UnstructuredSearchVerifier defaultVerifier {get;} = new UnstructuredSearchVerifier();
    public UnstructuredSearchVisualization defaultVisualization {get;} = new UnstructuredSearchVisualization();
    public string[] contributors { get; }= { "Alex Svancara" };

    private string _circuit = "";

    public string circuit
    {
        get
        {
            return _circuit;
        }
        set
        {
            _circuit = value;
        }
    }

    private List<int> _funcValues = new List<int>();

    public List<int> funcValues
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

    // --- Methods and Constructors ---
    public UNSTRUCTUREDSEARCH() : this(_defaultInstance) {

    }

    public UNSTRUCTUREDSEARCH(string input) {
        instance = input;

        StringParser parser = new("{y | y is list}");

        parser.parse(instance);

        UtilCollection bitslist = parser["y"];
        funcValues = new List<int>();
        foreach (var bit in bitslist)
        {
            funcValues.Add(int.Parse(bit.ToString()!));
        }
    }
}
