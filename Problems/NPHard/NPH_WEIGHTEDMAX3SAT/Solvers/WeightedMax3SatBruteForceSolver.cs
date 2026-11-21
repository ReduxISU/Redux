using API.Interfaces;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Solvers;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAX3SAT.Solvers;

[DefaultFor(typeof(WEIGHTEDMAX3SAT))]
class WeightedMax3SatBruteForceSolver : ISolver<WEIGHTEDMAX3SAT> {
    // --- Fields ---
    public string solverName { get; } = "Weighted Max-3SAT Exact Brute Force";
    public string solverDefinition { get; } = "Enumerates every truth assignment and returns one with maximum satisfied clause weight. This exact baseline guarantees an optimal solution and supports at most 20 variables.";
    public string source { get; } = "Exhaustive search.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.BruteForce;
    public MathematicalFunction timeComplexity { get; } = new("2^n * |phi|", ", n = variables, |Φ| = weighted formula size");
    public MathematicalFunction spaceComplexity { get; } = new("n", ", assignment storage and recursion depth");
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;

    // --- Methods Including Constructors ---
    public WeightedMax3SatBruteForceSolver() { }

    public string solve(WEIGHTEDMAX3SAT problem) {
        var result = WeightedMaxSatBruteForceSolver.Optimize(
            problem.weightedMaxSat, () => timerHasExpired, problem.problemName);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        return WeightedMaxSatBruteForceSolver.FormatCertificate(
            problem.weightedMaxSat.variables, result.Assignment, result.Value);
    }
}
