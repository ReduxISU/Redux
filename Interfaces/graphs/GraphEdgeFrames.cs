using System.Text.RegularExpressions;
using API.Interfaces.Steps;

namespace API.Interfaces.Graphs;

/// <summary>Which rule decides how a chosen set of edges, or a split of the nodes, is judged and drawn.</summary>
enum EdgeRule { SpanningTree, SteinerTree, Cut, WeightedCut, MaxCut, MinCut }

/// <summary>An undirected edge as a problem gives it, with its weight when the problem has weights.</summary>
sealed record GraphEdgeInput(string A, string B, double? Weight = null);

/// <summary>
/// What an edge-answer or cut-answer problem hands the Graph picture: its graph, its rule, and the optional
/// terminals and K. No colors, no drawing.
/// </summary>
/// <param name="Nodes">Node ids, in instance order.</param>
/// <param name="Edges">Undirected edges as given (either endpoint order). Loops are dropped; for parallel edges the lightest is drawn.</param>
/// <param name="Rule">How an answer is judged and which shape it is drawn in.</param>
sealed record GraphEdgeSpec(IReadOnlyList<string> Nodes, IReadOnlyList<GraphEdgeInput> Edges, EdgeRule Rule) {
    /// <summary>Steiner terminals: nodes the tree must reach. Empty for every other rule.</summary>
    public IReadOnlyList<string> Terminals { get; init; } = [];
    /// <summary>The instance's K, or null for problems without one.</summary>
    public int? K { get; init; }
}

/// <summary>
/// Derives the Graph picture for problems whose answer is a set of edges (<c>shape: "edges"</c>: Minimum Spanning
/// Tree, Steiner Tree) or a split of the nodes in two shown by the edges that cross it (<c>shape: "cut"</c>: Cut,
/// Weighted Cut, Max Cut, Minimum Cut). It shares the payload, frame and state vocabulary of
/// <see cref="GraphSubsetFrames"/> and nothing here knows about a particular problem or solver.
/// <para>
/// Edges answer, from the partial <see cref="EdgeSet"/> (edge keys are the payload's edge ids, <c>"a-b"</c>):
/// chosen edges are <c>Solution</c>; a node an edge of the answer touches is <c>Covered</c> (the nodes joined so
/// far). In a step that only tries a whole candidate (<see cref="StepEvent.Try"/>, <see cref="StepEvent.Reject"/> of a run
/// that is not <see cref="SolveRun.Incremental"/>) those edges and nodes are <c>ElementHighlight</c> instead (see
/// <see cref="GraphSubsetFrames.Picked"/>); in an incremental run (Kruskal, Prim) they are the tree so far, so they stay
/// <c>Solution</c> and <c>Covered</c> in every event. What the solver is looking at is <c>ElementHighlight</c>, or <c>Rejected</c> in a
/// <see cref="StepEvent.Reject"/> step (an edge that would close a cycle, a terminal that is not reached).
/// Everything else is Background. Steiner terminals are not a state: the payload marks them (<c>terminal: true</c>).
/// </para>
/// <para>
/// Cut answer, from the partial <see cref="Groups"/> (node id to 0, the S side, or 1, the T side): the frame carries
/// the split as <c>groups</c> (an annotation, so the frontend can color the sides) and every edge that crosses it
/// is <c>Solution</c>, or <c>ElementHighlight</c> in a step that only tries the split (Try, Reject of a run that is not
/// incremental). The focus is highlighted; in a Reject step of an incremental run it is the piece that failed, so
/// <c>Rejected</c>. Nodes keep their ordinary states.
/// </para>
/// </summary>
static class GraphEdgeFrames {
    public static string RuleName(EdgeRule rule) => rule switch {
        EdgeRule.SpanningTree => "spanningTree",
        EdgeRule.SteinerTree => "steinerTree",
        EdgeRule.Cut => "cut",
        EdgeRule.WeightedCut => "weightedCut",
        EdgeRule.MaxCut => "maxCut",
        EdgeRule.MinCut => "minCut",
        _ => throw new ArgumentOutOfRangeException(nameof(rule)),
    };

