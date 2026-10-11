using API.Interfaces.Steps;

namespace API.Interfaces.Table;

/// <summary>
/// Dijkstra's table (SSSP, SPSP): the Table picture's second view of the <see cref="Distances"/> steps the Layered Graph
/// picture draws. One row per node, in instance order, with when it was settled, its cost so far, the node it is reached
/// through and its path from the source. Nothing here changes a solver; the table is read off the same recorded steps.
/// <list type="bullet">
/// <item>The node a step settles (or the source, at the start) is the focus (<c>ElementHighlight</c>). The cost and via
/// cells that changed at the step are marked changed.</item>
/// <item>Dijkstra is incremental: a settled node's row is <c>Solution</c> in every step, since its cost is final.</item>
/// <item>In Done: for SSSP every reached node's row is <c>Solution</c>; for SPSP the path's rows are, or the target's row is
/// <c>Rejected</c> when it can't be reached. A node that can't be reached is <c>Untraveled</c>.</item>
/// <item>Settled shows <c>#k</c>, the order the node was settled in, or <c>yes</c> when that step was past the recorder's cap
/// and is not known.</item>
/// </list>
/// </summary>
static class DistanceTraceTable {
    public const string Infinity = "∞";
    public const string None = "—";

    /// <summary>The columns: Node, Settled, Cost, Via, Path.</summary>
    public static TableSpec Spec(string source, string? target) =>
        new(TableShape.Trace, [new("Node", "node"), new("Settled", "settled"), new("Cost", "cost"), new("Via", "via"), new("Path", "path")]) {
            Title = target == null ? $"Shortest paths from {source}" : $"Shortest path from {source} to {target}",
        };

    /// <summary>The nodes with nothing worked out yet.</summary>
    public static TableDraft Blank(IReadOnlyList<string> nodes) {
        var draft = new TableDraft();
        foreach (var n in nodes) draft.AddRow(n, null, null, null, null);
        return draft;
    }

    /// <summary>One draft per step. <paramref name="target"/> is SPSP's target, null for SSSP.</summary>
    public static IReadOnlyList<TableDraft> Drafts(IReadOnlyList<SolverStep<Distances>> steps, IReadOnlyList<string> nodes, string source, string? target) {
        var order = new Dictionary<string, int>(StringComparer.Ordinal);
        bool orderKnown = true;
        Distances? before = null;
        var drafts = new List<TableDraft>();
        foreach (var step in steps) {
            var d = step.Partial;
            bool done = step.Event == StepEvent.Done;
            var settled = new HashSet<string>(d.Settled, StringComparer.Ordinal);
            // The node this step settles: the one new to the settled set. Once steps were not shown (more than one new),
            // the order is no longer known.
            var fresh = nodes.Where(n => settled.Contains(n) && !order.ContainsKey(n)).ToList();
            string? settledNow = fresh.Count == 1 ? fresh[0] : null;
            if (fresh.Count > 1) orderKnown = false;
            if (settledNow != null && orderKnown) order[settledNow] = order.Count + 1;
            bool empty = done && d.Dist.Count == 0;

            var draft = new TableDraft();
            for (int i = 0; i < nodes.Count; i++) {
                string n = nodes[i];
                if (empty) { draft.AddRow(n, null, null, null, null); continue; }
                bool reached = d.Dist.TryGetValue(n, out int cost);
                string settledText = settled.Contains(n) ? (order.TryGetValue(n, out int k) ? $"#{k}" : "yes")
                    : done && !reached ? "unreachable" : "not yet";
                draft.AddRow(n, settledText, reached ? TableFrames.Num(cost) : Infinity,
                    d.Pred.TryGetValue(n, out var via) ? via : None, reached ? PathText(d, source, n) : None);
                if (before != null) {
                    if (Changed(before.Dist, d.Dist, n)) draft.MarkChanged(i, 2);
                    if (Changed(before.Pred, d.Pred, n)) draft.MarkChanged(i, 3);
                }

                if (!done) {
                    if (settled.Contains(n)) draft.MarkRow(i, PictureState.Solution);
                } else if (step.Ok == true) {
                    if (target == null ? reached : OnPath(d, source, target, n)) draft.MarkRow(i, PictureState.Solution);
                    else if (!reached) draft.MarkRow(i, PictureState.Untraveled);
                } else if (target != null && n == target) {
                    draft.MarkRow(i, PictureState.Rejected);
                } else if (!reached) {
                    draft.MarkRow(i, PictureState.Untraveled);
                }
            }
            if (!done) {
                string? focus = step.Event == StepEvent.Try && before == null ? source : settledNow;
                int at = focus == null ? -1 : nodes.ToList().IndexOf(focus);
                if (at >= 0) draft.FocusRow(at);
            }
            before = d;
            drafts.Add(draft);
        }
        return drafts;
    }

    private static bool Changed<T>(IReadOnlyDictionary<string, T> before, IReadOnlyDictionary<string, T> now, string n) {
        bool had = before.TryGetValue(n, out var a), has = now.TryGetValue(n, out var b);
        return had != has || has && !EqualityComparer<T>.Default.Equals(a, b);
    }

    // The source-to-node path read off the predecessors, written "1 → 3 → 5"; "—" when it does not lead back to the source.
    private static string PathText(Distances d, string source, string node) =>
        Path(d, source, node) is { } path ? string.Join(" → ", path) : None;

    private static List<string>? Path(Distances d, string source, string node) {
        var path = new List<string> { node };
        var seen = new HashSet<string>(StringComparer.Ordinal) { node };
        while (path[0] != source) {
            if (!d.Pred.TryGetValue(path[0], out var p) || !seen.Add(p)) return null;
            path.Insert(0, p);
        }
        return path;
    }

    private static bool OnPath(Distances d, string source, string target, string node) =>
        Path(d, source, target)?.Contains(node) == true;
}
