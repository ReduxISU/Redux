namespace API.Interfaces.Steps;

/// <summary>
/// One step as a solver describes it to <see cref="StepRecorder{TPartial}"/>: the answer so far and a one-sentence caption,
/// plus the ids the step is about and, for a solver that branches, the search-tree nodes it creates or updates. Pass it in a
/// lambda (<c>rec.Reject(() =&gt; new(partial, caption) { Focus = [id] })</c>) so that it is built only when the recorder
/// keeps the step.
/// </summary>
/// <param name="Partial">The answer so far (<see cref="SolverStep{TPartial}.Partial"/>).</param>
/// <param name="Caption">One plain sentence describing the step (<see cref="SolverStep{TPartial}.Caption"/>).</param>
sealed record Step<TPartial>(TPartial Partial, string Caption) {
    /// <summary>The ids the step is about: nodes, edge keys, clauses, variables (<see cref="SolverStep{TPartial}.Focus"/>).</summary>
    public string[] Focus { get; init; } = [];

    /// <summary>
    /// The search-tree nodes the step creates or updates, the last being the node it is about
    /// (<see cref="SolverStep{TPartial}.Tree"/>); null for a solver that does not branch.
    /// </summary>
    public SearchNode[]? Tree { get; init; }
}
