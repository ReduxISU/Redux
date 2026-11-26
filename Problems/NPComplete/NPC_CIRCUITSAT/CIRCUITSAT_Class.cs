using API.DummyClasses;
using API.Interfaces;
using API.Interfaces.Logic;
using API.Problems.NPComplete.NPC_CIRCUITSAT.Solvers;
using API.Problems.NPComplete.NPC_CIRCUITSAT.Verifiers;

namespace API.Problems.NPComplete.NPC_CIRCUITSAT;

class CIRCUITSAT : IProblem<CircuitSatBruteForceSolver, CircuitSatVerifier, DummyVisualization> {

    // --- Fields ---
    public string problemName { get; } = "CircuitSAT";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Circuit_satisfiability_problem";
    public string formalDefinition { get; } = "CircuitSAT = {<C> | C is a satisfiable Boolean combinational circuit}, where a circuit is the tuple ({inputs},(gates),output) of input wires, NOT/AND/OR gates and one output wire";
    public string problemDefinition { get; } = "Circuit SAT, or the Boolean circuit satisfiability problem, is a problem that asks whether some assignment of values to the input wires of a Boolean circuit makes its output wire True.";
    public string inputDescription { get; } = "C, a Boolean circuit built from NOT, AND and OR gates";
    public string outputDescription { get; } = "True or False, whether some input assignment makes C's output wire True";
    // Not one of Karp's 21: the circuit-satisfiability problem is stated and proved
    // NP-complete in CLRS, which is the standard reference for it (Lemma 34.5 for NP
    // membership, Lemma 34.6 for NP-hardness — the Cook-Levin construction that pastes
    // a verifier's computation into a circuit, so Cook 1971 is its origin — and
    // Theorem 34.7 for NP-completeness).
    public string source { get; } = "Cormen, Thomas H.; Leiserson, Charles E.; Rivest, Ronald L.; Stein, Clifford. Introduction to Algorithms. 3rd ed., MIT Press, 2009. Chapter 34 'NP-Completeness', Section 34.3 'NP-completeness and reducibility' — the circuit-satisfiability problem; Lemma 34.5, Lemma 34.6 and Theorem 34.7 prove CIRCUIT-SAT NP-complete.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://mitpress.mit.edu/9780262033848/introduction-to-algorithms/";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };

    // The shape SPADE checks (see CircuitParser for the rules that depend on the names).
    public const string InstanceGrammar = CircuitParser.InstanceGrammar;

    public static string _defaultInstance { get; } = "({x1,x2,x3},((g1,and,x1,x2),(g2,not,x3),(g3,or,g1,g2)),g3)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instanceFormat { get; } = "Boolean circuit as the tuple ({input wires},(gates),output wire). The inputs are a comma-separated set in braces. Each gate is a tuple (output wire,operator,input wires), where the operator is 'and', 'or' or 'not' — 'not' takes one input wire, 'and' and 'or' take two — and every input wire must already be a declared input or the output of an earlier gate. The output wire may be any declared wire, including an input. Example: ({x1,x2,x3},((g1,and,x1,x2),(g2,not,x3),(g3,or,g1,g2)),g3)";
    public string certificateFormat { get; } = "Comma-separated variable: Boolean pairs, optionally wrapped in parentheses. Booleans must be capitalized True/False (T/F also accepted); ':' or '=' may be used as the separator. Name input wires only (a gate's wire is computed, not assigned); unassigned inputs are read as False. Example: (x1:True,x2:True,x3:False)";
    public CircuitSatBruteForceSolver defaultSolver { get; } = new CircuitSatBruteForceSolver();
    public CircuitSatVerifier defaultVerifier { get; } = new CircuitSatVerifier();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;
    public ProblemType problemType { get; } = ProblemType.Logic;
    // Declared, not derived (#616): the instance carries no bound and the answer is yes/no —
    // does *some* input assignment make the output wire True.
    public ProblemVariant problemVariant { get; } = ProblemVariant.Decision;
    public string instance { get; set; } = string.Empty;

    public string wikiName { get; } = "";
    private List<string> _inputs = new List<string>();
    private List<string> _gates = new List<string>();
    private List<string> _wires = new List<string>();

    // --- Properties ---
    // JSON-friendly views of the parsed circuit (each gate as its instance tuple, e.g.
    // "(g1,and,x1,x2)"), following the clauses/literals pattern of SAT and SAT3.
    public List<string> inputs {
        get {
            return _inputs;
        }
        set {
            _inputs = value;
        }
    }
    public List<string> gates {
        get {
            return _gates;
        }
        set {
            _gates = value;
        }
    }
    public List<string> wires {
        get {
            return _wires;
        }
        set {
            _wires = value;
        }
    }
    public string output { get; private set; } = string.Empty;

    // The parsed instance. Internal so it stays out of the serialized problem JSON.
    // Note: the list properties above still have public setters for older callers; code
    // that assigns them directly does not update this. Redux code paths that need the
    // circuit (solver, verifier, reductions) construct CIRCUITSAT from an instance string.
    internal BooleanCircuit circuit { get; private set; }

    // --- Methods Including Constructors ---
    public CIRCUITSAT() : this(_defaultInstance) {
    }
    public CIRCUITSAT(string instance) {
        circuit = parseInstance(instance);
        this.instance = instance;
        inputs = circuit.Inputs.ToList();
        gates = circuit.Gates.Select(g => g.ToDefinitionString()).ToList();
        wires = circuit.Wires.ToList();
        output = circuit.Output;
    }

    private static BooleanCircuit parseInstance(string instance) {
        if (string.IsNullOrWhiteSpace(instance)) {
            throw new ProblemParseException("CircuitSAT", instance, "instance is empty");
        }

        try {
            return CircuitParser.Parse(instance);
        } catch (CircuitParseException e) {
            throw new ProblemParseException("CircuitSAT", instance, e.Message, e);
        }
    }

    public List<string> getInputs(string instance) {
        return parseInstance(instance).Inputs.ToList();
    }

    public string getOutputWire(string instance) {
        return parseInstance(instance).Output;
    }
}
