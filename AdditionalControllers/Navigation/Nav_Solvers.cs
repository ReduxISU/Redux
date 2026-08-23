using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using API.Interfaces;

// className -> declared SolverType/SolverComplexityBucket wire values. Built the same way as
// ComplexityClassCatalog/ReductionCostCatalog (Nav_Problems.cs / Nav_Reductions.cs): iterate
// ProblemProvider.Solvers, Activator.CreateInstance per-type try/catch, read the declared
// property off the constructed ISolver. Deliberately checks `is ISolver` (the non-generic
// interface), NOT `is ISolver<T>` like SolverNavigationData.Build() below does -- that generic
// check silently skips SATBruteForceSolver, which implements non-generic ISolver directly. One
// solver with a throwing/missing default constructor must not take down the whole catalog.
internal static class SolverTypeCatalog {
    internal static readonly Lazy<Dictionary<string, string>> SolverTypeByClassName = new(BuildSolverType);
    internal static readonly Lazy<Dictionary<string, string>> ComplexityBucketByClassName = new(BuildComplexityBucket);
    internal static readonly Lazy<Dictionary<string, string>> ComplexityByClassName = new(BuildComplexity);

    private static Dictionary<string, string> BuildSolverType() {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, type) in ProblemProvider.Solvers) {
            try {
                if (Activator.CreateInstance(type) is ISolver instance)
                    result[type.Name] = instance.solverType.ToString();
            } catch {
                // Skip a solver that can't be default-constructed instead of failing the whole
                // catalog. It falls back to Unclassified at the call site.
            }
        }
        return result;
    }

    private static Dictionary<string, string> BuildComplexityBucket() {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, type) in ProblemProvider.Solvers) {
            try {
                if (Activator.CreateInstance(type) is ISolver instance)
                    result[type.Name] = instance.complexityBucket.ToString();
            } catch {
                // Skip a solver that can't be default-constructed instead of failing the whole
                // catalog. It falls back to Unclassified at the call site.
            }
        }
        return result;
    }

    private static Dictionary<string, string> BuildComplexity() {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, type) in ProblemProvider.Solvers) {
            try {
                if (Activator.CreateInstance(type) is ISolver instance)
                    result[type.Name] = instance.complexity;
            } catch {
                // Skip a solver that can't be default-constructed instead of failing the whole
                // catalog. It falls back to "" (empty) at the call site.
            }
        }
        return result;
    }
}

internal static class SolverNavigationData {
    internal class SolverEntry {
        public string className { get; set; } = "";
        public string problemName { get; set; } = "";
    }

    // Build once from reflected solver types so navigation doesn't depend on
    // on-disk source layout. Mirrors VerifierNavigationData (see Nav_Verifiers.cs).
    internal static readonly List<SolverEntry> Entries = Build();

    internal static List<string> FindWithoutExtension(string? problemName, string? problemTypePrefix) {
        return Find(problemName, problemTypePrefix)
            .Select(x => x.className)
            .ToList();
    }

    private static List<SolverEntry> Build() {
        var entries = new List<SolverEntry>();
        foreach (var (_, solverType) in ProblemProvider.Solvers) {
            var generic = solverType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>));
            if (generic == null) continue;

            Type problemType = generic.GetGenericArguments()[0];
            entries.Add(new SolverEntry {
                className = solverType.Name,
                problemName = problemType.Name,
            });
        }

        return entries
            .GroupBy(e => e.className, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    // problemTypePrefix is accepted for API compatibility but intentionally ignored:
    // problem names are unique across complexity classes, so the name alone identifies
    // the solver set. Mirrors the verifier navigation (#317/#318): the GUI pins
    // problemType to "NPC", so matching on the prefix would drop P / NP-Hard solvers.
    private static List<SolverEntry> Find(string? problemName, string? problemTypePrefix) {
        IEnumerable<SolverEntry> query = Entries;

        if (!string.IsNullOrWhiteSpace(problemName)) {
            query = query.Where(e => string.Equals(e.problemName, problemName, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderBy(e => e.className, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

// Get all Solvers for a specific problem (Refactored)
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Solvers)")]
#pragma warning disable CS1591

public class Problem_SolversRefactorController : ControllerBase {
#pragma warning restore CS1591

    string NOT_FOUND_ERR_SOLVER = "entered a solver that does not exist";

    ///<summary>Returns all solvers available for a given problem </summary>
    ///<param name="chosenProblem" example="SAT3">Problem name</param>
    ///<param name="problemType" example="NPC">Problem type (optional; ignored — problem names are unique across complexity classes)</param>
    ///<response code="200">Returns string array of solvers</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public String getDefault([FromQuery] string chosenProblem, [FromQuery] string? problemType = null) {
        var options = new JsonSerializerOptions { WriteIndented = true };

        List<string> subFilesList = SolverNavigationData.FindWithoutExtension(chosenProblem, problemType);
        return subFilesList.Count > 0
            ? JsonSerializer.Serialize(subFilesList, options)
            : JsonSerializer.Serialize(NOT_FOUND_ERR_SOLVER, options);
    }
}
