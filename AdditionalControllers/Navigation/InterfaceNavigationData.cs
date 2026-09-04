// Shared {className, problemName} entry type and Build/Find logic for the per-kind
// navigation catalogs in Nav_Solvers.cs, Nav_Verifiers.cs, and Nav_Visualizations.cs. Each
// catalog scans a ProblemProvider type dictionary (Solvers/Verifiers/Visualizers) for classes
// implementing a single-type-parameter open generic marker interface (ISolver<>, IVerifier<>,
// IVisualization<>) and records which problem type each one was built for.
internal class NavigationEntry {
    public string className { get; set; } = "";
    public string problemName { get; set; } = "";
}

internal static class InterfaceNavigationData {
    // Scans `source` (e.g. ProblemProvider.Solvers) for types implementing the given
    // single-type-parameter open generic interface (e.g. typeof(ISolver<>)), recording each
    // type's className and the problemName of its generic argument. Types with no matching
    // interface are skipped; duplicate class names (case-insensitive) keep the first entry
    // encountered.
    internal static List<NavigationEntry> Build(Dictionary<string, Type> source, Type openGenericInterface) {
        var entries = new List<NavigationEntry>();
        foreach (var (_, type) in source) {
            var generic = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGenericInterface);
            if (generic == null) continue;

            Type problemType = generic.GetGenericArguments()[0];
            entries.Add(new NavigationEntry {
                className = type.Name,
                problemName = problemType.Name,
            });
        }

        return entries
            .GroupBy(e => e.className, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    // problemTypePrefix is accepted for API compatibility but intentionally ignored by every
    // caller: problem names are unique across complexity classes, so the name alone
    // identifies the entry set. The GUI pins problemType to "NPC", so matching on the prefix
    // would drop P / NP-Hard problems.
    internal static List<NavigationEntry> Find(List<NavigationEntry> entries, string? problemName, string? problemTypePrefix) {
        IEnumerable<NavigationEntry> query = entries;

        if (!string.IsNullOrWhiteSpace(problemName)) {
            query = query.Where(e => string.Equals(e.problemName, problemName, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderBy(e => e.className, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static List<string> FindWithoutExtension(List<NavigationEntry> entries, string? problemName, string? problemTypePrefix) {
        return Find(entries, problemName, problemTypePrefix)
            .Select(x => x.className)
            .ToList();
    }
}
