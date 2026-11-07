namespace API.Interfaces.Steps;

/// <summary>Small helpers for the plain-sentence captions of construction steps (<see cref="BuildDelta"/>).</summary>
static class BuildCaptions {
    /// <summary>"a, b, c", or "a, b, c, d, e, f and 4 more" past <paramref name="max"/> names, so captions stay one readable sentence.</summary>
    public static string Names(IReadOnlyCollection<string> names, int max = 6) =>
        names.Count <= max
            ? string.Join(", ", names)
            : string.Join(", ", names.Take(max)) + $" and {names.Count - max} more";
}

/// <summary>
/// Keeps a reduction's construction steps to a readable number without losing any of the target.
/// <para>
/// Construction steps are deltas replayed in order, so they cannot be capped the way a solver's steps are: a dropped
/// delta leaves the replayed target incomplete, and a late delta moved forward draws a target that never existed. A
/// reduction therefore records every step (construction is polynomial and <c>/reduce</c> builds the whole target
/// anyway), and <see cref="Limit"/> then keeps the first <c>cap</c> steps as they are and folds the rest, except the
/// closing Done step, into ONE step that adds all of their pieces and says so in its caption. Replaying the kept steps
/// therefore always reproduces the whole target, at any size.
/// </para>
/// </summary>
static class ConstructionSteps {
    /// <summary>
    /// The steps unchanged when there are at most <paramref name="cap"/> before the closing Done step; otherwise the first
    /// <paramref name="cap"/>, one combined step for the rest, then the Done step.
    /// </summary>
    public static IReadOnlyList<SolverStep<BuildDelta>> Limit(IReadOnlyList<SolverStep<BuildDelta>> steps, int cap = StepRecorder<BuildDelta>.DefaultCap) {
        bool endsInDone = steps.Count > 0 && steps[^1].Event == StepEvent.Done;
        int body = endsInDone ? steps.Count - 1 : steps.Count;
        if (body <= cap) return steps;
        var result = steps.Take(cap).ToList();
        result.Add(Combine(steps.Skip(cap).Take(body - cap).ToList()));
        if (endsInDone) result.Add(steps[^1]);
        return result;
    }

    /// <summary>
    /// One step that adds everything <paramref name="rest"/> adds (nodes in their order, edges and skipped pairs sorted by
    /// id): an Accept when any of them was an Accept, a Reject when none was. A single step is returned as it is.
    /// </summary>
    private static SolverStep<BuildDelta> Combine(IReadOnlyList<SolverStep<BuildDelta>> rest) {
        if (rest.Count == 1) return rest[0];
        string[] nodes = rest.SelectMany(s => s.Partial.Nodes).Distinct(StringComparer.Ordinal).ToArray();
        BuildEdge[] edges = BuildEdge.Sorted(rest.SelectMany(s => s.Partial.Edges).DistinctBy(e => e.Id, StringComparer.Ordinal));
        BuildEdge[] skipped = BuildEdge.Sorted(rest.SelectMany(s => s.Partial.Skipped).DistinctBy(e => e.Id, StringComparer.Ordinal));
        var delta = new BuildDelta {
            SourceFocus = rest.SelectMany(s => s.Partial.SourceFocus).Distinct(StringComparer.Ordinal).ToArray(),
            Nodes = nodes,
            Edges = edges,
            Skipped = skipped,
            Gadgets = rest.SelectMany(s => s.Partial.Gadgets).Distinct().ToArray(),
            Bound = rest.LastOrDefault(s => s.Partial.Bound != null)?.Partial.Bound,
        };
        string[] focus = rest.SelectMany(s => s.Focus).Distinct(StringComparer.Ordinal).ToArray();
        string what = nodes.Length > 0
            ? $"{Words.Count(nodes.Length, "node")} and {Words.Count(edges.Length, "edge")}"
            : Words.Count(edges.Length, "edge");
        string left = skipped.Length > 0 ? $", and leave {Words.Count(skipped.Length, "pair")} without an edge" : "";
        string caption = $"The last {rest.Count} steps are combined into this one to keep the list short. Together they add {what}{left}.";
        StepEvent ev = rest.Any(s => s.Event == StepEvent.Accept) ? StepEvent.Accept : StepEvent.Reject;
        return new SolverStep<BuildDelta>(delta, ev, focus, caption);
    }
}
