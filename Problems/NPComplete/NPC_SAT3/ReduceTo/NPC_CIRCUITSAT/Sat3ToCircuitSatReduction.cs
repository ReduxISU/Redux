using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Logic;
using API.Problems.NPComplete.NPC_CIRCUITSAT;

namespace API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_CIRCUITSAT;

// 3-CNF formulas are circuits whose OR gates have fan-in at most three, so SAT3 reduces
// to CircuitSAT by the same embedding as SAT — this class is the 3SAT-typed entry in the
// reduction graph, and the construction lives in the shared CnfCircuitEncoder.
class Sat3ToCircuitSatReduction : IReduction<SAT3, CIRCUITSAT> {

    // --- Fields ---
    public string reductionName { get; } = "SAT3 Circuit Embedding Reduction";
    public string reductionDefinition { get; } = "Turns a 3-CNF formula into the equivalent Boolean circuit: one input wire per variable, a shared NOT gate per negated variable, an OR tree per clause, and an AND tree joining the clause outputs.";
    public string source { get; } = "Cormen, Thomas H.; Leiserson, Charles E.; Rivest, Ronald L.; Stein, Clifford. Introduction to Algorithms. 3rd ed., MIT Press, 2009. Chapter 34, Section 34.3 'NP-completeness and reducibility' (Boolean combinational circuits generalize formulas); Section 34.4, Theorem 34.10, for 3-CNF-SAT. After Cook, Stephen A. The complexity of theorem-proving procedures. STOC 1971. 151-158.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://mitpress.mit.edu/9780262033848/introduction-to-algorithms/";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    // One NOT gate per negated variable, at most two OR gates per 3-literal clause, one
    // AND gate per extra clause: linear in the formula's literal count.
    public ReductionCost cost { get; } = ReductionCost.Linear;

    // Declared, not derived. 3-CNF is a syntactic fragment of the circuits this
    // reduction emits, so the transformation is a re-labeling into gate definitions —
    // the textbook "TO is a restriction of the general problem seen from the other side".
    public ReductionType reductionType { get; } = ReductionType.Restriction;
    // Declared, not derived. One pass over the clauses, at most two OR gates each.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Linear;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(L), L = total literal count across the 3SAT formula";

    private SAT3 _reductionFrom;
    private CIRCUITSAT _reductionTo;

    // --- Properties ---
    public SAT3 reductionFrom {
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
    public Sat3ToCircuitSatReduction(SAT3 from) {
        _reductionFrom = from;
        _reductionTo = reduce();
    }
    public Sat3ToCircuitSatReduction(string instance) : this(new SAT3(instance)) { }
    public Sat3ToCircuitSatReduction() : this(new SAT3()) { }

    public CIRCUITSAT reduce() {
        string instance = CnfCircuitEncoder.BuildInstance(reductionFrom.clauses);
        reductionTo = new CIRCUITSAT(instance);
        return reductionTo;
    }

    // The circuit's input wires are exactly the formula's variables, so a 3SAT
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
