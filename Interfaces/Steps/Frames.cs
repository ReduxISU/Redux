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

    /// <summary>
    /// The seed a randomized solver ran with (<see cref="SolveRun.Seed"/>, #932), so the run can be asked for again; absent for
    /// every other solver. Only the endpoints set it (<c>visualize?format=frames</c>), never a builder; Redux_GUI reads no frames
    /// response, so it never sees it.
    /// </summary>
    [JsonPropertyName("seed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Seed { get; init; }

    /// <summary>
    /// Serializer options for this response (camelCase names come from the records' attributes). Not indented: a frames
    /// response runs to hundreds of kilobytes, and indentation was over half of it (#909).
    /// </summary>
    public static JsonSerializerOptions JsonOptions { get; } = new() {
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

    /// <summary>
    /// Whether this picture can draw a run of <paramref name="solverType"/> (a solver of its problem). True by default. A picture
    /// of a search (the Search Tree) returns false for a randomized solver (<see cref="IRandomizedSolver"/>): its tries are not a
    /// tree of choices, so <c>visualize</c> and <c>reductionView</c> answer <c>solver_mismatch</c> and <c>visualizeRun</c> lists
    /// the picture as <c>solver_mismatch</c> (#932). A method, not a property, so it never appears in /info.
    /// </summary>
    bool DrawsSolver(Type solverType) => true;

    /// <summary>
    /// How this picture names the pieces its problem's default picture names, for one instance: given an id of the default
    /// picture, the ids of this picture that stand for the same piece (none when it has no such piece). Reductions name their
    /// target's (and source's) pieces by the default picture's ids, in their gadgets, construction steps and backward map; the
    /// Reduction View passes every such id through this when the caller chose this picture instead (#929), so gadgets and
    /// highlights land on its pieces. Null (the default) means this picture uses the default picture's ids as they are (the
    /// CNF Formula shares the Grouped Graph's). A method, not a property, so it never appears in /info.
    /// </summary>
    Func<string, IReadOnlyList<string>>? IdsFromDefaultPicture(string instance) => null;

    /// <param name="instance">The problem instance string.</param>
    /// <param name="run">The solver's answer and steps (use <see cref="SolveRun.StepsFor"/> before reading steps).</param>
    FramesResponse BuildFrames(string instance, SolveRun run);

    /// <summary>
    /// Whether this visualization's list methods (<c>StepsVisualization</c>) draw the solver's typed steps. False (the
    /// default) means they were written for legacy steps, so the list format gets none of the typed ones. A visualization
    /// whose list format already drew typed steps before it got a frames hook (DFA) returns true, so its list output stays
    /// as it was. A method, not a property, so it never appears in /info.
    /// </summary>
    bool ListReadsTypedSteps() => false;

    /// <summary>
    /// The picture type this visualization serves under <c>format=frames</c>: the <c>type</c> of its frames response (a
    /// <see cref="VisualizationType"/> name such as <c>"Graph"</c>). /info reports it as <c>pictureType</c> (#841), so a
    /// client need not run the visualization to find out. The default reads it off a frames response for the problem's
    /// default instance (see <see cref="PictureTypes"/>), so it always matches what <c>format=frames</c> returns. A method,
    /// not a property, so it never appears in JSON on its own.
    /// </summary>
    string? PictureType() => PictureTypes.Probe(this);
}
