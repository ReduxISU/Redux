using API.Interfaces;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Solvers;

class WeightedMaxSatBranchAndBoundSolver : ISolver<WEIGHTEDMAXSAT> {
    // --- Fields ---
    public string solverName { get; } = "Weighted Max-SAT Exact Branch and Bound";
    public string solverDefinition { get; } = "Searches partial assignments and prunes any branch whose still-satisfiable clause weight cannot exceed the incumbent. It is exact and guarantees an optimal solution.";
    public string source { get; } = "Standard branch-and-bound search for weighted maximum satisfiability.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Backtracking;
    public MathematicalFunction timeComplexity { get; } = new("2^n * |phi|", ", worst case; n = variables, |Φ| = weighted formula size");
    public MathematicalFunction spaceComplexity { get; } = new("n", ", recursion depth and assignment storage");
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;

    // --- Methods Including Constructors ---
    public WeightedMaxSatBranchAndBoundSolver() { }

    public string solve(WEIGHTEDMAXSAT problem) {
        var result = Optimize(problem);
        return timerHasExpired ? "timeout" : WeightedMaxSatBruteForceSolver.FormatCertificate(problem.variables, result.Assignment, result.Value);
    }

    private (Dictionary<string, bool> Assignment, long Value) Optimize(WEIGHTEDMAXSAT problem) {
        WeightedMaxSatExactSearchLimit.ThrowIfExceeded(problem.variables.Count);
        var assignment = new Dictionary<string, bool>(StringComparer.Ordinal);
        var best = new Dictionary<string, bool>(StringComparer.Ordinal);
        long bestValue = -1;

        void Search(int index) {
            if (timerHasExpired) return;

            // A clause contributes to the upper bound unless the partial assignment has
            // already made every literal false. The bound is therefore at least the value
            // of every completion, so pruning cannot discard a better solution.
            long upperBound = 0;
            foreach (WeightedCnfClause weighted in problem.weightedClauses) {
                bool satisfied = false;
                bool canStillSatisfy = false;
                foreach (var literal in weighted.Clause.Literals) {
                    if (!assignment.TryGetValue(literal.Variable, out bool value))
                        canStillSatisfy = true;
                    else if (value != literal.Negated)
                        satisfied = true;
                }
                if (satisfied || canStillSatisfy)
                    upperBound += weighted.Weight;
            }
            if (upperBound <= bestValue) return;

            if (index == problem.variables.Count) {
                bestValue = problem.Value(assignment);
                best = new Dictionary<string, bool>(assignment, StringComparer.Ordinal);
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
