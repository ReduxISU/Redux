using API.Interfaces.Steps;

namespace API.Interfaces.Graphs;

/// <summary>Which rule a tour is judged by. The picture is the same; the payload names the problem so a frontend can word its checks.</summary>
enum TourRule { HamiltonianCycle, TravelingSalesperson, MinimumTravelingSalesperson }

/// <summary>One edge of a tour problem's graph. For a directed graph it runs From to To; for an undirected one the ends are interchangeable.</summary>
/// <param name="From">One end (the tail, if directed).</param>
/// <param name="To">The other end (the head, if directed).</param>
/// <param name="Weight">The edge's weight (Traveling Salesperson, Minimum Traveling Salesperson), or null for an unweighted graph.</param>
sealed record GraphTourEdge(string From, string To, double? Weight = null);

/// <summary>
/// What a tour problem hands the Graph picture: its graph, whether the edges are directed, the optional K, and its rule.
/// No colors, no drawing.
/// </summary>
/// <param name="Nodes">Node ids, in instance order.</param>
/// <param name="Edges">The edges as listed in the instance, parallel edges included; loops are tolerated. Parallel edges (the
/// same pair of nodes, in the same direction if directed) are drawn as one edge weighing the lightest, labelled with every
/// listed weight (#769).</param>
/// <param name="Directed">True when an edge may only be walked from its From end to its To end.</param>
/// <param name="K">The instance's K (the most a tour may weigh), or null for problems without one.</param>
/// <param name="Rule">Which problem this is.</param>
sealed record GraphTourSpec(
    IReadOnlyList<string> Nodes,
    IReadOnlyList<GraphTourEdge> Edges,
    bool Directed,
    int? K,
    TourRule Rule);

/// <summary>
/// Derives the Graph picture for problems whose answer is a tour or path in visiting order (<c>shape: "tour"</c>:
/// Hamiltonian Cycle, Directed Hamiltonian Cycle, Traveling Salesperson, Minimum Traveling Salesperson). It shares the payload, frame and state
/// vocabulary of <see cref="GraphSubsetFrames"/> and nothing here knows about a particular problem or solver.
/// <para>
/// Every frame carries <c>order</c>, the path so far as node ids in visiting order. A closed tour repeats its first node
/// at the end, exactly like the certificate. The states, derived from <c>order</c> alone:
/// </para>
/// <list type="bullet">
/// <item>every node on the path is <c>Covered</c>; once the path is the confirmed answer (a <see cref="StepEvent.Done"/>
/// frame with ok true, or an Accept that ends the run) they are <c>Solution</c>, like the green nodes of the solved
/// mockups;</item>
/// <item>each hop of the path that is an edge of the graph (in the hop's direction, for a directed graph) is <c>Solution</c>
/// in the confirmed answer, <c>Rejected</c> in a Done with ok = false (the run found nothing; its nodes stay <c>Covered</c>),
/// and <c>Committed</c> while the path is only kept so far (#886);</item>
/// <item>in a step that only tries a whole tour (<see cref="StepEvent.Try"/>, <see cref="StepEvent.Reject"/> of a run that is
/// not <see cref="SolveRun.Incremental"/>) its nodes and hops are <c>ElementHighlight</c> instead (see
/// <see cref="GraphSubsetFrames.Picked"/>); an incremental run's path is the path so far, so it keeps its Committed hops in every
/// step and only the focus (the next node, the hop to it, or the closing hop) is marked;</item>
/// <item>a hop that is not an edge (a dead end the solver tried, or a closing edge the graph does not have) is a
/// <c>Rejected</c> phantom between its two nodes, drawn as a dashed line;</item>
/// <item>what the solver is looking at (the focus) is <c>ElementHighlight</c>; in a <see cref="StepEvent.Reject"/> step it
/// is the thing that failed, so <c>Rejected</c>.</item>
/// </list>
/// <para>
/// Edge ids: <c>"a-b"</c> with a &lt; b for an undirected graph (as in every other Graph shape), and <c>"a&gt;b"</c>, tail
/// first and never reordered, for a directed one. Directed ids keep the direction in the id because a directed graph can
/// hold both a-to-b and b-to-a as two different edges, which a sorted <c>"a-b"</c> would merge into one; and a frame's
/// state map is keyed by id, so the two must not share one. An edge's <c>a</c> is its tail and <c>b</c> its head, and
/// <c>subtype: "directed"</c> tells the frontend to draw the arrowhead at <c>b</c>.
/// </para>
/// </summary>
static class GraphTourFrames {
    public const string ShapeName = "tour";

    public static string RuleName(TourRule rule) => rule switch {
        TourRule.HamiltonianCycle => "hamiltonianCycle",
        TourRule.TravelingSalesperson => "tsp",
        TourRule.MinimumTravelingSalesperson => "minTsp",
        _ => throw new ArgumentOutOfRangeException(nameof(rule)),
    };

