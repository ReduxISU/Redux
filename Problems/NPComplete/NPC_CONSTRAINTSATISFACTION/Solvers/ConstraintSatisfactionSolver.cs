using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Solvers;
class ConstraintSatisfactionSolver : ISolver<CONSTRAINTSATISFACTION> {

    // --- Fields ---
    public string solverName {get;} = "Constraint Satisfaction Solver";
    public string solverDefinition {get;} = "TODO";
    public string source {get;} = "TODO";
    public string[] contributors {get;} = { "TODO" };
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
    public ConstraintSatisfactionSolver() {}

    public string solve(CONSTRAINTSATISFACTION problem){
        // TODO: implement constraint satisfaction solver for CONSTRAINTSATISFACTION

        // make sure to check timerHasExpired occasionally, and return if it has
        if (timerHasExpired)
            return "timeout";

        return "{}";
    }
}