    /// <summary>"cut" for the four problems that split the nodes in two, "edges" for the others.</summary>
    public static string ShapeName(EdgeRule rule) =>
        rule is EdgeRule.Cut or EdgeRule.WeightedCut or EdgeRule.MaxCut or EdgeRule.MinCut ? "cut" : "edges";

    /// <summary>The distinct edges (loops dropped, the lightest of parallel edges kept) with a before b, in first-seen order.</summary>
    internal static List<(string A, string B, string Id, double? Weight)> CanonicalEdges(IEnumerable<GraphEdgeInput> edges) {
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        var result = new List<(string A, string B, string Id, double? Weight)>();
        foreach (var e in edges) {
            if (e.A == e.B) continue;
            string a = string.CompareOrdinal(e.A, e.B) < 0 ? e.A : e.B;
            string b = a == e.A ? e.B : e.A;
            string id = GraphSubsetFrames.EdgeId(a, b);
            if (!index.TryGetValue(id, out int at)) {
                index[id] = result.Count;
                result.Add((a, b, id, e.Weight));
            } else if (e.Weight < result[at].Weight) {
                result[at] = (a, b, id, e.Weight);
            }
        }
        return result;
    }

    public static GraphPayload Payload(GraphEdgeSpec spec) {
        var terminals = new HashSet<string>(spec.Terminals, StringComparer.Ordinal);
        return new GraphPayload("graph", "undirected", "force",
            spec.Nodes.Select(n => new GraphNodeDto(n, n) { Terminal = terminals.Contains(n) ? true : null }).ToList(),
            CanonicalEdges(spec.Edges).Select(e => new GraphEdgeDto(e.Id, e.A, e.B) { Weight = e.Weight }).ToList(),
            ShapeName(spec.Rule), RuleName(spec.Rule)) { K = spec.K };
    }

    // Focus is what the solver is looking at; in a Reject step it is the thing that failed.
    private static void MarkFocus(string[] focus, StepEvent ev, HashSet<string> nodeIds, HashSet<string> edgeIds,
        Dictionary<string, PictureState> nodes, Dictionary<string, PictureState> edgeStates) {
        var mark = ev == StepEvent.Reject ? PictureState.Rejected : PictureState.ElementHighlight;
        foreach (var id in focus) {
            if (nodeIds.Contains(id)) GraphSubsetFrames.Raise(nodes, id, mark);
            else if (edgeIds.Contains(id)) GraphSubsetFrames.Raise(edgeStates, id, mark);
        }
    }

    /// <summary>
    /// The frame for a chosen set of edges at one step. <c>incremental</c> says whether the step comes from an incremental
    /// run (<see cref="SolveRun.Incremental"/>).
    /// </summary>
    public static GraphFrame EdgeFrame(GraphEdgeSpec spec, EdgeSet partial, StepEvent ev, string[] focus, string caption, bool? ok = null, bool incremental = false) {
        var edges = CanonicalEdges(spec.Edges);
        var byId = edges.ToDictionary(e => e.Id, StringComparer.Ordinal);
        var nodeIds = new HashSet<string>(spec.Nodes, StringComparer.Ordinal);
        var nodes = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var edgeStates = new Dictionary<string, PictureState>(StringComparer.Ordinal);

        // A whole candidate only being tried (Try, Reject) is highlighted, edges and the nodes they touch alike; a set the
        // solver has taken (in an incremental run, the tree so far in every step), and the answer, are Solution edges
        // joining Covered nodes.
        var picked = GraphSubsetFrames.Picked(ev, incremental);
        var touched = picked == PictureState.Solution ? PictureState.Covered : PictureState.ElementHighlight;
        foreach (var id in partial.Edges) {
            if (!byId.TryGetValue(id, out var e)) continue;
            GraphSubsetFrames.Raise(edgeStates, id, picked);
            GraphSubsetFrames.Raise(nodes, e.A, touched);
            GraphSubsetFrames.Raise(nodes, e.B, touched);
        }
        MarkFocus(focus, ev, nodeIds, new HashSet<string>(byId.Keys, StringComparer.Ordinal), nodes, edgeStates);

        return new GraphFrame(ev, caption, focus, GraphSubsetFrames.ToRefs(nodes), GraphSubsetFrames.ToRefs(edgeStates), []) { Ok = ok };
    }

