using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Solvers;

class ConstraintSatisfactionIntelligent : ISolver<CONSTRAINTSATISFACTION> {
    public virtual string solverName => "Constraint Satisfaction Forward Checking";
    public string solverDefinition => "Uses minimum remaining values and forward checking to detect domain wipeouts before descending.";
    public string source => "Finite-domain backtracking with minimum remaining values and forward checking.";
    public virtual string sourceFile { get; } = SourceFile.Path();
    public string[] contributors => Array.Empty<string>();
    public SolverType solverType => SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket => SolverComplexityBucket.Exponential;
    public MathematicalFunction timeComplexity => new("(d+1)^n * (L+1)^4", ", conservative bound; n = variables, d = maximum domain size, L = instance length");
    private volatile bool expired;
    public bool timerHasExpired { get => expired; set => expired = value; }
    public string solve(CONSTRAINTSATISFACTION problem) => ConstraintSatisfactionSearch.Solve(problem, true, () => timerHasExpired);
}
