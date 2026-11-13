namespace API.Interfaces;

internal static partial class ProblemDefaults {
    private static partial IEnumerable<Type> GetDefaultTypes() =>
        typeof(ProblemDefaults).Assembly.GetTypes();
}