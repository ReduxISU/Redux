using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Solvers;

// Exhaustive Cartesian-product enumeration. Unlike backtracking, this solver checks
// complete assignments only, providing a simple independent search strategy.
class ConstraintSatisfactionBruteForce : ISolver<CONSTRAINTSATISFACTION> {
    public string solverName => "Constraint Satisfaction Brute Force";
    public string solverDefinition => "Enumerates the Cartesian product of all domains and checks complete assignments.";
    public string source => "Exhaustive finite-domain enumeration.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors => Array.Empty<string>();
    public SolverType solverType => SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket => SolverComplexityBucket.Exponential;
    public MathematicalFunction timeComplexity => new("d^n * (n+L+1)", ", n = variables, d = max(1, maximum domain size), L = constraint text length; excludes input parsing");
    // Redux's timer may write from another thread; do not reset it inside solve.
    private volatile bool expired;
    public bool timerHasExpired { get => expired; set => expired = value; }

    // Returns the first satisfying assignment, {} on exhaustion, or the Redux timeout
    // answer on cancellation. Invalid problem syntax and arithmetic errors propagate.
    public string solve(CONSTRAINTSATISFACTION problem) {
        void CheckTime() { if (timerHasExpired) throw new OperationCanceledException(); }
        try {
            CheckTime();
            // Prepare once, then reuse the model for every combination of domain values.
            var model = new ConstraintSatisfactionModel(problem, CheckTime);
            int count = problem.X.Count;
            // Each position is a digit whose radix is that variable's domain length.
            // Domain indices avoid arithmetic assumptions about sparse or decimal values.
            var positions = new int[count];
            var values = new decimal?[count];
            // Enter even for zero variables: the Cartesian product then contains one
            // empty assignment, whose constant constraints must still be evaluated.
            while (true) {
                CheckTime();
                for (int i = 0; i < count; i++) {
                    CheckTime();
                    values[i] = problem.D[i][positions[i]];
                }
                if (model.Consistent(values, CheckTime)) {
                    CheckTime();
                    return ConstraintSatisfactionModel.Certificate(problem, values, CheckTime);
                }
                // Increment the rightmost digit and propagate carries. Falling beyond
                // the first digit proves that every domain combination was attempted.
                int digit = count - 1;
                while (digit >= 0) {
                    CheckTime();
                    if (++positions[digit] < problem.D[digit].Count) break;
                    positions[digit--] = 0;
                }
                if (digit < 0) return "{}";
            }
        } catch (OperationCanceledException) when (timerHasExpired) {
            // Timeout is an execution outcome; it must not be confused with exhaustion.
            // All other errors retain their original exception contract.
            return SolveRun.TimedOutAnswer;
        }
    }
}