    /// <summary>
    /// The id of an edge from <paramref name="from"/> to <paramref name="to"/>: <c>"a&gt;b"</c> if directed, else <c>"a-b"</c>
    /// with a before b. Names are escaped (<see cref="GraphSubsetFrames.EscapeName"/>), so ids are unique even when names
    /// contain <c>-</c> or <c>&gt;</c>.
    /// </summary>
    public static string EdgeId(bool directed, string from, string to) =>
        directed ? $"{GraphSubsetFrames.EscapeName(from)}>{GraphSubsetFrames.EscapeName(to)}" : GraphSubsetFrames.EdgeId(from, to);

    /// <summary>
    /// One edge per pair of nodes (per direction, if directed), loops dropped, in first-seen order. A tour takes the
    /// cheapest way between two cities, so the weight is the lightest of the pair's listed weights; the label lists them
    /// all in the order listed (<c>"2,5"</c>), or is null for an edge listed once or an unweighted graph (#769).
    /// </summary>
    private static List<(string A, string B, string Id, double? Weight, string? Label)> CanonicalEdges(GraphTourSpec spec) {
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        var result = new List<(string A, string B, string Id, List<double?> Weights)>();
        foreach (var e in spec.Edges) {
            if (e.From == e.To) continue;
            string a = e.From, b = e.To;
            if (!spec.Directed && string.CompareOrdinal(a, b) > 0) (a, b) = (b, a);
            string id = EdgeId(spec.Directed, a, b);
            if (index.TryGetValue(id, out int at)) {
                result[at].Weights.Add(e.Weight);
            } else {
                index[id] = result.Count;
                result.Add((a, b, id, [e.Weight]));
            }
        }
        return result.Select(e => (e.A, e.B, e.Id, GraphEdgeFrames.Counted(e.Weights, addWeights: false), GraphEdgeFrames.WeightLabel(e.Weights))).ToList();
    }

    public static GraphPayload Payload(GraphTourSpec spec) =>
        new("graph", spec.Directed ? "directed" : "undirected", "circle",
            spec.Nodes.Select(n => new GraphNodeDto(n, n)).ToList(),
            CanonicalEdges(spec).Select(e => new GraphEdgeDto(e.Id, e.A, e.B) { Weight = e.Weight, Label = e.Label }).ToList(),
            ShapeName, RuleName(spec.Rule)) { K = spec.K };

    /// <summary>
    /// The frame for a path at one step. <c>incremental</c> says whether the step comes from an incremental run
    /// (<see cref="SolveRun.Incremental"/>); <c>endsRun</c> whether it is an Accept that ends the run
    /// (<see cref="GraphSubsetFrames.EndsRun"/>).
    /// </summary>
    public static GraphFrame Frame(GraphTourSpec spec, Tour partial, StepEvent ev, string[] focus, string caption, bool? ok = null, bool incremental = false, bool endsRun = false) {
        var nodeIds = new HashSet<string>(spec.Nodes, StringComparer.Ordinal);
        var edgeIds = new HashSet<string>(CanonicalEdges(spec).Select(e => e.Id), StringComparer.Ordinal);
        var order = partial.Order.Where(nodeIds.Contains).ToList();

        var nodes = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var edgeStates = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var phantoms = new List<GraphPhantom>();

        // A whole tour only being tried (Try, Reject) is highlighted, nodes and hops alike; otherwise (and always in an
        // incremental run, whose path is the path so far) its hops are Committed and its nodes Covered, or both Solution once
        // the path is the confirmed answer (#886). A Done that found nothing has Rejected hops on Covered nodes.
        var picked = GraphSubsetFrames.Picked(ev, incremental, endsRun, ok == true);
        var onPath = picked == PictureState.ElementHighlight ? PictureState.ElementHighlight
            : ev == StepEvent.Done ? (ok == true ? PictureState.Solution : PictureState.Covered)
            : picked == PictureState.Solution ? PictureState.Solution : PictureState.Covered;
        foreach (var n in order) GraphSubsetFrames.Raise(nodes, n, onPath);

        var drawn = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i + 1 < order.Count; i++) {
            string from = order[i], to = order[i + 1];
            if (from == to) continue;
            string id = EdgeId(spec.Directed, from, to);
            if (edgeIds.Contains(id)) GraphSubsetFrames.Raise(edgeStates, id, picked);
            else if (drawn.Add(id)) phantoms.Add(new GraphPhantom(from, to, PictureState.Rejected));
        }