    /// <summary>
    /// The frame for a split of the nodes at one step. The edges that cross it are the cut. <c>incremental</c> says whether
    /// the step comes from an incremental run (<see cref="SolveRun.Incremental"/>).
    /// </summary>
    public static GraphFrame CutFrame(GraphEdgeSpec spec, Groups partial, StepEvent ev, string[] focus, string caption, bool? ok = null, bool incremental = false) {
        var edges = CanonicalEdges(spec.Edges);
        var nodeIds = new HashSet<string>(spec.Nodes, StringComparer.Ordinal);
        var groups = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (id, group) in partial.Of)
            if (nodeIds.Contains(id)) groups[id] = group;

        var nodes = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var edgeStates = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        foreach (var e in edges)
            if (groups.TryGetValue(e.A, out int ga) && groups.TryGetValue(e.B, out int gb) && ga != gb)
                GraphSubsetFrames.Raise(edgeStates, e.Id, GraphSubsetFrames.Picked(ev, incremental));
        // A rejected whole split is not an element that failed (nothing in it is wrong, it is just the wrong size), so its
        // focus is only highlighted; the event tells the frontend it was rejected. An incremental run's focus is the piece
        // under test, so a Reject marks it Rejected.
        MarkFocus(focus, incremental ? ev : StepEvent.Try, nodeIds, new HashSet<string>(edges.Select(e => e.Id), StringComparer.Ordinal), nodes, edgeStates);

