using System.Globalization;
using System.Numerics;
using API.Interfaces;
using API.Interfaces.Steps;

namespace API.Problems.NPHard.NPH_ILP.Solvers;

// Deterministic feasibility-first coordinate local search; no approximation guarantee.
class IntegerLinearProgrammingHeuristicSolver : ISolver<INTEGERLINEARPROGRAMMING, Assignment> {
    public string solverName => "Integer Linear Programming Local Search Heuristic";
    public string solverDefinition => "Heuristic coordinate search that first reduces total constraint violation, then improves the objective using feasible neighboring assignments. A returned assignment is feasible, but is not guaranteed optimal; no approximation ratio is claimed.";
    public string source => "Original feasibility-first coordinate local search heuristic.";
    public string sourceLink => "";
    public string sourceFile => SourceFile.Path();
    public string[] contributors => ["Prashant Pant"];
    public SolverType solverType => SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket => SolverComplexityBucket.Exponential;
    public MathematicalFunction timeComplexity { get; } = new("K * m * n^2", "K = iteration budget, m = constraints, n = variables");
    public bool timerHasExpired { get; set; }
    public int IterationLimit { get; set; } = 5_000;

    public string solve(INTEGERLINEARPROGRAMMING problem) => Run(problem, null);
    public List<object> GetSteps(INTEGERLINEARPROGRAMMING problem) {
        var steps = new List<object>();
        Run(problem, steps);
        return steps;
    }
    public string Solve(INTEGERLINEARPROGRAMMING problem, StepRecorder<Assignment> recorder) => Run(problem, null, recorder);

    private string Run(INTEGERLINEARPROGRAMMING problem, List<object>? steps, StepRecorder<Assignment>? recorder = null) {
        string Finish(string result, BigInteger[]? point) {
            steps?.Add(new SearchStep(result, point is null ? null : (BigInteger[])point.Clone()));
            recorder?.Done(
                point is null ? new Assignment([]) : ToAssignment(point),
                point is not null,
                point is null ? result : "Feasible heuristic assignment; optimality not proven."
            );
            return result;
        }
        if (timerHasExpired || IterationLimit <= 0) return Finish(SolveRun.TimedOutAnswer, null);
        int n = problem.c.Count;
        BigInteger[] current = new BigInteger[n];
        BigInteger violation = Violation(problem, current);
        BigInteger objective = IntegerLinearProgrammingSolver.Dot(problem.c, current);
        BigInteger[]? best = null;
        BigInteger bestObjective = BigInteger.Zero;
        if (violation.IsZero) {
            best = (BigInteger[])current.Clone();
            bestObjective = objective;
            steps?.Add(new SearchStep("Feasible assignment", (BigInteger[])best.Clone()));
            recorder?.Accept(() => ToAssignment(best), () => "Found a feasible heuristic assignment.");
        }
        // Multiple scales let the search cross large distances without integer-by-integer walks.
        BigInteger[] scales = [1024, 256, 64, 16, 4, 1];
        int iterations = 0;
        foreach (BigInteger scale in scales) {
            while (iterations < IterationLimit) {
                if (timerHasExpired) return Finish(SolveRun.TimedOutAnswer, null);
                iterations++;
                BigInteger[]? next = null;
                BigInteger nextViolation = violation;
                BigInteger nextObjective = objective;
                for (int j = 0; j < n; j++) {
                    foreach (int sign in new[] { -1, 1 }) {
                        var candidate = (BigInteger[])current.Clone();
                        candidate[j] += sign * scale;
                        BigInteger candidateViolation = Violation(problem, candidate);
                        BigInteger candidateObjective = IntegerLinearProgrammingSolver.Dot(problem.c, candidate);
                        if (candidateViolation < nextViolation ||
                            (candidateViolation == nextViolation && candidateViolation.IsZero && candidateObjective > nextObjective)) {
                            next = candidate;
                            nextViolation = candidateViolation;
                            nextObjective = candidateObjective;
                        }
                    }
                }
                if (next is null) break;
                current = next;
                violation = nextViolation;
                objective = nextObjective;
                if (violation.IsZero && (best is null || objective > bestObjective)) {
                    best = (BigInteger[])current.Clone();
                    bestObjective = objective;
                    steps?.Add(new SearchStep("Improved feasible assignment", (BigInteger[])best.Clone()));
                    recorder?.Accept(() => ToAssignment(best), () => "Found a feasible heuristic assignment.");
                }
            }
            if (iterations >= IterationLimit) break;
        }
        // No feasible point found is not a proof of infeasibility.
        return best is null ? Finish(SolveRun.TimedOutAnswer, null)
                            : Finish(IntegerLinearProgrammingSolver.Format(best), best);
    }

    internal record SearchStep(string Status, BigInteger[]? Assignment);
    private static BigInteger Violation(INTEGERLINEARPROGRAMMING p, BigInteger[] x) {
        BigInteger total = BigInteger.Zero;
        for (int r = 0; r < p.A.Count; r++) {
            BigInteger excess = IntegerLinearProgrammingSolver.Dot(p.A[r], x) - p.b[r];
            if (excess > 0) total += excess;
        }
        return total;
    }
    private static Assignment ToAssignment(IReadOnlyList<BigInteger> x) =>
        new(x.Select((v, i) => new KeyValuePair<string, string>($"x{i + 1}", v.ToString(CultureInfo.InvariantCulture))));
}
