using API.Interfaces;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Solvers;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAX3SAT.Solvers;

class WeightedMax3SatBranchAndBoundSolver : ISolver<WEIGHTEDMAX3SAT> {
    // --- Fields ---
    public string solverName { get; } = "Weighted Max-3SAT Exact Branch and Bound";
    public string solverDefinition { get; } = "Searches partial assignments and prunes any branch whose still-satisfiable clause weight cannot exceed the incumbent. It guarantees an optimal solution and supports at most 20 variables.";
    public string source { get; } = "Standard branch-and-bound search for weighted maximum satisfiability.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Backtracking;
    public MathematicalFunction timeComplexity { get; } = new("2^n * |phi|", ", n = variables, |Φ| = weighted formula size");
    public MathematicalFunction spaceComplexity { get; } = new("n", ", assignment storage and recursion depth");
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;

    // --- Methods Including Constructors ---
    public WeightedMax3SatBranchAndBoundSolver() { }

    public string solve(WEIGHTEDMAX3SAT problem) {
        var result = WeightedMaxSatBranchAndBoundSolver.Optimize(
            problem.weightedMaxSat, () => timerHasExpired, problem.problemName);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        return WeightedMaxSatBruteForceSolver.FormatCertificate(
            problem.weightedMaxSat.variables, result.Assignment, result.Value);
    }
}
