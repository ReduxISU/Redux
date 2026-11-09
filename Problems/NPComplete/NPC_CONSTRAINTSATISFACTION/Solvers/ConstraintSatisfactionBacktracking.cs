using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Solvers;

// Standard input-order backtracking. Delegates stack management to the shared engine
// with both variable reordering and future-domain probing disabled. Ready constraints
// still prune failing partial assignments before a deeper frame is created.
class ConstraintSatisfactionBacktracking : ISolver<CONSTRAINTSATISFACTION> {
    public string solverName => "Constraint Satisfaction Backtracking";
    public string solverDefinition => "Assigns variables in input order, pruning whenever a fully assigned constraint fails.";
    public string source => "Depth-first finite-domain constraint search.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors => Array.Empty<string>();
    public SolverType solverType => SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket => SolverComplexityBucket.Exponential;
    public MathematicalFunction timeComplexity => new("(d+1)^n * (n+L+1)", ", conservative search bound; n = variables, d = max(1, maximum domain size), L = constraint text length");
    // Volatile allows the Redux timing thread to signal cooperative cancellation.
    private volatile bool expired;
    public bool timerHasExpired { get => expired; set => expired = value; }
    /// <summary>Runs iterative depth-first search in the original variable/domain order.</summary>
    public string solve(CONSTRAINTSATISFACTION problem) => ConstraintSatisfactionSearch.Solve(problem, false, () => timerHasExpired);
}
