namespace API.Interfaces;

internal static partial class ProblemDefaults {
    private static partial IEnumerable<Type> GetDefaultTypes() =>
        ProblemProvider.Solvers.Values
            .Concat(ProblemProvider.Verifiers.Values)
            .Concat(ProblemProvider.Visualizers.Values);
}