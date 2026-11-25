using System.Text.Json.Serialization;

namespace API.Interfaces.Steps;

/// <summary>
/// Where a node of a backtracking or branch-and-bound search stands. Serializes in lower case (<c>"queued"</c>).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SearchNodeState>))]
enum SearchNodeState {
    /// <summary>Made and waiting in a best-first solver's queue; not expanded yet.</summary>
    [JsonStringEnumMemberName("queued")] Queued,
    /// <summary>Looked at, or expanded into children.</summary>
    [JsonStringEnumMemberName("explored")] Explored,
    /// <summary>Cut without being explored (a clash, a false clause, a bound that cannot win).</summary>
    [JsonStringEnumMemberName("pruned")] Pruned,
    /// <summary>Explored, and every branch under it failed, so the solver backed out of it.</summary>
    [JsonStringEnumMemberName("failed")] Failed,
    /// <summary>An answer (for a branch and bound, the best one so far).</summary>
    [JsonStringEnumMemberName("solution")] Solution,
    /// <summary>Never needed once its parent was decided (game trees).</summary>
    [JsonStringEnumMemberName("skipped")] Skipped,
}

/// <summary>
/// One node of a search tree, as a step reports it: a patch that creates the node the first time its id is seen and
/// updates its state (and bound) after that. A step that carries patches (<see cref="SolverStep{TPartial}.Tree"/>) is
/// about its LAST patch: the node the solver is working on. Every other picture ignores them.
/// </summary>
/// <param name="NodeId">The node's id, unique within one run (a counter the solver keeps).</param>
/// <param name="ParentId">The id of the node it branched from; null for the root.</param>
/// <param name="Label">The choice that leads to it, short: <c>x2=F</c>, <c>(1,3)</c>, <c>Denver</c>; <c>start</c> for a root.</param>
/// <param name="State">Where the node stands after this step.</param>
sealed record SearchNode(
    [property: JsonPropertyName("nodeId")] int NodeId,
    [property: JsonPropertyName("parentId")] int? ParentId,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("state")] SearchNodeState State) {

    /// <summary>A short note on the bound or cost that decided it (<c>lb 4,525</c>, <c>tour 5,075</c>); omitted when null.</summary>
    [JsonPropertyName("bound")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Bound { get; init; }
}
