using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Logic;
using API.Problems.NPComplete.NPC_SAT;

namespace API.Problems.NPComplete.NPC_CIRCUITSAT.ReduceTo.NPC_SAT;

// The reduction from Circuit-SAT to SAT in CLRS (Introduction to Algorithms, 3rd ed.,
// §34.4, the proof of Theorem 34.9; the identical construction is Sipser's Theorem 7.53):
// every wire of the circuit becomes a SAT variable, and each gate g = op(a, b) is
// expressed by the clauses that force g <-> op(a, b). The output wire is forced True with
// a unit clause, giving the formula phi_C = phi_C AND x_m. Satisfiable circuits map to
// satisfiable formulas and vice versa.
class CircuitSatToSatReduction : IReduction<CIRCUITSAT, SAT> {

    // --- Fields ---
    public string reductionName { get; } = "CLRS Circuit-to-SAT Reduction";
    public string reductionDefinition { get; } = "The CLRS reduction from CircuitSAT to SAT: one variable per wire, one small clause group per gate encoding g <-> op(inputs), plus a unit clause forcing the output wire True.";
    public string source { get; } = "Cormen, Thomas H.; Leiserson, Charles E.; Rivest, Ronald L.; Stein, Clifford. Introduction to Algorithms. 3rd ed., MIT Press, 2009. Chapter 34, Section 34.4 'NP-completeness proofs', proof of Theorem 34.9 (CIRCUIT-SAT <=P SAT).";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://mitpress.mit.edu/9780262033848/introduction-to-algorithms/";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    // The formula has 2-3 clauses per gate plus one unit clause for the output, so its
    // size is within a constant factor of the circuit's — linear.
    public ReductionCost cost { get; } = ReductionCost.Linear;

    // Declared, not derived. Each gate is swapped independently for a fixed clause
    // template keyed by its operator; the clauses share no coordination beyond wire names.
    public ReductionType reductionType { get; } = ReductionType.LocalReplacement;
    // Declared, not derived. One pass over the gates, emitting O(1) clauses each.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Linear;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(|C|), |C| = number of gates in the circuit";

    private CIRCUITSAT _reductionFrom;
    private SAT _reductionTo;

    // --- Properties ---
    public CIRCUITSAT reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public SAT reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }

    // --- Methods Including Constructors ---
    public CircuitSatToSatReduction(CIRCUITSAT from) {
        _reductionFrom = from;
        _reductionTo = reduce();
    }
    public CircuitSatToSatReduction(string instance) : this(new CIRCUITSAT(instance)) { }
    public CircuitSatToSatReduction() : this(new CIRCUITSAT()) { }

    public SAT reduce() {
        List<string> clauses = new List<string>();
        foreach (CircuitGate gate in reductionFrom.circuit.Gates) {
            string g = gate.Output;
            switch (gate.Kind) {
                case CircuitGateKind.Not:
                    // g <-> !a
                    clauses.Add($"({gate.Inputs[0]} | {g})");
                    clauses.Add($"(!{gate.Inputs[0]} | !{g})");
                    break;
                case CircuitGateKind.And:
                    // g <-> a & b
                    clauses.Add($"(!{gate.Inputs[0]} | !{gate.Inputs[1]} | {g})");
                    clauses.Add($"({gate.Inputs[0]} | !{g})");
                    clauses.Add($"({gate.Inputs[1]} | !{g})");
                    break;
                case CircuitGateKind.Or:
                    // g <-> a | b
                    clauses.Add($"({gate.Inputs[0]} | {gate.Inputs[1]} | !{g})");
                    clauses.Add($"(!{gate.Inputs[0]} | {g})");
                    clauses.Add($"(!{gate.Inputs[1]} | {g})");
                    break;
                default:
                    throw new InvalidOperationException($"unknown gate kind '{gate.Kind}'");
            }
        }
        clauses.Add($"({reductionFrom.circuit.Output})");

        string instance = string.Join(" & ", clauses);
        reductionTo = new SAT(instance);
        return reductionTo;
    }

    // A CircuitSAT certificate assigns the circuit's inputs; the SAT formula also has a
    // variable per gate. Evaluate the circuit under the given inputs and emit the value
    // of every wire, which satisfies the formula exactly when the inputs satisfy the
    // circuit (the gate clauses pin each gate variable to its computed value).
    public string mapSolutions(string problemFromSolution) {
        Dictionary<string, bool> assignment;
        try {
            assignment = TruthAssignment.Parse(problemFromSolution);
        } catch (FormatException e) {
            throw new ReductionInputException(this, problemFromSolution,
                reductionFrom.certificateFormat, e.Message, e);
        }

        BooleanCircuit circuit = reductionFrom.circuit;
        var inputs = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (string wire in circuit.Inputs) {
            inputs[wire] = assignment.TryGetValue(wire, out bool value) && value;
        }
        Dictionary<string, bool> wireValues = circuit.WireValues(inputs);
        return TruthAssignment.ToCertificate(circuit.Wires, wireValues);
    }
}
