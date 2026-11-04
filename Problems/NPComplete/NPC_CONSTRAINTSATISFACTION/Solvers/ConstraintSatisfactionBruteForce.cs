using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Solvers;

class ConstraintSatisfactionBruteForce : ISolver<CONSTRAINTSATISFACTION> {
    public string solverName => "Constraint Satisfaction Brute Force";
    public string solverDefinition => "Enumerates the Cartesian product of all domains and checks complete assignments.";
    public string source => "Exhaustive finite-domain enumeration.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors => Array.Empty<string>();
    public SolverType solverType => SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket => SolverComplexityBucket.Exponential;
    public MathematicalFunction timeComplexity => new("(d+1)^n * (L+1)^4", ", conservative bound; n = variables, d = maximum domain size, L = instance length");
    private volatile bool expired;
    public bool timerHasExpired { get => expired; set => expired = value; }

    public string solve(CONSTRAINTSATISFACTION problem) {
        void CheckTime() { if (timerHasExpired) throw new OperationCanceledException(); }
        try {
            CheckTime();
            var model = new ConstraintSatisfactionModel(problem, CheckTime);
            int count = problem.X.Count;
            var positions = new int[count];
            var values = new int?[count];
            while (true) {
                CheckTime();
                for (int i = 0; i < count; i++) {
                    CheckTime();
                    values[i] = problem.D[i][positions[i]];
                }
                if (model.Consistent(values, CheckTime)) {
                    CheckTime();
                    return ConstraintSatisfactionModel.Certificate(problem, values);
                }
                int digit = count - 1;
                while (digit >= 0) {
                    CheckTime();
                    if (++positions[digit] < problem.D[digit].Count) break;
                    positions[digit--] = 0;
                }
                if (digit < 0) return "{}";
            }
        } catch (OperationCanceledException) when (timerHasExpired) {
            return SolveRun.TimedOutAnswer;
        }
    }
}
