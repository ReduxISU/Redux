using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Solvers;

// Iterative backtracking with minimum remaining values (MRV) and optional forward
// checking. Uses the same parser, readiness rules, and frame engine as standard search;
// only candidate preparation and variable selection differ.
class ConstraintSatisfactionIntelligent : ISolver<CONSTRAINTSATISFACTION> {
    public virtual string solverName => "Constraint Satisfaction Forward Checking";
    public string solverDefinition => "Uses minimum remaining values with optional forward checking to detect domain wipeouts before descending.";
    public string source => "Finite-domain backtracking with minimum remaining values and forward checking.";
    public virtual string sourceFile { get; } = SourceFile.Path();
    public string[] contributors => Array.Empty<string>();
    public SolverType solverType => SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket => SolverComplexityBucket.Exponential;
    public MathematicalFunction timeComplexity => new("(d+1)^n * (n+1) * (d+1) * (L+1)", ", conservative forward-checking bound; n = variables, d = max(1, maximum domain size), L = constraint text length");
    // Redux controls timer lifecycle; direct callers must manage this flag themselves.
    private volatile bool expired;
    public bool timerHasExpired { get => expired; set => expired = value; }

    // When true, probe unassigned domains and choose the smallest viable list, rejecting
    // domain wipeouts immediately. When false, MRV compares original domain sizes and
    // constraints are checked only after actual assignments. Defaults to enabled.
    public bool ForwardChecking { get; set; } = true;
    // Runs the shared stack engine with MRV and the configured probing policy.
    public string solve(CONSTRAINTSATISFACTION problem) => ConstraintSatisfactionSearch.Solve(problem, ForwardChecking, () => timerHasExpired, useMrv: true);
}
