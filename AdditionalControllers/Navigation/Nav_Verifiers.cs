using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using API.Interfaces;

internal static class VerifierNavigationData
{
    internal class VerifierEntry
    {
        public string className { get; set; } = "";
        public string problemName { get; set; } = "";
    }

    // Build once from reflected verifier types so navigation doesn't depend on
    // on-disk source layout.
    internal static readonly List<VerifierEntry> Entries = Build();

    internal static List<string> FindWithoutExtension(string? problemName, string? problemTypePrefix)
    {
        return Find(problemName, problemTypePrefix)
            .Select(x => x.className)
            .ToList();
    }

    internal static bool TryParseProblemKey(string chosenProblem, out string? problemTypePrefix, out string? problemName)
    {
        problemTypePrefix = null;
        problemName = null;
        if (string.IsNullOrWhiteSpace(chosenProblem)) return false;

        string trimmed = chosenProblem.Trim();
        int idx = trimmed.IndexOf('_');
        if (idx <= 0 || idx >= trimmed.Length - 1)
        {
            problemName = trimmed;
            return true;
        }

        problemTypePrefix = trimmed[..idx];
        problemName = trimmed[(idx + 1)..];
        return true;
    }

    private static List<VerifierEntry> Build()
    {
        var entries = new List<VerifierEntry>();
        foreach (var (_, verifierType) in ProblemProvider.Verifiers)
        {
            var generic = verifierType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IVerifier<>));
            if (generic == null) continue;

            Type problemType = generic.GetGenericArguments()[0];
            string className = verifierType.Name;
            entries.Add(new VerifierEntry
            {
                className = className,
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
    // the verifier set. (Solver/visualization nav still use the prefix; handled separately.)
    private static List<VerifierEntry> Find(string? problemName, string? problemTypePrefix)
    {
        IEnumerable<VerifierEntry> query = Entries;

        if (!string.IsNullOrWhiteSpace(problemName))
        {
            query = query.Where(e => string.Equals(e.problemName, problemName, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderBy(e => e.className, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

// Get all Verifiers for a specific problem\
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Verifiers)")]
#pragma warning disable CS1591

public class Problem_VerifiersRefactorController : ControllerBase {
#pragma warning restore CS1591
    
///<summary>Returns all verifiers available for a given problem </summary>
///<param name="chosenProblem" example="SAT3">Problem name</param>
///<param name="problemType" example="NPC">Problem type</param>
///<response code="200">Returns string array of verifiers</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public String getDefault([FromQuery]string chosenProblem,[FromQuery]string problemType) {
        string NOT_FOUND_ERR_VERIFIER = "entered a verifier that does not exist";
        string jsonString = "";
        var options = new JsonSerializerOptions { WriteIndented = true };

        List<string> subFilesList = VerifierNavigationData.FindWithoutExtension(chosenProblem, problemType);
        jsonString = subFilesList.Count > 0
            ? JsonSerializer.Serialize(subFilesList, options)
            : JsonSerializer.Serialize(NOT_FOUND_ERR_VERIFIER, options);

        return jsonString;
    }
}