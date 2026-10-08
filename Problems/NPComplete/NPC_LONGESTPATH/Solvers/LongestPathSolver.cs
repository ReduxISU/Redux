using API.Interfaces;

namespace API.Problems.NPComplete.NPC_LONGESTPATH.Solvers;

class LongestPathSolver : ISolver<LONGESTPATH> {

    // --- Fields ---
    public string solverName { get; } = "Longest Path Solver";
    public string solverDefinition { get; } = "TODO";
    public string source { get; } = "TODO";
    public string[] contributors { get; } = { "TODO" };
    // Declared, not derived: read the algorithm below and decide; don't guess from the
    // solver's name. Getting these wrong is a public correctness claim the API serves to callers.
    // TODO: replace Unclassified. A test in redux-tests/Metadata fails until you do, on purpose. See Interfaces/SolverType.cs for what each value means.
    public SolverType solverType { get; } = SolverType.Unclassified;
    // TODO: replace Unclassified. A test in redux-tests/Metadata fails until you do, on purpose. See Interfaces/SolverComplexityBucket.cs for what each value means.
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Unclassified;
    // TODO: replace "" with a confidently-known Big-O string such as "O(n log n)", never a guess. A test in redux-tests/Metadata fails until you do, on purpose.
    public string complexity { get; } = "";
    public bool timerHasExpired { get; set; }

    // --- Methods Including Constructors ---
    public LongestPathSolver() { }

    public string solve(LONGESTPATH problem) {
        // TODO: implement Longest Path Solver for Longest Path

        // make sure to check timerHasExpired occasionally, and return if it has
        if (timerHasExpired)
            return "timeout";

        // An unfinished solver must fail loudly, not silently claim "no solution".
        throw new NotImplementedException("TODO: implement Longest Path Solver for Longest Path");
    }
}
