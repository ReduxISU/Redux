using System.Text.Json.Serialization;
using API.Interfaces.Steps;

namespace API.Interfaces.Graphs;

// --- JSON shapes (explicit names so the contract does not depend on serializer naming options) ---

/// <summary>
/// The static part of a Nested Set picture, sent once per response: the Bipartite Graph's elements and sets, without its
/// membership edges (a set's <c>members</c> already say what it holds). A frontend draws each element as a dot and each set as
/// a region around its own dots, or falls back to a list of the sets when the regions would not read; that choice is the
/// renderer's, so nothing about it is sent.
/// </summary>
sealed record NestedSetPayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("layout")] string Layout,
    [property: JsonPropertyName("pickSide")] string PickSide,
    [property: JsonPropertyName("rule")] string Rule,
    [property: JsonPropertyName("elements")] IReadOnlyList<GraphNodeDto> Elements,
    [property: JsonPropertyName("sets")] IReadOnlyList<BipartiteSetDto> Sets) {
    [JsonPropertyName("k")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? K { get; init; }
}

/// <summary>
/// One picture of the sets: the Bipartite frame without <c>edges</c>. <c>nodes</c> holds the states of elements and sets alike
/// (their ids never collide), only those that are not <see cref="PictureState.Background"/>; <c>hits</c> says how many picked
/// nodes hit each node of the other column, for every node hit at least once (an annotation, drawn as a ×N badge above 1).
/// </summary>
sealed record NestedSetFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("nodes")] IReadOnlyDictionary<string, StateRef> Nodes,
    [property: JsonPropertyName("hits")] IReadOnlyDictionary<string, int> Hits) {
    /// <summary>The solver step this frame was drawn from (see <see cref="FramesResponse"/>, #895); null for an added frame. Always written.</summary>
    [JsonPropertyName("step")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? Step { get; init; }

    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }

    /// <summary>The Bipartite frame without its edges.</summary>
    public static NestedSetFrame Of(BipartiteFrame frame) =>
        new NestedSetFrame(frame.Event, frame.Caption, frame.Focus, frame.Nodes, frame.Hits) { Step = frame.Step, Ok = frame.Ok };
}

/// <summary>
/// Derives the Nested Set picture of a set problem (Set Cover, Exact Cover, Exact Hitting Set; #926): the sets drawn as sets,
/// each a region around its own elements. It is a second view of the Bipartite Graph and shares everything with it but the
/// edges: the spec (<see cref="BipartiteSpec"/>), the ids (element <c>e:{x}</c>, set <c>S{i+1}</c>; see
/// <see cref="BipartiteFrames"/>), the step shapes (<see cref="ChosenSets"/> when the answer picks sets, <see cref="NodeSet"/>
/// when it picks elements) and every state (<c>Covered</c>, <c>Blocked</c>, <c>Rejected</c> with its ×N count in <c>hits</c>,
/// <c>Committed</c>, <c>Solution</c>), which <see cref="BipartiteFrames.Frame"/> works out. So both pictures color the same
/// element and set alike.
/// <para>
/// A Done with ok = false draws its picks Rejected; an empty failed answer (Exact Cover and Hitting Set send none) is judged as
/// nothing, so that Done is blank apart from its caption. A run stopped by its time limit ends on a blank Done.
/// </para>
/// </summary>
static class NestedSetFrames {
    public const string KindName = "nestedSet";
    public const string LayoutName = "regions";

    // ---- The payload ----

    public static NestedSetPayload Payload(BipartiteSpec spec) {
        var graph = BipartiteFrames.Payload(spec);
        return new NestedSetPayload(KindName, LayoutName, graph.PickSide, graph.Rule, graph.Elements, graph.Sets) { K = graph.K };
    }

    // ---- The whole response ----

    /// <summary>
    /// The whole response: the Bipartite Graph's frames of the same run (<see cref="BipartiteFrames.Build"/>: a frame per kept
    /// step, then a solved frame built from the answer unless the last step already is the Done step, or a blank stopped frame),
    /// each without its edges, and this picture's payload.
    /// </summary>
    public static FramesResponse Build(BipartiteSpec spec, SolveRun run, Func<string, string[]?> parseAnswer, Func<string, bool> verifyAnswer) {
        var graph = BipartiteFrames.Build(spec, run, parseAnswer, verifyAnswer);
        var frames = graph.Frames.Select(f => (object)NestedSetFrame.Of((BipartiteFrame)f)).ToList();
        return new FramesResponse(nameof(VisualizationType.NestedSet), Payload(spec), frames);
    }
}

/// <summary>
/// A visualization that draws a set problem as nested sets (Set Cover, Exact Cover, Exact Hitting Set; #926). Like
/// <see cref="IBipartiteVisualization{TProblem}"/> it declares only what the problem is (<see cref="Describe"/>), and the frames
/// hook comes for free. Implement members explicitly so nothing new appears in /info, and declare
/// <c>Type? IVisualization.StepShape</c> as the Bipartite picture of the same problem does (<see cref="BipartiteFrames.StepShape"/>),
/// so every solver that draws on the Bipartite Graph draws on the Nested Set too.
/// </summary>
interface INestedSetVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    BipartiteSpec Describe(TProblem problem);

    /// <summary>
    /// Reads a solver's answer string as pick ids (see <see cref="BipartiteFrames.ParseAnswer"/>), or null when it cannot be
    /// read. Only used for the last frame of a solver whose steps do not end in a Done step.
    /// </summary>
    string[]? ParseAnswer(TProblem problem, string answer) => BipartiteFrames.ParseAnswer(Describe(problem), answer);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return NestedSetFrames.Build(Describe(problem), run,
            answer => ParseAnswer(problem, answer),
            answer => {
                // The problem's own verifier decides; a certificate it cannot read is not correct.
                try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
            });
    }
}
