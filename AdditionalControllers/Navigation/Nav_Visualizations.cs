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

    // The wire values Redux_GUI has a renderer for (or, for Unimplemented, an explicit "not renderable" state): the
    // vocabulary from before the visualization redesign. Documentation/visualization-types.json mirrors this list, since
    // Redux_GUI's manifest-drift job compares its own copy against that file. A member added since (a picture type served
    // only under format=frames) is left out until Redux_GUI can draw it, and then added here and to the manifest together.
    internal static readonly IReadOnlySet<string> GuiWireValues = new HashSet<string>(StringComparer.Ordinal) {
        nameof(VisualizationType.Unimplemented), nameof(VisualizationType.GraphD3), nameof(VisualizationType.GraphLaTeX),
        nameof(VisualizationType.SetD3), nameof(VisualizationType.BooleanSatisfiability), nameof(VisualizationType.QuantumCircuitD3),
        nameof(VisualizationType.QuantumCircuitQjs), nameof(VisualizationType.PumpSchedule), nameof(VisualizationType.DynamicTable),
    };

    // Sorted wire values a client that has not opted in (includePictures=true) is shown: GuiWireValues.
    internal static string[] Gui() => All().Where(GuiWireValues.Contains).ToArray();

    // className -> declared visualizationType's wire value. Built by instantiating every
    // reflected visualizer inside a per-type try/catch, exactly like Nav_Batch.cs's
    // InfoJson: one visualizer with a throwing/missing default constructor must not take
    // down navigation at startup. Deliberately Lazy<>, never a static readonly field
    // initializer, for the same reason.
    internal static readonly Lazy<Dictionary<string, string>> ByClassName = new(Build);

    // className -> the wire value a client that has not opted in is told (GuiView.LegacyType), for the visualizations that
    // declare a LegacyVisualizationTypeAttribute; every other class reports its ByClassName value to every client.
    internal static readonly Lazy<Dictionary<string, string>> LegacyByClassName = new(() =>
        ProblemProvider.Visualizers.Values
            .Select(t => (t.Name, Legacy: GuiView.LegacyType(t)))
            .Where(p => p.Legacy != null)
            .ToDictionary(p => p.Name, p => p.Legacy!.Value.ToString(), StringComparer.OrdinalIgnoreCase));

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

    // Class names of the opt-in visualizations (IOptInVisualization), which a listing shows only on request.
    internal static readonly HashSet<string> OptInClassNames = ProblemProvider.Visualizers.Values
        .Where(OptInVisualizations.IsOptIn).Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether a listing shows the visualization class <paramref name="className"/> (see <see cref="IOptInVisualization"/>).</summary>
    internal static bool Listed(string className, bool includePictures) => includePictures || !OptInClassNames.Contains(className);

    /// <summary>The entries a listing shows: all of them when the client opts in, else all but the opt-in ones.</summary>
    internal static List<NavigationEntry> ListedEntries(bool includePictures) =>
        Entries.Where(e => Listed(e.className, includePictures)).ToList();

    internal static List<string> FindWithoutExtension(string? problemName, string? problemTypePrefix, bool includePictures = false) =>
        InterfaceNavigationData.FindWithoutExtension(ListedEntries(includePictures), problemName, problemTypePrefix);
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
    ///<param name="includePictures">True to also list the visualizations only picture-type clients can draw (IOptInVisualization: the Formula and Nested Set views). Default false: the list Redux_GUI reads.</param>
    ///<response code="200">Returns string array of Visualizations</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public String getDefault([FromQuery] string chosenProblem, [FromQuery] string? problemType = null, [FromQuery] bool includePictures = false) {
        var options = new JsonSerializerOptions { WriteIndented = true };

        // Returns an empty array when the problem has no visualizations (preserved
        // not-found contract; this endpoint never returned an error string).
        List<string> subFilesList = VisualizationNavigationData.FindWithoutExtension(chosenProblem, problemType, includePictures);
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

    ///<summary>Returns the VisualizationType wire values, sorted. By default the closed vocabulary Redux_GUI's renderer registry must cover; with includePictures=true every member, the picture types included.</summary>
    ///<param name="includePictures">True to list every member, the picture types served under format=frames included. Default false: the list Redux_GUI reads.</param>
    ///<response code="200">Returns a sorted string array of VisualizationType member names</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public IActionResult Get([FromQuery] bool includePictures = false) {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return Content(JsonSerializer.Serialize(includePictures ? VisualizationTypeCatalog.All() : VisualizationTypeCatalog.Gui(), options), "application/json");
    }
}
