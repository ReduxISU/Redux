namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Solvers;

// Retain the existing default solver's API identifier and problem/visualization references.
class ConstraintSatisfactionSolver : ConstraintSatisfactionIntelligent {
    public override string solverName => "Constraint Satisfaction Default Forward Checking";
    public override string sourceFile { get; } = API.Interfaces.SourceFile.Path();
}
