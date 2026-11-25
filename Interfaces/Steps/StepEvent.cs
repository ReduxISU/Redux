using System.Text.Json.Serialization;

namespace API.Interfaces.Steps;

/// <summary>
/// What a solver did at one recorded step. Serializes as its name, e.g. <c>"Try"</c>, and the pictures and both front ends
/// read it. The Update keyword (<see cref="RunSteps{TState, TPartial}.Update"/>, a step with no verdict) is not an event of
/// its own: it records a Try.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
enum StepEvent { Try, Accept, Reject, Backtrack, Done }
