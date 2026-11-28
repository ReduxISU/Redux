using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Logic;
using API.Problems.NPComplete.NPC_CIRCUITSAT;

namespace API.Problems.NPComplete.NPC_SAT.ReduceTo.NPC_CIRCUITSAT;

// Every CNF formula is already a Boolean circuit — clauses are OR trees feeding an AND
// tree — so SAT is a special case of CircuitSAT (CLRS 3rd ed., Section 34.3 introduces
// circuits built from AND, OR and NOT gates as the generalization of a formula; this is
// the direction of Cook's theorem, Cook 1971). The reduction is the mechanical rewriting
// done by CnfCircuitEncoder (shared with the SAT3 reduction).
class SatToCircuitSatReduction : IReduction<SAT, CIRCUITSAT> {

    // --- Fields ---
    public string reductionName { get; } = "SAT Circuit Embedding Reduction";
    public string reductionDefinition { get; } = "Turns a CNF formula into the equivalent Boolean circuit: one input wire per variable, a shared NOT gate per negated variable, an OR tree per clause, and an AND tree joining the clause outputs.";
    public string source { get; } = "Cormen, Thomas H.; Leiserson, Charles E.; Rivest, Ronald L.; Stein, Clifford. Introduction to Algorithms. 3rd ed., MIT Press, 2009. Chapter 34, Section 34.3 'NP-completeness and reducibility' (Boolean combinational circuits generalize formulas). After Cook, Stephen A. The complexity of theorem-proving procedures. STOC 1971. 151-158.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://mitpress.mit.edu/9780262033848/introduction-to-algorithms/";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    // One NOT gate per negated variable, one OR gate per extra literal, one AND gate per
    // extra clause: the circuit has O(total literal count) gates and the same inputs.
    public ReductionCost cost { get; } = ReductionCost.Linear;

    // Declared, not derived. The FROM instance is recognized as a special case of the TO
    // problem (a CNF formula IS a two-level circuit), so the transformation is a
    // re-labeling into gate definitions rather than constructed gadgets.
    public ReductionType reductionType { get; } = ReductionType.Restriction;
    // Declared, not derived. One pass over the clauses, emitting O(1) gates per literal.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Linear;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(L), L = total literal count across the SAT formula";

    private SAT _reductionFrom;
    private CIRCUITSAT _reductionTo;

    // --- Properties ---
    public SAT reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public CIRCUITSAT reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }

    // --- Methods Including Constructors ---
    public SatToCircuitSatReduction(SAT from) {
        _reductionFrom = from;
        _reductionTo = reduce();
    }
    public SatToCircuitSatReduction(string instance) : this(new SAT(instance)) { }
    public SatToCircuitSatReduction() : this(new SAT()) { }

    public CIRCUITSAT reduce() {
        string instance = CnfCircuitEncoder.BuildInstance(reductionFrom.clauses);
        reductionTo = new CIRCUITSAT(instance);
        return reductionTo;
    }

    // The circuit's input wires are exactly the formula's variables, so a SAT
    // certificate is already a CircuitSAT certificate; the mapping re-serializes it over
    // the circuit's inputs in canonical form.
    public string mapSolutions(string problemFromSolution) {
        Dictionary<string, bool> assignment;
        try {
            assignment = TruthAssignment.Parse(problemFromSolution);
        } catch (FormatException e) {
            throw new ReductionInputException(this, problemFromSolution,
                reductionFrom.certificateFormat, e.Message, e);
        }
        return TruthAssignment.ToCertificate(reductionTo.circuit.Inputs, assignment);
    }
}
