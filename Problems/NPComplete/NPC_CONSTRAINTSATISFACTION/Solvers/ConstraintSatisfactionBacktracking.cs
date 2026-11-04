using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Solvers;

class ConstraintSatisfactionBacktracking : ISolver<CONSTRAINTSATISFACTION> {
    public string solverName => "Constraint Satisfaction Backtracking";
    public string solverDefinition => "Assigns variables in input order, pruning whenever a fully assigned constraint fails.";
    public string source => "Depth-first finite-domain constraint search.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors => Array.Empty<string>();
    public SolverType solverType => SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket => SolverComplexityBucket.Exponential;
    public MathematicalFunction timeComplexity => new("(d+1)^n * (L+1)^4", ", conservative bound; n = variables, d = maximum domain size, L = instance length");
    private volatile bool expired;
    public bool timerHasExpired { get => expired; set => expired = value; }
    public string solve(CONSTRAINTSATISFACTION problem) => ConstraintSatisfactionSearch.Solve(problem, false, () => timerHasExpired);
}
