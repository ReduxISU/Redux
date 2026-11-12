namespace API.Interfaces;

internal static partial class ProblemDefaults {
    private static partial IEnumerable<Type> GetDefaultTypes() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes());
}