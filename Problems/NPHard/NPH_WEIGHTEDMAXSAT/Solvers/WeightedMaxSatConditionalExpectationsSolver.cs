using API.Interfaces;
using System.Numerics;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Solvers;

class WeightedMaxSatConditionalExpectationsSolver : ISolver<WEIGHTEDMAXSAT> {
    // --- Fields ---
    public string solverName { get; } = "Weighted Max-SAT Conditional Expectations Approximation";
    public string solverDefinition { get; } = "Deterministically fixes variables to preserve the larger conditional expected satisfied weight. For nonnegative weights and nonempty clauses, the result is at least OPT/2; it does not claim optimality.";
    public string source { get; } = "Method of conditional expectations applied to the random-assignment 1/2-approximation for Max-SAT.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Approximation;
    public MathematicalFunction timeComplexity { get; } = new("n * |phi|", ", n = variables, |Φ| = weighted formula size; exact integer arithmetic");
    public MathematicalFunction spaceComplexity { get; } = new("n + |phi|", ", assignment and conditional-expectation bookkeeping");
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;

    // --- Methods Including Constructors ---
    public WeightedMaxSatConditionalExpectationsSolver() { }

    public string solve(WEIGHTEDMAXSAT problem) {
        if (timerHasExpired) return "timeout";
        var result = Optimize(problem, () => timerHasExpired);
        if (timerHasExpired) return "timeout";
        return WeightedMaxSatBruteForceSolver.FormatCertificate(problem.variables, result.Assignment, result.Value);
    }

    internal static (Dictionary<string, bool> Assignment, long Value) Optimize(
        WEIGHTEDMAXSAT problem, Func<bool>? expired = null) {
        var assignment = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (string variable in problem.variables) {
            if (expired?.Invoke() == true) return (assignment, 0);
            assignment[variable] = false;
            BigInteger falseExpectation = ScaledConditionalExpectation(problem, assignment, expired, out bool stopped);
            if (stopped) return (assignment, 0);
            assignment[variable] = true;
            BigInteger trueExpectation = ScaledConditionalExpectation(problem, assignment, expired, out stopped);
            if (stopped) return (assignment, 0);
            assignment[variable] = trueExpectation >= falseExpectation;
        }
        return (assignment, problem.Value(assignment));
    }

    private static BigInteger ScaledConditionalExpectation(
        WEIGHTEDMAXSAT problem, IReadOnlyDictionary<string, bool> assignment,
        Func<bool>? expired, out bool stopped) {
        int unassignedCount = problem.variables.Count - assignment.Count;
        BigInteger denominator = BigInteger.One << unassignedCount;
        BigInteger total = BigInteger.Zero;
        stopped = false;

        foreach (WeightedCnfClause weighted in problem.weightedClauses) {
            if (expired?.Invoke() == true) {
                stopped = true;
                return BigInteger.Zero;
            }
            bool satisfied = false;
            var requiredFalseValues = new Dictionary<string, bool>(StringComparer.Ordinal);
            bool tautology = false;
            foreach (var literal in weighted.Clause.Literals) {
                if (expired?.Invoke() == true) {
                    stopped = true;
                    return BigInteger.Zero;
                }
                if (assignment.TryGetValue(literal.Variable, out bool value)) {
                    if (value != literal.Negated) satisfied = true;
                } else if (requiredFalseValues.TryGetValue(literal.Variable, out bool previous)
                    && previous != literal.Negated) {
                    tautology = true;
                } else {
                    requiredFalseValues[literal.Variable] = literal.Negated;
                }
            }

            // With k distinct undecided variables, an unsatisfied non-tautological clause
            // remains false with probability 2^-k. Scaling every expectation by 2^U keeps
            // comparisons exact. Preserving conditional expectation yields at least OPT/2.
            BigInteger satisfiedNumerator = satisfied || tautology
                ? denominator
                : denominator - (BigInteger.One << (unassignedCount - requiredFalseValues.Count));
            total += weighted.Weight * satisfiedNumerator;
        }
        return total;
    }
}
