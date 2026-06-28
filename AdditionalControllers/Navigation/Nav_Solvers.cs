using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using API.Interfaces;

internal static class SolverNavigationData
{
    internal class SolverEntry
    {
        public string className { get; set; } = "";
        public string problemName { get; set; } = "";
    }

    // Build once from reflected solver types so navigation doesn't depend on
    // on-disk source layout. Mirrors VerifierNavigationData (see Nav_Verifiers.cs).
    internal static readonly List<SolverEntry> Entries = Build();

    internal static List<string> FindWithoutExtension(string? problemName, string? problemTypePrefix)
    {
        return Find(problemName, problemTypePrefix)
            .Select(x => x.className)
            .ToList();
    }

    // Every distinct solver class name, for the "all solvers" view.
    internal static List<string> AllClassNames()
    {
        return Entries
            .Select(e => e.className)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<SolverEntry> Build()
    {
        var entries = new List<SolverEntry>();
        foreach (var (_, solverType) in ProblemProvider.Solvers)
        {
            var generic = solverType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>));
            if (generic == null) continue;

            Type problemType = generic.GetGenericArguments()[0];
            entries.Add(new SolverEntry
            {
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
    private static List<SolverEntry> Find(string? problemName, string? problemTypePrefix)
    {
        IEnumerable<SolverEntry> query = Entries;

        if (!string.IsNullOrWhiteSpace(problemName))
        {
            query = query.Where(e => string.Equals(e.problemName, problemName, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderBy(e => e.className, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

// Get all Solvers regardless of complexity class
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Solvers)")]
#pragma warning disable CS1591

public class All_SolversController : ControllerBase {
//Note: CALEB - should probably be removed with api refactor

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet]
    public String getDefault() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(SolverNavigationData.AllClassNames(), options);
    }
}
#pragma warning restore CS1591


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
    public String getDefault([FromQuery]string chosenProblem, [FromQuery]string? problemType = null) {
        var options = new JsonSerializerOptions { WriteIndented = true };

        List<string> subFilesList = SolverNavigationData.FindWithoutExtension(chosenProblem, problemType);
        return subFilesList.Count > 0
            ? JsonSerializer.Serialize(subFilesList, options)
            : JsonSerializer.Serialize(NOT_FOUND_ERR_SOLVER, options);
    }
}
