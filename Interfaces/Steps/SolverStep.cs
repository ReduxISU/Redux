using System.Text.Json.Serialization;

namespace API.Interfaces.Steps;

/// <summary>
/// One recorded moment of a solver run.
/// </summary>
/// <typeparam name="TPartial">The answer shape so far (one of the shapes in Shapes.cs), typed by the shape of the answer, not a certificate string.</typeparam>
/// <param name="Partial">The answer so far.</param>
/// <param name="Event">What the solver did.</param>
/// <param name="Focus">Ids the solver is looking at (nodes, edge keys, cells).</param>
/// <param name="Caption">One plain sentence describing the step.</param>
record SolverStep<TPartial>(
    [property: JsonPropertyName("partial")] TPartial Partial,
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("caption")] string Caption) {

    /// <summary>Whether the answer is correct. Set only on <see cref="StepEvent.Done"/>; omitted from JSON otherwise.</summary>
    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }

    /// <summary>
    /// The search-tree nodes this step creates or updates, for the Search Tree picture; the last one is the node the step is
    /// about. Null (and omitted from JSON) for every step of a solver that does not branch, so its JSON is what it always was.
    /// </summary>
    [JsonPropertyName("tree")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<SearchNode>? Tree { get; init; }
}
