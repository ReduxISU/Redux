using API.Interfaces;
using API.Interfaces.Logic;

namespace API.Problems.NPComplete.NPC_CIRCUITSAT.Solvers;

// Exhaustive search over the 2^n input assignments of the circuit, evaluating the
// circuit once per assignment. Mirrors SATBruteForceSolver, including its documented
// return values: the certificate on success, "No solution exists" when the circuit is
// unsatisfiable, and SolveRun.TimedOutAnswer when the run-time timer fires (#935: one
// timeout answer for every solver, never a bare "timeout" that looks like an answer).
class CircuitSatBruteForceSolver : ISolver<CIRCUITSAT> {

    // --- Fields ---
    public string solverName { get; } = "CircuitSAT Brute Force";
    public string solverDefinition { get; } = "This is a simple brute force solver for CircuitSAT: it tries every input assignment, counting through them like a binary number, and evaluates the circuit under each.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration of all 2^n assignments.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Enumerates all 2^|I| input assignments; for each it copies the
    // |I| input values and evaluates the |G| gates. The names are CIRCUITSAT's size measures,
    // so the Big-O precheck (#843) can check it.
    public MathematicalFunction timeComplexity { get; } = new("2^|I| * (|I| + |G|)", ", |I| = number of input wires, |G| = number of gates");
    public string[] contributors { get; } = { "Andrija Sevaljevic" };

    // --- Methods Including Constructors ---
    public CircuitSatBruteForceSolver() {

    }

    public string solve(CIRCUITSAT problem) {
        BooleanCircuit circuit = problem.circuit;
        IReadOnlyList<string> inputs = circuit.Inputs;
        bool[] values = new bool[inputs.Count];
        Dictionary<string, bool> assignment = new Dictionary<string, bool>();

        while (true) {
            if (timerHasExpired)
                return SolveRun.TimedOutAnswer;

            for (int i = 0; i < inputs.Count; i++) {
                assignment[inputs[i]] = values[i];
            }
            if (circuit.Evaluate(assignment)) {
                return TruthAssignment.ToCertificate(inputs, assignment);
            }

            // Advance to the next assignment; wrapping back to all-false means all were tried
            // (the last input is the low bit, counting up from all-False, as in SAT).
            int bit = values.Length - 1;
            while (bit >= 0 && values[bit]) {
                values[bit] = false;
                bit--;
            }
            if (bit < 0)
                return "No solution exists";
            values[bit] = true;
        }
    }
}