        // Focus is what the solver is looking at; in a Reject step it is what failed.
        var mark = ev == StepEvent.Reject ? PictureState.Rejected : PictureState.ElementHighlight;
        foreach (var id in focus) {
            if (nodeIds.Contains(id)) GraphSubsetFrames.Raise(nodes, id, mark);
            else if (edgeIds.Contains(id)) GraphSubsetFrames.Raise(edgeStates, id, mark);
        }

        return new GraphFrame(ev, caption, focus, GraphSubsetFrames.ToRefs(nodes), GraphSubsetFrames.ToRefs(edgeStates), phantoms) {
            Ok = ok,
            Order = order,
        };
    }

    // ---- Reading answers ----

    /// <summary>
    /// The tour named by an answer such as <c>{1,2,4,5,3,1}</c> (or the bare <c>1,2,3</c>), in the order written, a
    /// closing repeat of the first node included. <c>{}</c> gives no tour. Names that are not nodes of the graph are
    /// dropped when a frame is built.
    /// </summary>
    public static Tour ParseTour(string answer) =>
        new(answer.Replace("{", "").Replace("}", "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0));

    // ---- Captions, for solvers to call inside a recorder lambda so they cost nothing when recording is off ----

    /// <summary>"1 → 4 → 3": the path with arrows.</summary>
    public static string Arrows(IEnumerable<string> order) => string.Join(" → ", order);

    /// <summary>The node names of a path given as indexes into <paramref name="nodes"/>, closed back to its start when
    /// <paramref name="close"/> is set: the order a Tour takes.</summary>
    public static List<string> NodeNames(IReadOnlyList<string> nodes, IReadOnlyList<int> path, bool close = false) {
        var names = path.Select(i => nodes[i]).ToList();
        if (close) names.Add(nodes[path[0]]);
        return names;
    }

    // ---- The whole response ----

    // A stopped run's last frame: blank, with the empty order every tour frame carries.
    private static GraphFrame StoppedFrame(string caption) => GraphSubsetFrames.StoppedFrame(caption) with { Order = [] };

    /// <summary>
    /// The whole response: the payload, a frame per kept step, then a solved frame built from the answer unless the last
    /// step already is the Done step. A run stopped by its time limit ends with a blank Done frame (see
    /// <see cref="GraphSubsetFrames.StoppedFrame"/>) with an empty <c>order</c> instead, and its answer string is never read.
    /// </summary>
    public static FramesResponse Build(GraphTourSpec spec, SolveRun run, Func<string, Tour> parseAnswer, Func<string, bool> verifyAnswer) {
        var frames = new List<object>();
        SolverStep<Tour>? last = null;
        foreach (var (step, endsRun, index) in GraphSubsetFrames.DrawnSteps<Tour>(run)) {
            frames.Add((run.TimedOut && step.Event == StepEvent.Done
                ? StoppedFrame(step.Caption)
                : Frame(spec, step.Partial, step.Event, step.Focus, step.Caption, step.Ok, run.Incremental, endsRun)) with { Step = index });
            last = step;
        }
        if (run.TimedOut) {
            if (last is not { Event: StepEvent.Done }) frames.Add(StoppedFrame(GraphSubsetFrames.StoppedCaption));
        } else if (last is not { Event: StepEvent.Done }) {
            bool ok = verifyAnswer(run.Answer);
            var shown = ok ? parseAnswer(run.Answer) : new Tour([]);
            frames.Add(Frame(spec, shown, StepEvent.Done, [],
                shown.Order.Length == 0 ? "No answer was found." : $"The tour is {Arrows(shown.Order)}.",
                ok));
        }
        return new FramesResponse(nameof(VisualizationType.Graph), Payload(spec), frames);
    }
}

/// <summary>
/// A visualization for a graph problem whose answer is a tour or path in visiting order (Hamiltonian Cycle, Directed
/// Hamiltonian Cycle, Traveling Salesperson, Minimum Traveling Salesperson). The visualization declares only what the problem is (<see cref="Describe"/>);
/// the frames hook then comes for free. Implement members explicitly so nothing new appears in /info. The class must also
/// declare <c>Type? IVisualization.StepShape =&gt; typeof(Tour)</c>. A solver's steps carry the path so far
/// (<see cref="Tour"/>: node ids in order); only the solved frame of a solver that records no steps has to be rebuilt
/// from the answer string.
/// </summary>
interface IGraphTourVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    GraphTourSpec Describe(TProblem problem);

    /// <summary>Reads a solver's answer string as a tour. The default accepts <c>{1,2,4,5,3,1}</c>, with or without braces.</summary>
    Tour ParseAnswer(TProblem problem, string answer) => GraphTourFrames.ParseTour(answer);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return GraphTourFrames.Build(Describe(problem), run,
            answer => ParseAnswer(problem, answer),
            answer => {
                // The problem's own verifier decides; a certificate it cannot read (such as "{}") is not correct.
                try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
            });
    }
}
