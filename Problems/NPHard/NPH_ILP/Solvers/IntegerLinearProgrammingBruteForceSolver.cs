using System.Globalization;
using System.Numerics;
using API.Interfaces;
using API.Interfaces.Steps;

namespace API.Problems.NPHard.NPH_ILP.Solvers;

// Exact exhaustive baseline for instances with explicit finite bounds on each variable.
// Unsupported/unbounded search domains are reported as incomplete, never optimal.
class IntegerLinearProgrammingBruteForceSolver : ISolver<INTEGERLINEARPROGRAMMING, Assignment> {
    public string solverName => "Integer Linear Programming Brute Force";
    public string solverDefinition => "Enumerates every integer assignment within explicit finite per-variable bounds, returning a proven optimum or infeasibility when complete. Throws an exact-search-limit error if the search box exceeds WorkLimit. For compatibility, returns the time-limit status if any variable lacks an explicit lower or upper bound from single-variable constraints (even when no time has elapsed), or when the search is interrupted.";
    public string source => "Original exhaustive enumeration implementation; integer linear programming formulation: Papadimitriou, C. H., and Steiglitz, K. (1982). Combinatorial Optimization: Algorithms and Complexity.";
    public string sourceLink => "https://doi.org/10.1137/1.9780486144811";
    public string sourceFile => SourceFile.Path();
    public string[] contributors => ["Prashant Pant"];
    public SolverType solverType => SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket => SolverComplexityBucket.Exponential;
    public MathematicalFunction timeComplexity { get; } = new("U^n * m * n", "U = maximum number of values per variable, n = number of variables, m = number of constraints");
    public bool timerHasExpired { get; set; }
    public int WorkLimit { get; set; } = 1_000_000;

    public string solve(INTEGERLINEARPROGRAMMING problem) => Run(problem, null);
    public List<object> GetSteps(INTEGERLINEARPROGRAMMING problem) {
        var steps = new List<object>();
        Run(problem, steps);
        return steps;
    }
    public string Solve(INTEGERLINEARPROGRAMMING problem, StepRecorder<Assignment> recorder) => Run(problem, null, recorder);

    private string Run(INTEGERLINEARPROGRAMMING problem, List<object>? steps, StepRecorder<Assignment>? recorder = null) {
        string Finish(string answer, BigInteger[]? values, bool optimal) {
            steps?.Add(new IntegerLinearProgrammingSolver.Step(answer, values is null ? null : (BigInteger[])values.Clone()));
            recorder?.Done(values is null ? new Assignment([]) : ToAssignment(values), optimal,
                optimal ? "Exhaustive enumeration completed." : answer);
            return answer;
        }
        if (timerHasExpired) return Finish(SolveRun.TimedOutAnswer, null, false);
        int n = problem.c.Count;
        var lower = new BigInteger?[n];
        var upper = new BigInteger?[n];
        for (int r = 0; r < problem.A.Count; r++) {
            var row = problem.A[r];
            int index = -1;
            for (int j = 0; j < n; j++) {
                if (row[j].IsZero) continue;
                if (index >= 0) { index = -2; break; }
                index = j;
            }
            if (index == -1) {
                if (problem.b[r] < 0) return Finish("infeasible", null, false);
                continue;
            }
            if (index < 0) continue;
            BigInteger coefficient = row[index];
            if (coefficient > 0) {
                BigInteger bound = FloorDiv(problem.b[r], coefficient);
                upper[index] = upper[index] is null ? bound : BigInteger.Min(upper[index]!.Value, bound);
            } else {
                BigInteger bound = CeilDiv(problem.b[r], coefficient);
                lower[index] = lower[index] is null ? bound : BigInteger.Max(lower[index]!.Value, bound);
            }
        }
        for (int j = 0; j < n; j++)
            if (lower[j] is not null && upper[j] is not null && lower[j] > upper[j])
                return Finish("infeasible", null, false);
        if (lower.Any(v => v is null) || upper.Any(v => v is null))
            return Finish(SolveRun.TimedOutAnswer, null, false);

        // Reject enormous Cartesian products before allocating or iterating.
        BigInteger count = BigInteger.One;
        for (int j = 0; j < n; j++) {
            count *= upper[j]!.Value - lower[j]!.Value + 1;
        }
        if (count > WorkLimit)
            throw new ExactSearchLimitExceededException(problem.problemName, count, WorkLimit);
        var point = lower.Select(v => v!.Value).ToArray();
        BigInteger[]? best = null;
        BigInteger bestValue = BigInteger.Zero;
        while (true) {
            if (timerHasExpired) return Finish(SolveRun.TimedOutAnswer, null, false);
            if (Feasible(problem, point)) {
                BigInteger value = IntegerLinearProgrammingSolver.Dot(problem.c, point);
                if (best is null || value > bestValue) {
                    best = (BigInteger[])point.Clone();
                    bestValue = value;
                    steps?.Add(new IntegerLinearProgrammingSolver.Step("Improved feasible assignment", (BigInteger[])best.Clone()));
                    recorder?.Accept(() => ToAssignment(best), () => "Found a better feasible assignment.");
                }
            }
            int index = n - 1;
            while (index >= 0 && point[index] == upper[index]!.Value) {
                point[index] = lower[index]!.Value;
                index--;
            }
            if (index < 0) break;
            point[index]++;
        }
        return best is null ? Finish("infeasible", null, false)
                            : Finish(IntegerLinearProgrammingSolver.Format(best), best, true);
    }

    private static bool Feasible(INTEGERLINEARPROGRAMMING p, BigInteger[] x) {
        for (int r = 0; r < p.A.Count; r++)
            if (IntegerLinearProgrammingSolver.Dot(p.A[r], x) > p.b[r]) return false;
        return true;
    }
    private static BigInteger FloorDiv(BigInteger a, BigInteger b) {
        BigInteger q = BigInteger.DivRem(a, b, out BigInteger remainder);
        return remainder != 0 && ((remainder > 0) != (b > 0)) ? q - 1 : q;
    }
    private static BigInteger CeilDiv(BigInteger a, BigInteger b) => -FloorDiv(-a, b);
    private static Assignment ToAssignment(IReadOnlyList<BigInteger> x) =>
        new(x.Select((v, i) => new KeyValuePair<string, string>($"x{i + 1}", v.ToString(CultureInfo.InvariantCulture))));
}
