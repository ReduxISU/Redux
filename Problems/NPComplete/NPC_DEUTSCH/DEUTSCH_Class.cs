using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_DEUTSCH.Solvers;
using API.Problems.NPComplete.NPC_DEUTSCH.Verifiers;
using SPADE;

namespace API.Problems.NPComplete.NPC_DEUTSCH;

using System.Text.Json.Serialization;

class DEUTSCH : IProblem<DeutschClassicalSolver, DeutschClassicalVerifier, DummyVisualization> {

    // --- Fields ---
    public string problemName { get; } = "Deutsch"; // Name as it appears in the dropdown selection panel
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Deutsch%E2%80%93Jozsa_algorithm#Deutsch's_algorithm"; // Link to the Wikipedia page for the problem
    public string formalDefinition { get; } = "Deutsch = {<i, w> | i is bit (0 or 1), w is bit (0 or 1)}"; // Mathematical description of the problem (todo later)
    public string problemDefinition { get; } = "Deutsch's algorithm determines whether a given function f: {0,1} -> {0,1} is constant or balanced. The problem has four possible input functions and is represented to the ordered list of outputs, i.e. (f(0), f(1))."; // plaintext description of the problem
    public string source { get; } = "Deutsch, David. 1985. Quantum theory, the Church-Turing principle and the universal quantum computer. Proc. R. Soc. Lond. A40097-117"; // Academic paper proper citation
    public string sourceLink { get; } = "https://royalsocietypublishing.org/doi/10.1098/rspa.1985.0070"; // Link to the academic paper
    public const string InstanceGrammar = "{(i,w) | i is int, w is int}";
    private static readonly string _defaultInstance = "(0,1)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;
    public string instanceFormat { get; } = $"Format: {InstanceGrammar} (i,w are bits encoding f: f(0) = (i != 0), f(1) = (w != 0)) Example: {_defaultInstance}";
    public string certificateFormat { get; } =
        $"Format: {DeutschClassicalVerifier.CertificateGrammar} Example: {DeutschClassicalVerifier.CertificateExample}";
    public string wikiName { get; } = ""; // Wiki name or link? - not used yet
    public DeutschClassicalSolver defaultSolver { get; } = new DeutschClassicalSolver();
    public DeutschClassicalVerifier defaultVerifier { get; } = new DeutschClassicalVerifier();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();
    public string[] contributors { get; } = { "Eric Hill", "Paul Gilbreath", "Max Gruenwoldt", "Alex Svancara", "Jason L. Wright" };
    // Declared, not derived. Deutsch's algorithm solves this promise problem with a
    // single oracle query and zero error probability — exact, not merely bounded-error
    // — so it belongs in EQP, not the classical P/NP hierarchy. See ComplexityClass.EQP.
    public ComplexityClass complexityClass { get; } = ComplexityClass.EQP;
    public ProblemType problemType { get; } = ProblemType.Miscellaneous;

    // --- Methods and Constructors ---
    public DEUTSCH() : this(_defaultInstance) {

    }

    private bool[] _funcValues = new bool[2] { false, false };

    public bool[] funcValues {
        get {
            return _funcValues;
        }
        set {
            _funcValues = value;
        }
    }

    public bool Func(bool x) {
        return x ? funcValues[1] : funcValues[0];
    }

    public DEUTSCH(string input) {
        instance = input;

        StringParser parser = new(InstanceGrammar);

        parser.parse(instance);

        // items = parser["i"];
        int I = int.Parse(parser["i"].ToString());
        int W = int.Parse(parser["w"].ToString());

        // determine which function to use
        funcValues = new bool[2] { I != 0, W != 0 }; ;
    }
}
