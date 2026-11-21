using API.Interfaces;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Solvers;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAX3SAT.Solvers;

class WeightedMax3SatConditionalExpectationsSolver : ISolver<WEIGHTEDMAX3SAT> {
    // --- Fields ---
    public string solverName { get; } = "Weighted Max-3SAT Conditional Expectations Approximation";
    public string solverDefinition { get; } = "Deterministically fixes variables to preserve the larger conditional expected satisfied weight. For nonnegative weights and clauses with 1–3 literal occurrences, the result is at least OPT/2; it does not claim optimality.";
    public string source { get; } = "Method of conditional expectations applied to the random-assignment 1/2-approximation for Max-SAT.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Approximation;
    public MathematicalFunction timeComplexity { get; } = new("n * |phi|", ", n = variables, |Φ| = weighted formula size; exact integer arithmetic");
    public MathematicalFunction spaceComplexity { get; } = new("n + |phi|", ", assignment and conditional-expectation bookkeeping");
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;

    // --- Methods Including Constructors ---
    public WeightedMax3SatConditionalExpectationsSolver() { }

    public string solve(WEIGHTEDMAX3SAT problem) {
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        var result = WeightedMaxSatConditionalExpectationsSolver.Optimize(
            problem.weightedMaxSat, () => timerHasExpired);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        return WeightedMaxSatBruteForceSolver.FormatCertificate(
            problem.weightedMaxSat.variables, result.Assignment, result.Value);
    }
}
