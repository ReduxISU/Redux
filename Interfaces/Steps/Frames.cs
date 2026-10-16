using System.Text.Json;
using System.Text.Json.Serialization;

namespace API.Interfaces.Steps;

/// <summary>
/// The shared state vocabulary every picture type names its elements with; a frontend maps each name to a
/// style in one table. Serializes as its name (<c>"ElementHighlight"</c>, <c>"False"</c>). A missing entry in a frame
/// means <see cref="Background"/>. New names are added at the end, so the existing ones keep their numbers.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PictureState>))]
enum PictureState {
    /// <summary>Nothing has happened to it yet. Frames omit these entries.</summary>
    Background,
    /// <summary>What the solver is looking at or trying right now.</summary>
    ElementHighlight,
    /// <summary>
    /// Part of the confirmed answer: only in the Done frame with ok = true, or an Accept that ends the run (the next step
    /// is that Done). What a solver has kept before then is <see cref="Committed"/> (#886). A run that ends without an
    /// answer (a Done with ok = false) never shows it: what that Done picks is <see cref="Rejected"/>.
    /// </summary>
    Solution,
    /// <summary>Failed: uncovered, undominated, or doubled; and what a Done with ok = false picks (the run found nothing).</summary>
    Rejected,
    /// <summary>Never reached during this run.</summary>
    Untraveled,
    /// <summary>A constraint that is met (an edge or node covered, dominated).</summary>
    Covered,
    /// <summary>Cannot be chosen without breaking an exactly-once rule.</summary>
    Blocked,
    /// <summary>
    /// Assigned, and false under the current assignment (a literal whose variable has the other value). An element
    /// whose value is not decided yet stays <see cref="Background"/>.
    /// </summary>
    False,
    /// <summary>
    /// Kept so far but not confirmed: what an incremental solver has committed mid-run (queens or digits placed, finished
    /// sub-hulls, items packed, tree edges chosen, jobs scheduled), and a best-so-far a solver has accepted but may still
    /// replace. It becomes <see cref="Solution"/> only once the run ends on it (#886). A whole candidate that is only being
    /// tried is <see cref="ElementHighlight"/>, never this.
    /// </summary>
    Committed,
}

/// <summary>
/// The body of <c>/ProblemProvider/visualize?format=frames</c> for a picture type that builds its own frames:
/// the picture type, a static payload sent once, and one frame per step followed by the solved frame.
/// </summary>
/// <param name="Type">A <see cref="VisualizationType"/> name, e.g. <c>"Graph"</c>.</param>
/// <param name="Payload">What does not change between frames.</param>
/// <param name="Frames">
/// What changes: one per kept solver step, then the solved frame (unless the last step already is it). Every frame carries
/// <c>step</c> (#895), always written: the 0-based index, in the run's full step list (<see cref="SolveRun.Steps"/>, before
/// any filtering by shape), of the solver step it was drawn from. It is null for a frame drawn from no step: the solved frame
/// a builder adds from the answer, and the blank stopped frame it adds when a stopped run's own Done step cannot be drawn. A
/// stopped run's own Done step, when drawn, keeps its index. Pictures built from one run (<c>visualizeRun</c>) line up by it:
/// frames with the same <c>step</c> show the same moment of the run.
/// </param>
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
    /// <summary>
    /// Whether this picture says something useful about a run whose steps it cannot draw (another <see cref="IVisualization.StepShape"/>):
    /// its builder then draws the answer alone, a payload and one added solved frame (<c>step</c> null). <c>visualizeRun</c> returns
    /// such a solved-only picture when this is true and lists the visualization as <c>shape_mismatch</c> when it is false (#895).
    /// True by default: every picture whose frames show an answer. A picture of the search itself (the Search Tree) returns false,
    /// since with no steps it has nothing to show. A method, not a property, so it never appears in /info.
    /// </summary>
    bool DrawsAnswerAlone() => true;

    /// <param name="instance">The problem instance string.</param>
    /// <param name="run">The solver's answer and steps (use <see cref="SolveRun.StepsFor"/> before reading steps).</param>
    FramesResponse BuildFrames(string instance, SolveRun run);
}
