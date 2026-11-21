using System.Text.Json.Serialization;

namespace API.Interfaces.Steps;

/// <summary>What a solver did at one recorded step. Serializes as its name, e.g. <c>"Try"</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
enum StepEvent { Try, Accept, Reject, Backtrack, Done }