        return new GraphFrame(ev, caption, focus, GraphSubsetFrames.ToRefs(nodes), GraphSubsetFrames.ToRefs(edgeStates), []) {
            Ok = ok,
            Groups = groups,
        };
    }

    // ---- Reading answers ----

    private static readonly Regex EndpointPair = new(@"\{\s*([^{}(),\s]+)\s*,\s*([^{}(),\s]+)\s*\}", RegexOptions.Compiled);

    /// <summary>
    /// The edges named by an answer such as <c>{{1,2},{2,3}}</c> or <c>{({1,2},4),({2,3},1)}</c>, as edge ids. Weights are
    /// ignored; anything unreadable gives an empty set.
    /// </summary>
    public static EdgeSet ParseEdges(string answer) =>
        new(EndpointPair.Matches(answer).Select(m => GraphSubsetFrames.EdgeId(m.Groups[1].Value, m.Groups[2].Value)));

    /// <summary>
    /// A split of the nodes whose crossing edges are exactly the edges in <paramref name="answer"/>, for answers that
    /// list the cut edges instead of a side. Each piece of the graph starts on side 0 and every cut edge flips the side;
    /// if the edges do not describe a split at all, or there are none, the split is empty (no side is invented).
    /// </summary>
    public static Groups PartitionFromCutEdges(GraphEdgeSpec spec, string answer) {
        var cut = new HashSet<string>(ParseEdges(answer).Edges, StringComparer.Ordinal);
        if (cut.Count == 0) return new Groups([]);
        var adjacent = spec.Nodes.Distinct(StringComparer.Ordinal)
            .ToDictionary(n => n, _ => new List<(string To, bool Cut)>(), StringComparer.Ordinal);
        foreach (var e in CanonicalEdges(spec.Edges)) {
            bool isCut = cut.Contains(e.Id);
            if (adjacent.TryGetValue(e.A, out var fromA) && adjacent.TryGetValue(e.B, out var fromB)) {
                fromA.Add((e.B, isCut));
                fromB.Add((e.A, isCut));
            }
        }
        var side = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var start in adjacent.Keys) {
            if (side.ContainsKey(start)) continue;
            side[start] = 0;
            var queue = new Queue<string>([start]);
            while (queue.Count > 0) {
                string at = queue.Dequeue();
                foreach (var (to, isCut) in adjacent[at]) {
                    int want = isCut ? 1 - side[at] : side[at];
                    if (!side.TryAdd(to, want)) {
                        if (side[to] != want) return new Groups([]);
                    } else queue.Enqueue(to);
                }
            }
        }
        return new Groups(side);
    }

    /// <summary>The split that puts <paramref name="sSide"/> on side 0 and every other node on side 1.</summary>
    public static Groups Split(IEnumerable<string> nodes, IEnumerable<string> sSide) {
        var s = new HashSet<string>(sSide, StringComparer.Ordinal);
        return new Groups(nodes.Select(n => new KeyValuePair<string, int>(n, s.Contains(n) ? 0 : 1)));
    }

    // ---- Captions, for solvers to call inside a recorder lambda so they cost nothing when recording is off ----

    /// <summary>"{1,2,3}" for the nodes on one side.</summary>
    public static string Side(IEnumerable<string> nodes) => GraphSubsetFrames.Braces(nodes.OrderBy(n => n, StringComparer.Ordinal));

    /// <summary>How many distinct edges join one side of the split to the other, and their total weight.</summary>
    public static (int Count, double Weight) Crossing(IEnumerable<GraphEdgeInput> edges, ISet<string> sSide) {
        int count = 0;
        double weight = 0;
        foreach (var e in CanonicalEdges(edges))
            if (sSide.Contains(e.A) != sSide.Contains(e.B)) {
                count++;
                weight += e.Weight ?? 0;
            }
        return (count, weight);
    }

    // ---- The whole response ----

    /// <summary>
    /// The whole response for an edges answer: the payload, a frame per kept step, then a solved frame built from the
    /// answer unless the last step already is the Done step. A run stopped by its time limit ends with
    /// <see cref="GraphSubsetFrames.StoppedFrame"/> instead, and its answer string is never read.
    /// </summary>
    public static FramesResponse BuildEdges(GraphEdgeSpec spec, SolveRun run, Func<string, EdgeSet> parseAnswer, Func<string, bool> verifyAnswer) {
        var frames = new List<object>();
        SolverStep<EdgeSet>? last = null;
        foreach (var step in run.StepsFor(typeof(EdgeSet)).OfType<SolverStep<EdgeSet>>()) {
            frames.Add(run.TimedOut && step.Event == StepEvent.Done
                ? GraphSubsetFrames.StoppedFrame(step.Caption)
                : EdgeFrame(spec, step.Partial, step.Event, step.Focus, step.Caption, step.Ok, run.Incremental));
            last = step;
        }
        if (run.TimedOut) {
            if (last is not { Event: StepEvent.Done }) frames.Add(GraphSubsetFrames.StoppedFrame(GraphSubsetFrames.StoppedCaption));
        } else if (last is not { Event: StepEvent.Done }) {
            EdgeSet answer = parseAnswer(run.Answer);
            frames.Add(EdgeFrame(spec, answer, StepEvent.Done, [],
                answer.Edges.Length == 0 ? "No answer was found." : $"The answer is {GraphSubsetFrames.Braces(answer.Edges)}.",
                verifyAnswer(run.Answer)));
        }
        return new FramesResponse(nameof(VisualizationType.Graph), Payload(spec), frames);
    }

    // A stopped cut run's last frame: blank, with the empty split every cut frame carries.
    private static GraphFrame StoppedCutFrame(string caption) =>
        GraphSubsetFrames.StoppedFrame(caption) with { Groups = new SortedDictionary<string, int>(StringComparer.Ordinal) };

    /// <summary>The same for a cut answer.</summary>
    public static FramesResponse BuildCut(GraphEdgeSpec spec, SolveRun run, Func<string, Groups> parseAnswer, Func<string, bool> verifyAnswer) {
        var frames = new List<object>();
        SolverStep<Groups>? last = null;
        foreach (var step in run.StepsFor(typeof(Groups)).OfType<SolverStep<Groups>>()) {
            frames.Add(run.TimedOut && step.Event == StepEvent.Done
                ? StoppedCutFrame(step.Caption)
                : CutFrame(spec, step.Partial, step.Event, step.Focus, step.Caption, step.Ok, run.Incremental));
            last = step;
        }
        if (run.TimedOut) {
            if (last is not { Event: StepEvent.Done }) frames.Add(StoppedCutFrame(GraphSubsetFrames.StoppedCaption));
        } else if (last is not { Event: StepEvent.Done }) {
            Groups answer = parseAnswer(run.Answer);
            string caption = answer.Of.Count == 0
                ? "No answer was found."
                : $"The answer splits the nodes into {Side(answer.Of.Where(p => p.Value == 0).Select(p => p.Key))} and {Side(answer.Of.Where(p => p.Value != 0).Select(p => p.Key))}.";
            frames.Add(CutFrame(spec, answer, StepEvent.Done, [], caption, verifyAnswer(run.Answer)));
        }
        return new FramesResponse(nameof(VisualizationType.Graph), Payload(spec), frames);
    }
}

