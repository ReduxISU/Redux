using API.Interfaces;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Solvers;

class WeightedMaxSatBruteForceSolver : ISolver<WEIGHTEDMAXSAT> {
    // --- Fields ---
    public string solverName { get; } = "Weighted Max-SAT Exact Brute Force";
    public string solverDefinition { get; } = "Enumerates every truth assignment and returns one with maximum satisfied clause weight. This exact baseline guarantees an optimal solution.";
    public string source { get; } = "Exhaustive search.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.BruteForce;
    public MathematicalFunction timeComplexity { get; } = new("2^n * |phi|", ", n = variables, |Φ| = weighted formula size");
    public MathematicalFunction spaceComplexity { get; } = new("n", ", n = variables");
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;

    // --- Methods Including Constructors ---
    public WeightedMaxSatBruteForceSolver() { }

    public string solve(WEIGHTEDMAXSAT problem) {
        var result = Optimize(problem, () => timerHasExpired);
        return timerHasExpired ? "timeout" : FormatCertificate(problem.variables, result.Assignment, result.Value);
    }

    internal static string FormatCertificate(
        IReadOnlyList<string> variables, IReadOnlyDictionary<string, bool> assignment, long value) =>
        "((" + string.Join(",", variables.Select(v => $"{v}:{assignment[v]}")) + $"),{value})";

    internal static (Dictionary<string, bool> Assignment, long Value) Optimize(
        WEIGHTEDMAXSAT problem, Func<bool>? expired = null) {
        WeightedMaxSatExactSearchLimit.ThrowIfExceeded(problem.variables.Count);
        var assignment = new Dictionary<string, bool>(StringComparer.Ordinal);
        var best = new Dictionary<string, bool>(StringComparer.Ordinal);
        long bestValue = -1;

        void Search(int index) {
            if (expired?.Invoke() == true) return;
            if (index == problem.variables.Count) {
                long value = problem.Value(assignment);
                if (value > bestValue) {
                    bestValue = value;
                    best = new Dictionary<string, bool>(assignment, StringComparer.Ordinal);
                }
                return;
            }

            string variable = problem.variables[index];
            assignment[variable] = false;
            Search(index + 1);
            assignment[variable] = true;
            Search(index + 1);
            assignment.Remove(variable);
        }

        Search(0);
        return (best, bestValue);
    }
}
