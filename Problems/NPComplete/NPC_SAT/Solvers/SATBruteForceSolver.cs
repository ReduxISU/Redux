using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Logic;
using API.Interfaces.Steps;
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

    public string solve(string SATInstance) => Solve(CheckedFormula(SATInstance), StepRecorder<Assignment>.Off);

    // This solver takes the raw string (it is not an ISolver<SAT>), so both entry points parse it as a SAT
    // first, as ISolver<T> does: a malformed instance is a ProblemParseException (HTTP 400).
    static CnfFormula CheckedFormula(string instance) => ParseGuard.CreateProblem<SAT>(instance).formula;

    // Records typed steps without implementing ISolver<SAT, Assignment>: this solver has always implemented the
    // non-generic ISolver (see Nav_Solvers.cs), and the generic interface would add it to the navigation lists.
    SolveRun ISolver.Run(string instance, bool withSteps, TimeSpan? timeLimit) {
        CnfFormula formula = CheckedFormula(instance);
        return SolveRun.Record<Assignment>(withSteps, timeLimit, rec => Solve(formula, rec));
    }

    // Tries every assignment, counting through them like a binary number with false = 0 and
    // true = 1 (the last variable is the low bit), starting from all-false.
    // Steps: one per candidate assignment, in the order the counter tries them. A candidate is a Reject naming the first
    // clause with no true literal, or the Accept that ends the search. The recorder's cap
    // hides the rest of a long run. Recorder state lives only in this call.
    internal string Solve(CnfFormula formula, StepRecorder<Assignment> rec) {
        IReadOnlyList<string> variables = formula.Variables;
        List<List<string>> clauseStrings = formula.ClauseStrings();
        bool[] values = new bool[variables.Count];
        Dictionary<string, bool> assignment = new Dictionary<string, bool>();

        while (true) {
            if (timerHasExpired) {
                // Its own timer stopped it: there is no answer, so the run ends on a Done that says so (never on a candidate).
                rec.Done(new Assignment([]), false, GroupedGraphFrames.SolverTimeoutCaption);
                return GroupedGraphFrames.SolverTimeoutAnswer;
            }

            for (int i = 0; i < variables.Count; i++) {
                assignment[variables[i]] = values[i];
            }

            int failed = -1;
            for (int c = 0; c < formula.Clauses.Count; c++) {
                if (!formula.Clauses[c].Literals.Any(l => assignment[l.Variable] != l.Negated)) {
                    failed = c;
                    break;
                }
            }
            if (failed < 0) {
                rec.Accept(() => ToAssignment(assignment),
                    () => $"Try {GroupedGraphFrames.Assigned(assignment)}: every clause has a true literal.",
                    () => []);
                rec.Done(ToAssignment(assignment), true, $"The formula is satisfied by {GroupedGraphFrames.Assigned(assignment)}.");
                return "(" + string.Join(",", variables.Select(v => $"{v}:{assignment[v]}")) + ")";
            }
            List<string> failedClause = clauseStrings[failed];
            rec.Reject(() => ToAssignment(assignment),
                () => $"Try {GroupedGraphFrames.Assigned(assignment)}: clause C{failed + 1} {GroupedGraphFrames.ClauseText(failedClause)} has no true literal.",
                () => [GroupedGraphFrames.ClauseId(failed)]);

            // Advance to the next assignment; wrapping back to all-false means all were tried.
            int bit = values.Length - 1;
            while (bit >= 0 && values[bit]) {
                values[bit] = false;
                bit--;
            }
            if (bit < 0) {
                rec.Done(new Assignment([]), false, "No assignment makes every clause true, so no solution exists.");
                return "No solution exists";
            }
            values[bit] = true;
        }
    }

    private static Assignment ToAssignment(Dictionary<string, bool> values) =>
        new(values.Select(p => new KeyValuePair<string, string>(p.Key, p.Value.ToString())));

    #endregion

}
