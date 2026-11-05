using API.Interfaces;
using API.Interfaces.Logic;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SAT;

namespace API.Problems.NPComplete.NPC_SAT.Solvers;
#pragma warning disable CS1591

public partial class SATBruteForceSolver : ISolver<SAT> {


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
    // to be public on this public class while SAT is internal. Passing itself starts its own timer under a time limit.
    SolveRun ISolver.Run(string instance, bool withSteps, TimeSpan? timeLimit, int? stepCap) {
        CnfFormula formula = CheckedFormula(instance);
        return SolveRun.Record<Assignment>(withSteps, timeLimit, rec => Solve(formula, rec), stepCap, this);
    }

    // Tries every assignment, counting through them like a binary number with false = 0 and
    // true = 1 (the last variable is the low bit), starting from all-false.
    internal string Solve(CnfFormula formula, StepRecorder<Assignment> rec) {
        var steps = new Steps(formula, rec);
        IReadOnlyList<string> variables = formula.Variables;
        bool[] values = new bool[variables.Count];
        Dictionary<string, bool> assignment = new Dictionary<string, bool>();

        while (true) {
            if (timerHasExpired) {
                steps.TimedOut();
                return SolveRun.TimedOutAnswer;
            }

            for (int i = 0; i < variables.Count; i++) {
                assignment[variables[i]] = values[i];
            }

            int failed = FirstFalseClause(formula, assignment);
            if (failed < 0) {
                steps.Satisfied(assignment);
                return "(" + string.Join(",", variables.Select(v => $"{v}:{assignment[v]}")) + ")";
            }
            steps.Falsified(assignment, failed);

            // Advance to the next assignment; wrapping back to all-false means all were tried.
            int bit = values.Length - 1;
            while (bit >= 0 && values[bit]) {
                values[bit] = false;
                bit--;
            }
            if (bit < 0) {
                steps.NoneSatisfies();
                return "No solution exists";
            }
            values[bit] = true;
        }
    }

    // The index of the first clause with no true literal under the assignment, or -1 when every clause has one.
    private static int FirstFalseClause(CnfFormula formula, Dictionary<string, bool> assignment) {
        for (int c = 0; c < formula.Clauses.Count; c++) {
            if (!formula.Clauses[c].Literals.Any(l => assignment[l.Variable] != l.Negated)) {
                return c;
            }
        }
        return -1;
    }

    #endregion

}
