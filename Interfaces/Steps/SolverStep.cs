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
}
