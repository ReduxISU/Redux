using System.Text.Json.Serialization;
using API.Interfaces.Steps;

namespace API.Interfaces.Graphs;

/// <summary>Which rule decides how a chosen set of nodes is judged and drawn.</summary>
enum SubsetRule { Clique, VertexCover, MinVertexCover, IndependentSet, DominatingSet }

/// <summary>
/// What a subset-of-nodes problem hands the Graph picture: its graph, the optional K, and its rule.
/// No colors, no drawing.
/// </summary>
/// <param name="Nodes">Node ids, in instance order.</param>
/// <param name="Edges">Undirected edges as given (either endpoint order); duplicates and loops are tolerated.</param>
/// <param name="K">The instance's K, or null for problems without one (Minimum Vertex Cover).</param>
/// <param name="Rule">How a node set is judged.</param>
sealed record GraphSubsetSpec(
    IReadOnlyList<string> Nodes,
    IReadOnlyList<KeyValuePair<string, string>> Edges,
    int? K,
    SubsetRule Rule);

// --- JSON shapes (explicit names so the contract does not depend on serializer naming options) ---

sealed record GraphNodeDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label);

sealed record GraphEdgeDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("a")] string A,
    [property: JsonPropertyName("b")] string B) {
    [JsonPropertyName("weight")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Weight { get; init; }
}

/// <summary>The static part of a Graph picture, sent once per response.</summary>
sealed record GraphPayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("subtype")] string Subtype,
    [property: JsonPropertyName("layout")] string Layout,
    [property: JsonPropertyName("nodes")] IReadOnlyList<GraphNodeDto> Nodes,
    [property: JsonPropertyName("edges")] IReadOnlyList<GraphEdgeDto> Edges,
    [property: JsonPropertyName("shape")] string Shape,
    [property: JsonPropertyName("rule")] string Rule) {
    [JsonPropertyName("k")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? K { get; init; }
}

sealed record StateRef([property: JsonPropertyName("state")] PictureState State);

/// <summary>A drawn annotation that is not an edge of the graph (a missing edge between chosen nodes).</summary>
sealed record GraphPhantom(
    [property: JsonPropertyName("a")] string A,
    [property: JsonPropertyName("b")] string B,
    [property: JsonPropertyName("state")] PictureState State);

/// <summary>
/// One picture of the graph. <c>nodes</c> and <c>edges</c> list only elements whose state is not
/// <see cref="PictureState.Background"/>; an id that is absent is Background.
/// </summary>
sealed record GraphFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("nodes")] IReadOnlyDictionary<string, StateRef> Nodes,
    [property: JsonPropertyName("edges")] IReadOnlyDictionary<string, StateRef> Edges,
    [property: JsonPropertyName("phantoms")] IReadOnlyList<GraphPhantom> Phantoms) {
    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }
}

/// <summary>
/// Derives the Graph picture for subset-of-nodes problems from (graph, rule, partial <see cref="NodeSet"/>,
/// step event, focus). Nothing here knows about a particular problem or solver.
/// <para>
/// State precedence when several states apply to one element:
/// <c>Rejected &gt; ElementHighlight &gt; Solution &gt; Covered &gt; Blocked &gt; Background</c>.
/// A failure is never hidden by a highlight; the focus (what the solver is looking at now) shows over the
/// element's ordinary state, so a node that was just taken still reads as "being looked at".
/// </para>
/// <para>
/// "Partial": a step with <see cref="StepEvent.Accept"/> or <see cref="StepEvent.Backtrack"/> is a set still
/// under construction, so an uncovered edge or undominated node is not failed yet. <see cref="StepEvent.Try"/>,
/// <see cref="StepEvent.Reject"/> and <see cref="StepEvent.Done"/> judge a complete candidate, so they are.
/// Missing clique edges and edges inside an independent set are failures in every event.
/// </para>
/// </summary>
static class GraphSubsetFrames {
    public static string RuleName(SubsetRule rule) => rule switch {
        SubsetRule.Clique => "clique",
        SubsetRule.VertexCover => "vertexCover",
        SubsetRule.MinVertexCover => "minVertexCover",
        SubsetRule.IndependentSet => "independentSet",
        SubsetRule.DominatingSet => "dominatingSet",
        _ => throw new ArgumentOutOfRangeException(nameof(rule)),
    };

