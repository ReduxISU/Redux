using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Logic;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SAT;

namespace API.Problems.NPComplete.NPC_SAT.Solvers;
#pragma warning disable CS1591

public class SATBruteForceSolver : ISolver<SAT> {


    #region Fields

    // --- Fields ---
    public string solverName { get; } = "SAT Brute Force";
    public string solverDefinition { get; } = "This is a simple brute force solver for SAT";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
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

    // The generic entry point; the visualize endpoint uses ISolver<SAT> to tell which problem this solver runs on.
    string ISolver<SAT>.solve(SAT problem) => Solve(problem.formula, StepRecorder<Assignment>.Off);

    // The string entry points parse the instance as a SAT first, as ISolver<T> does: a malformed instance is a
    // ProblemParseException (HTTP 400).
    static CnfFormula CheckedFormula(string instance) => ParseGuard.CreateProblem<SAT>(instance).formula;

    // Records typed steps through its own Run rather than ISolver<SAT, Assignment>, whose Solve(SAT, ...) would have
    // to be public on this public class while SAT is internal. Passing itself starts its own timer under a time limit (#935).
    SolveRun ISolver.Run(string instance, bool withSteps, TimeSpan? timeLimit, int? stepCap) {
        CnfFormula formula = CheckedFormula(instance);
        return SolveRun.Record<Assignment>(withSteps, timeLimit, rec => Solve(formula, rec), stepCap, this);
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
                return SolveRun.TimedOutAnswer;
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
