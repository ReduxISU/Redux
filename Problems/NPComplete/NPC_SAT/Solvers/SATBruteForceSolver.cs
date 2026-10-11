using API.Interfaces;
using API.Interfaces.Logic;
using API.Problems.NPComplete.NPC_SAT;

namespace API.Problems.NPComplete.NPC_SAT.Solvers;
#pragma warning disable CS1591

// TODO: use generic `ISolver<SAT>` for type safety
public class SATBruteForceSolver : ISolver {


    #region Fields

    // --- Fields ---
    public string solverName { get; } = "SAT Brute Force";
    public string solverDefinition { get; } = "This is a simple brute force solver for SAT";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration. Implements non-generic ISolver directly
    // (not ISolver<T>) -- included here so SolverTypeCatalog's `is ISolver` check (not `is ISolver<T>`)
    // does not silently skip it.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Enumerates all 2^n truth assignments (n = distinct literals);
    // each is evaluated against every clause, O(|phi|) total literal occurrences.
    public MathematicalFunction timeComplexity { get; } = new("2^n * |phi|", ", n = number of variables, |phi| = formula size");
    public string[] contributors { get; } = { "Daniel Igbokwe", "Show Pratoomratana" };

    #endregion

    #region Constructors
    // --- Methods Including Constructors ---
    public SATBruteForceSolver() {

    }
    #endregion


    #region Methods

    // Tries every assignment, counting through them like a binary number with false = 0 and
    // true = 1 (the last variable is the low bit), starting from all-false.
    public string solve(string SATInstance) {
        // A malformed instance becomes a ProblemParseException (HTTP 400).
        CnfFormula formula = ParseGuard.CreateProblem<SAT>(SATInstance).formula;
        IReadOnlyList<string> variables = formula.Variables;
        bool[] values = new bool[variables.Count];
        Dictionary<string, bool> assignment = new Dictionary<string, bool>();

        while (true) {
            if (timerHasExpired)
                return "timeout";

            for (int i = 0; i < variables.Count; i++) {
                assignment[variables[i]] = values[i];
            }
            if (formula.Evaluate(assignment)) {
                return "(" + string.Join(",", variables.Select(v => $"{v}:{assignment[v]}")) + ")";
            }

            // Advance to the next assignment; wrapping back to all-false means all were tried.
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

    #endregion

}