/// <summary>
/// A visualization for a graph problem whose answer is a set of edges (Minimum Spanning Tree, Steiner Tree). The
/// visualization declares only what the problem is (<see cref="Describe"/>); the frames hook then comes for free.
/// Implement members explicitly so nothing new appears in /info. The class must also declare
/// <c>Type? IVisualization.StepShape =&gt; typeof(EdgeSet)</c>.
/// </summary>
interface IGraphEdgesVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    GraphEdgeSpec Describe(TProblem problem);

    /// <summary>Reads a solver's answer string as a set of edges. The default accepts <c>{{a,b},{c,d}}</c> and <c>{({a,b},w),...}</c>.</summary>
    EdgeSet ParseAnswer(TProblem problem, string answer) => GraphEdgeFrames.ParseEdges(answer);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return GraphEdgeFrames.BuildEdges(Describe(problem), run,
            answer => ParseAnswer(problem, answer),
            answer => {
                // The problem's own verifier decides; a certificate it cannot read (such as "{}") is not correct.
                try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
            });
    }
}

/// <summary>
/// A visualization for a graph problem whose answer is a split of the nodes in two, drawn by the edges that cross it
/// (Cut, Weighted Cut, Max Cut, Minimum Cut). The class must also declare
/// <c>Type? IVisualization.StepShape =&gt; typeof(Groups)</c>. A solver's steps carry the split (<see cref="Groups"/>:
/// 0 for the S side, 1 for the T side); only the solved frame of a solver that records no steps has to be rebuilt from
/// the answer string.
/// </summary>
interface IGraphCutVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    GraphEdgeSpec Describe(TProblem problem);

    /// <summary>
    /// Reads a solver's answer string as a split. The default reads a list of cut edges (<c>{{a,b},...}</c> or
    /// <c>{({a,b},w),...}</c>) and recovers a split that produces exactly those crossings.
    /// </summary>
    Groups ParseAnswer(TProblem problem, string answer) => GraphEdgeFrames.PartitionFromCutEdges(Describe(problem), answer);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return GraphEdgeFrames.BuildCut(Describe(problem), run,
            answer => ParseAnswer(problem, answer),
            answer => {
                try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
            });
    }
}
