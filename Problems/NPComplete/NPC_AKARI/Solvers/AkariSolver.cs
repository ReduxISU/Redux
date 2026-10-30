using API.Interfaces;

namespace API.Problems.NPComplete.NPC_AKARI.Solvers;

class AkariSolver : ISolver<AKARI> {

    // --- Fields ---
    public string solverName { get; } = "Akari Solver";
    public string solverDefinition { get; } = "TODO";
    public string source { get; } = "TODO";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "TODO" };
    // Declared, not derived — see API.Interfaces.SolverType and
    // API.Interfaces.SolverComplexityBucket. Leave these commented out (the
    // interface defaults to Unclassified) until you've actually read the
    // algorithm below and are confident which values fit — don't guess from
    // the solver's name. Getting this wrong is a public correctness claim
    // the API will serve to callers.
    // public SolverType solverType {get;} = SolverType.TODO;
    // public SolverComplexityBucket complexityBucket {get;} = SolverComplexityBucket.TODO;
    // Only set this to a confidently-known Big-O string, never a guess, e.g. "O(n log n)".
    // public string complexity {get;} = "TODO";
    public bool timerHasExpired { get; set; }

    // --- Methods Including Constructors ---
    public AkariSolver() { }

    public string solve(AKARI problem) {
        // TODO: implement Akari Solver for AKARI

        // To get parsed problem instance, use the problem.grid property.

        // make sure to check timerHasExpired occasionally, and return if it has
        if (timerHasExpired)
            return "timeout";

        return "{}";
    }
}
