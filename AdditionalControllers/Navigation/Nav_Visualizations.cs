using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using API.Interfaces;

internal static class VisualizationNavigationData
{
    internal class VisualizationEntry
    {
        public string className { get; set; } = "";
        public string problemName { get; set; } = "";
    }

    // Build once from reflected visualization types so navigation doesn't depend on
    // on-disk source layout. Mirrors VerifierNavigationData (see Nav_Verifiers.cs).
    internal static readonly List<VisualizationEntry> Entries = Build();

    internal static List<string> FindWithoutExtension(string? problemName, string? problemTypePrefix)
    {
        return Find(problemName, problemTypePrefix)
            .Select(x => x.className)
            .ToList();
    }

    private static List<VisualizationEntry> Build()
    {
        var entries = new List<VisualizationEntry>();
        foreach (var (_, visType) in ProblemProvider.Visualizers)
        {
            var generic = visType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IVisualization<>));
            if (generic == null) continue;

            Type problemType = generic.GetGenericArguments()[0];
            entries.Add(new VisualizationEntry
            {
                className = visType.Name,
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
    // the visualization set. Mirrors the verifier navigation (#317/#318): the GUI pins
    // problemType to "NPC", so matching on the prefix would drop P / NP-Hard problems.
    private static List<VisualizationEntry> Find(string? problemName, string? problemTypePrefix)
    {
        IEnumerable<VisualizationEntry> query = Entries;

        if (!string.IsNullOrWhiteSpace(problemName))
        {
            query = query.Where(e => string.Equals(e.problemName, problemName, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderBy(e => e.className, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

// Get all Visualizations for a specific problem\
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Visualizations)")]
#pragma warning disable CS1591

public class Problem_VisualizationsRefactorController : ControllerBase {
#pragma warning restore CS1591

///<summary>Returns all Visualizations available for a given problem </summary>
///<param name="chosenProblem" example="SAT3">Problem name</param>
///<param name="problemType" example="NPC">Problem type (optional; ignored — problem names are unique across complexity classes)</param>
///<response code="200">Returns string array of Visualizations</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public String getDefault([FromQuery]string chosenProblem, [FromQuery]string? problemType = null) {
        var options = new JsonSerializerOptions { WriteIndented = true };

        // Returns an empty array when the problem has no visualizations (preserved
        // not-found contract; this endpoint never returned an error string).
        List<string> subFilesList = VisualizationNavigationData.FindWithoutExtension(chosenProblem, problemType);
        return JsonSerializer.Serialize(subFilesList, options);
    }
}
