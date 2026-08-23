using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;

// Batch navigation endpoints: return metadata for ALL problems in a single
// call so the GUI can prime its state with one request instead of N per-problem
// round-trips.
//
// Everything here is projected from the same reflected data the singular
// Navigation controllers already use — ProblemProvider.* and the *NavigationData
// caches built once at startup (see Nav_Solvers/Nav_Verifiers/Nav_Visualizations).
// Because the source of truth is shared, a batch response can never drift from its
// per-problem counterpart.
//
// This deliberately does NOT scan the on-disk Problems/ layout, take a problemType
// argument, or register an IMemoryCache the way the original proposal (upstream
// PR #168) did: the reflected dictionaries are already a process-lifetime in-memory
// cache, so each payload is serialized exactly once via Lazy<string>. Problem names
// are unique across complexity classes, so no class filter is required.
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Batch)")]
#pragma warning disable CS1591
public class BatchController : ControllerBase {
#pragma warning restore CS1591

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    // Mirrors ProblemProvider's reflected-object serialization: IncludeFields so public
    // fields serialize (System.Text.Json omits them by default, whereas the previous
    // Newtonsoft path serialized them), plus cycle handling to replace the old
    // ReferenceLoopHandling.Ignore now that arbitrary reflected instances are serialized.
    private static readonly JsonSerializerOptions ReflectedIndented = new() {
        WriteIndented = true,
        IncludeFields = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    // All top-level problem names, from the same reflected source as the singular
    // Navigation/*Problems* endpoints (see ProblemNavigationData in Nav_Problems.cs).
    private static readonly Lazy<string> ProblemsJson = new(() =>
        JsonSerializer.Serialize(ProblemNavigationData.All(), Indented));

    private static readonly Lazy<string> SolversJson = new(() =>
        GroupedJson(SolverNavigationData.Entries.Select(e => (e.problemName, e.className))));

    private static readonly Lazy<string> VerifiersJson = new(() =>
        GroupedJson(VerifierNavigationData.Entries.Select(e => (e.problemName, e.className))));

    private static readonly Lazy<string> VisualizationsJson = new(() =>
        GroupedJson(VisualizationNavigationData.Entries.Select(e => (e.problemName, e.className))));

    // className -> declared VisualizationType wire value, for every reflected
    // visualizer. Backed by VisualizationTypeCatalog.ByClassName (see
    // Nav_Visualizations.cs), which already does the per-type try/catch instantiation.
    // Exists so the GUI can resolve renderability without walking allInfo, which would
    // instantiate and Newtonsoft-serialize every problem/solver/verifier/visualization/
    // reduction just to read one string per visualization.
    private static readonly Lazy<string> VisualizationTypesJson = new(() =>
        JsonSerializer.Serialize(VisualizationTypeCatalog.ByClassName.Value, Indented));

    // Instances of every reflected interface type, mirroring ProblemProvider.info
    // (Newtonsoft + reference-loop ignore) but for all interfaces at once. Types that
    // cannot be default-constructed are skipped rather than failing the whole payload.
    private static readonly Lazy<string> InfoJson = new(() => {
        var result = new Dictionary<string, object>();
        foreach (var (_, type) in ProblemProvider.Interfaces) {
            try {
                if (Activator.CreateInstance(type) is object instance)
                    result[type.Name] = instance;
            } catch {
                // Skip an interface that has no parameterless constructor instead of
                // failing the whole response.
            }
        }
        return JsonSerializer.Serialize(result, ReflectedIndented);
    });

    // Group (problemName -> className) pairs into a sorted map of sorted class names.
    private static string GroupedJson(IEnumerable<(string problemName, string className)> entries) =>
        JsonSerializer.Serialize(
            entries
                .GroupBy(e => e.problemName, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.className)
                          .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                          .ToList(),
                    StringComparer.OrdinalIgnoreCase),
            Indented);

    /// <summary>Returns every problem name in a single call.</summary>
    /// <response code="200">String array of all problem names.</response>
    [ProducesResponseType(typeof(List<string>), 200)]
    [HttpGet("allProblems")]
    public IActionResult AllProblems() => Content(ProblemsJson.Value, "application/json");

    /// <summary>Returns the solvers for every problem, keyed by problem name.</summary>
    /// <response code="200">Map of problem name to its solver class names.</response>
    [ProducesResponseType(typeof(Dictionary<string, List<string>>), 200)]
    [HttpGet("allSolvers")]
    public IActionResult AllSolvers() => Content(SolversJson.Value, "application/json");

    /// <summary>Returns the verifiers for every problem, keyed by problem name.</summary>
    /// <response code="200">Map of problem name to its verifier class names.</response>
    [ProducesResponseType(typeof(Dictionary<string, List<string>>), 200)]
    [HttpGet("allVerifiers")]
    public IActionResult AllVerifiers() => Content(VerifiersJson.Value, "application/json");

    /// <summary>Returns the visualizations for every problem, keyed by problem name.</summary>
    /// <response code="200">Map of problem name to its visualization class names.</response>
    [ProducesResponseType(typeof(Dictionary<string, List<string>>), 200)]
    [HttpGet("allVisualizations")]
    public IActionResult AllVisualizations() => Content(VisualizationsJson.Value, "application/json");

    /// <summary>Returns info objects for every reflected interface, keyed by class name.</summary>
    /// <response code="200">Map of interface class name to its instance info.</response>
    [ProducesResponseType(typeof(Dictionary<string, object>), 200)]
    [HttpGet("allInfo")]
    public IActionResult AllInfo() => Content(InfoJson.Value, "application/json");

    /// <summary>Returns the declared VisualizationType wire value for every visualizer, keyed by class name.</summary>
    /// <response code="200">Map of visualization class name to its VisualizationType wire value.</response>
    [ProducesResponseType(typeof(Dictionary<string, string>), 200)]
    [HttpGet("allVisualizationTypes")]
    public IActionResult AllVisualizationTypes() => Content(VisualizationTypesJson.Value, "application/json");
}
