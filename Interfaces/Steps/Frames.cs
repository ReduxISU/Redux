using System.Text.Json;
using System.Text.Json.Serialization;

namespace API.Interfaces.Steps;

/// <summary>
/// The shared state vocabulary every picture type names its elements with; a frontend maps each name to a
/// style in one table. Serializes as its name. A missing entry in a frame means <see cref="Background"/>.
/// <c>False</c> (assigned and false) is deliberately absent while that state is under review.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PictureState>))]
enum PictureState {
    /// <summary>Nothing has happened to it yet. Frames omit these entries.</summary>
    Background,
    /// <summary>What the solver is looking at or trying right now.</summary>
    ElementHighlight,
    /// <summary>Part of the answer.</summary>
    Solution,
    /// <summary>Failed: uncovered, undominated, or doubled.</summary>
    Rejected,
    /// <summary>Never reached during this run.</summary>
    Untraveled,
    /// <summary>A constraint that is met (an edge or node covered, dominated).</summary>
    Covered,
    /// <summary>Cannot be chosen without breaking an exactly-once rule.</summary>
    Blocked,
}

/// <summary>
/// The body of <c>/ProblemProvider/visualize?format=frames</c> for a picture type that builds its own frames:
/// the picture type, a static payload sent once, and one frame per step followed by the solved frame.
/// </summary>
/// <param name="Type">A <see cref="VisualizationType"/> name, e.g. <c>"Graph"</c>.</param>
/// <param name="Payload">What does not change between frames.</param>
/// <param name="Frames">What changes: one per kept solver step, then the solved frame (unless the last step already is it).</param>
sealed record FramesResponse(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("payload")] object Payload,
    [property: JsonPropertyName("frames")] IReadOnlyList<object> Frames) {

    /// <summary>Serializer options for this response (camelCase names come from the records' attributes).</summary>
    public static JsonSerializerOptions JsonOptions { get; } = new() {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };
}

/// <summary>
/// A visualization that builds its own <c>format=frames</c> response from the solver's typed steps, instead of
/// the generic wrapping of the list items. Implement explicitly so it does not appear in /info. The list
/// format never uses this: <c>visualize</c>, <c>SolvedVisualization</c> and <c>StepsVisualization</c> keep
/// serving old frontends unchanged.
/// </summary>
interface IFramesVisualization : IVisualization {
    /// <param name="instance">The problem instance string.</param>
    /// <param name="run">The solver's answer and steps (use <see cref="SolveRun.StepsFor"/> before reading steps).</param>
    FramesResponse BuildFrames(string instance, SolveRun run);

    /// <summary>
    /// The picture type this visualization serves under <c>format=frames</c>: the <c>type</c> of its frames response (a
    /// <see cref="VisualizationType"/> name such as <c>"Graph"</c>). /info reports it as <c>pictureType</c> (#841), so a
    /// client need not run the visualization to find out. The default reads it off a frames response for the problem's
    /// default instance (see <see cref="PictureTypes"/>), so it always matches what <c>format=frames</c> returns. A method,
    /// not a property, so it never appears in JSON on its own.
    /// </summary>
    string? PictureType() => PictureTypes.Probe(this);
}
