using API.Interfaces;

namespace API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Solvers;

class UnstructuredSearchSolver : ISolver<UNSTRUCTUREDSEARCH> {

    // --- Fields ---
    public string solverName { get; } = "Clasical unstructured search solver";
    public string solverDefinition { get; } = "This solver simply loops through all possible x until one f(x) = 1.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Jason L. Wright", "Alex Svancara" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration. Natural parameter is oracle-bit-count n
    // (matches the paired UnstructuredGroverSolver's parameter), not the 2^n-length array scanned --
    // classically needs exponentially many oracle queries in n, the entire pedagogical point of this
    // problem (classical search vs. Grover's quadratic quantum speedup).
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // solve() linearly scans problem.funcValues (length 2^n) until it finds a 1, worst
    // case (or "no solution") requiring all 2^n oracle queries.
    public string complexity { get; } = "O(2^n) queries";

    // --- Methods Including Constructors ---
    public UnstructuredSearchSolver() { }

    public string solve(UNSTRUCTUREDSEARCH problem) {
        // For a classical solution, we just need to loop over the values and
        // find the first non-zero value.
        for (int i = 0; i < problem.funcValues.Count; i++) {
            if (problem.funcValues[i] != 0)
                return Convert.ToString(i);
        }
        return "no solution";
    }
}
