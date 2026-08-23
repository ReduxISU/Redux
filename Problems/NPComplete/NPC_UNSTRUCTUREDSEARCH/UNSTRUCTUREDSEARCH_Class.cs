using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Solvers;
using API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Verifiers;
using API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Visualizations;
using SPADE;

namespace API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH;

class UNSTRUCTUREDSEARCH : IProblem<UnstructuredSearchSolver, UnstructuredSearchVerifier, UnstructuredSearchVisualization> {

    // --- Fields ---
    public string problemName { get; } = "Unstructured Search";
    public string problemLink { get; } = "https://quantum.cloud.ibm.com/learning/en/courses/fundamentals-of-quantum-algorithms/grover-algorithm/unstructured-search";
    public string formalDefinition { get; } = "Unstructured Search = {(x, y) | x is int, y is int}";
    public string problemDefinition { get; } = "Input: a function f:Σn→Σf:Σn→Σ; Output: a string x∈Σnx∈Σn satisfying f(x)=1,f(x)=1, or \"no solution\" if no such string xx exists";
    public string source { get; } = "Grover, L. K. (1996). A fast quantum mechanical algorithm for database search. In Proceedings of the twenty-eighth annual ACM symposium on Theory of computing (pp. 212-219).";
    public string sourceLink { get; } = "https://dl.acm.org/doi/pdf/10.1145/237814.237866";
    public const string InstanceGrammar = "{y | y is list}";
    private static readonly string _defaultInstance = "(0, 1, 0, 0)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instanceFormat { get; } =
        $"Format: {InstanceGrammar} (y = (f(0), f(1), ..., f(n-1)), the oracle function's output bits) Example: {_defaultInstance}";
    public string certificateFormat { get; } =
        $"Format: {UnstructuredSearchVerifier.CertificateGrammar} Example: {UnstructuredSearchVerifier.CertificateExample}";
    public string instance { get; set; } = string.Empty;
    public string wikiName { get; } = "";
    public UnstructuredSearchSolver defaultSolver { get; } = new UnstructuredSearchSolver();
    public UnstructuredSearchVerifier defaultVerifier { get; } = new UnstructuredSearchVerifier();
    public UnstructuredSearchVisualization defaultVisualization { get; } = new UnstructuredSearchVisualization();
    public string[] contributors { get; } = { "Alex Svancara" };
    // Declared, not derived. Grover/unstructured search is a query-complexity promise
    // problem over an oracle, not a citizen of the classical P/NP hierarchy — given an
    // explicit input instead of an oracle it is trivially in P. See
    // ComplexityClass.QuantumOracle.
    public ComplexityClass complexityClass { get; } = ComplexityClass.QuantumOracle;

    private string _circuit = "";

    public string circuit {
        get {
            return _circuit;
        }
        set {
            _circuit = value;
        }
    }

    private List<int> _funcValues = new List<int>();

    public List<int> funcValues {
        get {
            return _funcValues;
        }
        set {
            _funcValues = value;
        }
    }

    // --- Methods and Constructors ---
    public UNSTRUCTUREDSEARCH() : this(_defaultInstance) {

    }

    public UNSTRUCTUREDSEARCH(string input) {
        instance = input;

        StringParser parser = new(InstanceGrammar);

        parser.parse(instance);

        UtilCollection bitslist = parser["y"];
        funcValues = new List<int>();
        foreach (var bit in bitslist) {
            funcValues.Add(int.Parse(bit.ToString()!));
        }
    }
}
