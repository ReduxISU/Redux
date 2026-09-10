using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using API.Interfaces;

// Machine-checked vocabulary for VisualizationType: the enum's sorted wire values, and
// a className -> wire-value map used by the singular and batch "what type is this
// visualization" endpoints below. See Documentation/visualization-types.json for the
// committed manifest that redux-tests/Metadata pins this catalog against.
internal static class VisualizationTypeCatalog {
    // Sorted wire values of every VisualizationType member (member names are the wire
    // values themselves — see VisualizationType.cs).
    internal static string[] All() =>
        Enum.GetNames(typeof(VisualizationType)).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    // className -> declared visualizationType's wire value. Built by instantiating every
    // reflected visualizer inside a per-type try/catch, exactly like Nav_Batch.cs's
    // InfoJson: one visualizer with a throwing/missing default constructor must not take
    // down navigation at startup. Deliberately Lazy<>, never a static readonly field
    // initializer, for the same reason.
    internal static readonly Lazy<Dictionary<string, string>> ByClassName = new(Build);

    private static Dictionary<string, string> Build() {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, type) in ProblemProvider.Visualizers) {
            try {
                if (Activator.CreateInstance(type) is IVisualization instance)
                    result[type.Name] = instance.visualizationType.ToString();
            } catch {
                // Skip a visualizer that can't be default-constructed instead of
                // failing the whole catalog.
            }
        }
        return result;
    }
}

internal static class VisualizationNavigationData {
    // Build once from reflected visualization types so navigation doesn't depend on
    // on-disk source layout. Mirrors VerifierNavigationData (see Nav_Verifiers.cs).
    internal static readonly List<NavigationEntry> Entries =
        InterfaceNavigationData.Build(ProblemProvider.Visualizers, typeof(IVisualization<>));

    internal static List<string> FindWithoutExtension(string? problemName, string? problemTypePrefix) =>
        InterfaceNavigationData.FindWithoutExtension(Entries, problemName, problemTypePrefix);
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
    public String getDefault([FromQuery] string chosenProblem, [FromQuery] string? problemType = null) {
        var options = new JsonSerializerOptions { WriteIndented = true };

        // Returns an empty array when the problem has no visualizations (preserved
        // not-found contract; this endpoint never returned an error string).
        List<string> subFilesList = VisualizationNavigationData.FindWithoutExtension(chosenProblem, problemType);
        return JsonSerializer.Serialize(subFilesList, options);
    }
}

// Get the closed vocabulary of VisualizationType wire values.
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Visualizations)")]
#pragma warning disable CS1591

public class VisualizationTypesController : ControllerBase {
#pragma warning restore CS1591

    ///<summary>Returns every VisualizationType wire value, sorted. This is the closed vocabulary the GUI's renderer registry must cover.</summary>
    ///<response code="200">Returns a sorted string array of VisualizationType member names</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public IActionResult Get() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return Content(JsonSerializer.Serialize(VisualizationTypeCatalog.All(), options), "application/json");
    }
}