    /// <summary>Canonical edge id: <c>"a-b"</c> with a &lt; b by ordinal comparison.</summary>
    public static string EdgeId(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? $"{a}-{b}" : $"{b}-{a}";

    /// <summary>The distinct edges (loops dropped) with a before b, in first-seen order.</summary>
    private static List<(string A, string B, string Id)> CanonicalEdges(GraphSubsetSpec spec) {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<(string, string, string)>();
        foreach (var e in spec.Edges) {
            if (e.Key == e.Value) continue;
            string a = string.CompareOrdinal(e.Key, e.Value) < 0 ? e.Key : e.Value;
            string b = a == e.Key ? e.Value : e.Key;
            string id = EdgeId(a, b);
            if (seen.Add(id)) result.Add((a, b, id));
        }
        return result;
    }

    public static GraphPayload Payload(GraphSubsetSpec spec) =>
        new("graph", "undirected", "force",
            spec.Nodes.Select(n => new GraphNodeDto(n, n)).ToList(),
            CanonicalEdges(spec).Select(e => new GraphEdgeDto(e.Id, e.A, e.B)).ToList(),
            "subset", RuleName(spec.Rule)) { K = spec.K };

    private static int Rank(PictureState s) => s switch {
        PictureState.Rejected => 5,
        PictureState.ElementHighlight => 4,
        PictureState.Solution => 3,
        PictureState.Covered => 2,
        PictureState.Blocked => 1,
        _ => 0,
    };

    private static void Raise(Dictionary<string, PictureState> map, string id, PictureState state) {
        if (!map.TryGetValue(id, out var current) || Rank(state) > Rank(current)) map[id] = state;
    }

    /// <summary>The frame for a chosen set at one step.</summary>
    public static GraphFrame Frame(GraphSubsetSpec spec, NodeSet partial, StepEvent ev, string[] focus, string caption, bool? ok = null) {
        bool inProgress = ev is StepEvent.Accept or StepEvent.Backtrack;
        var nodeIds = new HashSet<string>(spec.Nodes, StringComparer.Ordinal);
        var chosen = new HashSet<string>(partial.Nodes.Where(nodeIds.Contains), StringComparer.Ordinal);
        var edges = CanonicalEdges(spec);
        var edgeIds = new HashSet<string>(edges.Select(e => e.Id), StringComparer.Ordinal);
        var adj = spec.Nodes.Distinct(StringComparer.Ordinal).ToDictionary(n => n, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var e in edges) {
            if (adj.TryGetValue(e.A, out var fromA)) fromA.Add(e.B);
            if (adj.TryGetValue(e.B, out var fromB)) fromB.Add(e.A);
        }

        var nodes = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var edgeStates = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var phantoms = new List<GraphPhantom>();

        foreach (var n in chosen) Raise(nodes, n, PictureState.Solution);

        switch (spec.Rule) {
            case SubsetRule.Clique:
                var members = chosen.OrderBy(n => n, StringComparer.Ordinal).ToList();
                for (int i = 0; i < members.Count; i++)
                    for (int j = i + 1; j < members.Count; j++) {
                        if (adj[members[i]].Contains(members[j])) Raise(edgeStates, EdgeId(members[i], members[j]), PictureState.Solution);
                        else phantoms.Add(new GraphPhantom(members[i], members[j], PictureState.Rejected));
                    }
                break;

            case SubsetRule.VertexCover:
            case SubsetRule.MinVertexCover:
                foreach (var e in edges) {
                    if (chosen.Contains(e.A) || chosen.Contains(e.B)) Raise(edgeStates, e.Id, PictureState.Covered);
                    else if (!inProgress) Raise(edgeStates, e.Id, PictureState.Rejected);
                }
                break;

            case SubsetRule.IndependentSet:
                foreach (var e in edges)
                    if (chosen.Contains(e.A) && chosen.Contains(e.B)) Raise(edgeStates, e.Id, PictureState.Rejected);
                foreach (var (n, neighbours) in adj)
                    if (!chosen.Contains(n) && neighbours.Any(chosen.Contains)) Raise(nodes, n, PictureState.Blocked);
                break;

            case SubsetRule.DominatingSet:
                foreach (var (n, neighbours) in adj) {
                    if (chosen.Contains(n)) continue;
                    if (neighbours.Any(chosen.Contains)) Raise(nodes, n, PictureState.Covered);
                    else if (!inProgress && chosen.Count > 0) Raise(nodes, n, PictureState.Rejected);
                }
                foreach (var e in edges)
                    if (chosen.Contains(e.A) != chosen.Contains(e.B)) Raise(edgeStates, e.Id, PictureState.Covered);
                break;
        }

        foreach (var id in focus) {
            if (nodeIds.Contains(id)) Raise(nodes, id, PictureState.ElementHighlight);
            else if (edgeIds.Contains(id)) Raise(edgeStates, id, PictureState.ElementHighlight);
        }

        return new GraphFrame(ev, caption, focus, ToRefs(nodes), ToRefs(edgeStates), phantoms) { Ok = ok };
    }

    private static SortedDictionary<string, StateRef> ToRefs(Dictionary<string, PictureState> map) {
        var sorted = new SortedDictionary<string, StateRef>(StringComparer.Ordinal);
        foreach (var (id, state) in map)
            if (state != PictureState.Background) sorted[id] = new StateRef(state);
        return sorted;
    }

    public static string Format(NodeSet set) => Braces(set.Nodes);

    /// <summary>"{a,b,c}" for captions.</summary>
    public static string Braces(IEnumerable<string> nodes) => "{" + string.Join(",", nodes) + "}";

    /// <summary>
    /// A plain reason a candidate set fails <paramref name="rule"/> ("b and c aren't joined"), or "" if it
    /// passes the structural part of the rule (sizes against K are the solver's own business). For solver
    /// captions; call it inside a recorder lambda so it costs nothing when recording is off.
    /// </summary>
    public static string Why(SubsetRule rule, IReadOnlyList<string> nodes, IReadOnlyList<KeyValuePair<string, string>> edges, IEnumerable<string> candidate) {
        var set = new HashSet<string>(candidate, StringComparer.Ordinal);
        var joined = new HashSet<string>(edges.Select(e => EdgeId(e.Key, e.Value)), StringComparer.Ordinal);
        switch (rule) {
            case SubsetRule.Clique:
                var ordered = set.OrderBy(n => n, StringComparer.Ordinal).ToList();
                for (int i = 0; i < ordered.Count; i++)
                    for (int j = i + 1; j < ordered.Count; j++)
                        if (!joined.Contains(EdgeId(ordered[i], ordered[j]))) return $"{ordered[i]} and {ordered[j]} aren't joined";
                break;
            case SubsetRule.IndependentSet:
                foreach (var e in edges)
                    if (e.Key != e.Value && set.Contains(e.Key) && set.Contains(e.Value)) return $"{e.Key} and {e.Value} are joined";
                break;
            case SubsetRule.VertexCover:
            case SubsetRule.MinVertexCover:
                foreach (var e in edges)
                    if (!set.Contains(e.Key) && !set.Contains(e.Value)) return $"edge {{{e.Key},{e.Value}}} isn't covered";
                break;
            case SubsetRule.DominatingSet:
                foreach (var n in nodes) {
                    if (set.Contains(n)) continue;
                    bool dominated = edges.Any(e => (e.Key == n && set.Contains(e.Value)) || (e.Value == n && set.Contains(e.Key)));
                    if (!dominated) return $"{n} isn't dominated";
                }
                break;
        }
        return "";
    }

    /// <summary>
    /// The whole response: the payload, a frame per kept step, then a solved frame built from the answer
    /// unless the last step already is the Done step.
    /// </summary>
    public static FramesResponse Build(GraphSubsetSpec spec, SolveRun run, Func<string, NodeSet> parseAnswer, Func<string, bool> verifyAnswer) {
        var frames = new List<object>();
        SolverStep<NodeSet>? last = null;
        foreach (var step in run.StepsFor(typeof(NodeSet)).OfType<SolverStep<NodeSet>>()) {
            frames.Add(Frame(spec, step.Partial, step.Event, step.Focus, step.Caption, step.Ok));
            last = step;
        }
        if (last is not { Event: StepEvent.Done }) {
            NodeSet answer = parseAnswer(run.Answer);
            frames.Add(Frame(spec, answer, StepEvent.Done, [],
                answer.Nodes.Length == 0 ? "No answer was found." : $"The answer is {Format(answer)}.",
                verifyAnswer(run.Answer)));
        }
        return new FramesResponse(nameof(VisualizationType.Graph), Payload(spec), frames);
    }
}

/// <summary>
/// A visualization for a subset-of-nodes graph problem. The visualization declares only what the problem
/// is (<see cref="Describe"/>) and, if its answers are not a plain brace-delimited node list, how to read one
/// (<see cref="ParseAnswer"/>); the frames hook then comes for free. Implement members explicitly so nothing
/// new appears in /info. The class must also declare <c>Type? IVisualization.StepShape =&gt; typeof(NodeSet)</c>.
/// </summary>
interface IGraphSubsetVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    GraphSubsetSpec Describe(TProblem problem);

    /// <summary>Reads a solver's answer string as a node set. The default accepts <c>{a,b,c}</c>, with or without braces.</summary>
    NodeSet ParseAnswer(TProblem problem, string answer) =>
        new(answer.Replace("{", "").Replace("}", "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0));

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return GraphSubsetFrames.Build(Describe(problem), run,
            answer => ParseAnswer(problem, answer),
            answer => {
                // The problem's own verifier decides; a certificate it cannot read (such as "{}") is not correct.
                try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
            });
    }
}
