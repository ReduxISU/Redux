using System.Text.RegularExpressions;
using API.Interfaces.Steps;

namespace API.Interfaces.Graphs;

/// <summary>Which rule decides how a split of the nodes into groups is judged and drawn.</summary>
enum GroupRule { Coloring, CliqueCover }

/// <summary>
/// What a node-grouping problem hands the Graph picture: its graph, its K and its rule. No colors, no drawing.
/// </summary>
/// <param name="Nodes">Node ids, in instance order.</param>
/// <param name="Edges">Undirected edges as given (either endpoint order); duplicates and loops are tolerated.</param>
/// <param name="K">The instance's K: the number of colors, or the most cliques the cover may use.</param>
/// <param name="Rule">How a grouping is judged.</param>
sealed record GraphGroupSpec(IReadOnlyList<string> Nodes, IReadOnlyList<KeyValuePair<string, string>> Edges, int K, GroupRule Rule);

/// <summary>What is wrong with a candidate grouping, and the nodes that show it.</summary>
sealed record GroupFailure(string Reason, string[] Focus);

/// <summary>
/// Derives the Graph picture for problems whose answer splits the nodes into groups (<c>shape: "groups"</c>: Graph
/// Coloring, Clique Cover). It shares the payload, frame and state vocabulary of <see cref="GraphSubsetFrames"/> and
/// nothing here knows about a particular problem or solver.
/// <para>
/// Every frame carries <c>groups</c> (node id to group index, 0-based; a node with no group yet is left out), the
/// annotation the frontend colors the nodes with. Group colors are the meaning of a grouping, so a node is never
/// <c>Solution</c> (the mockups keep the group colors on a solved picture; green is not used for nodes). The states
/// that are left:
/// </para>
/// <list type="bullet">
/// <item><b>Coloring</b>: an edge whose two ends share a group is <c>Rejected</c> (a clash). Other edges are plain.</item>
/// <item><b>Clique cover</b>: an edge inside a group is <c>Solution</c> in the confirmed answer (the clique is drawn solid),
/// <c>Rejected</c> in a Done with ok = false (the run found nothing), <c>Committed</c> in a grouping kept so far, or <c>ElementHighlight</c> in a step that only tries a whole grouping
/// (Try, Reject of a run that is not <see cref="SolveRun.Incremental"/>: see <see cref="GraphSubsetFrames.Picked"/>); a missing edge
/// between two nodes of one group is a <c>Rejected</c> phantom, as in the Clique rule of <see cref="GraphSubsetFrames"/>.</item>
/// <item><b>Clique cover</b>: in a step that judges a whole candidate (a Reject of a run that is not incremental, or a Done
/// with ok = false that carries a grouping), a node in no group is <c>Rejected</c>: no clique covers it.</item>
/// <item>Either rule: a node whose group index is K or more (more colors, or more cliques, than allowed) is <c>Rejected</c>.</item>
/// <item>What the solver is looking at (the node being colored) is <c>ElementHighlight</c>; in a
/// <see cref="StepEvent.Reject"/> step it is the thing that failed, so <c>Rejected</c>.</item>
/// </list>
/// A clash is a clash however many nodes are still uncolored, so no event is treated as "still in progress". Group
/// indices are 0-based in the data and in captions.
/// </summary>
static class GraphGroupFrames {
    public static string RuleName(GroupRule rule) => rule switch {
        GroupRule.Coloring => "coloring",
        GroupRule.CliqueCover => "cliqueCover",
        _ => throw new ArgumentOutOfRangeException(nameof(rule)),
    };

    public const string ShapeName = "groups";

    private static List<(string A, string B, string Id)> CanonicalEdges(GraphGroupSpec spec) {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<(string, string, string)>();
        foreach (var e in spec.Edges) {
            if (e.Key == e.Value) continue;
            string a = string.CompareOrdinal(e.Key, e.Value) < 0 ? e.Key : e.Value;
            string b = a == e.Key ? e.Value : e.Key;
            string id = GraphSubsetFrames.EdgeId(a, b);
            if (seen.Add(id)) result.Add((a, b, id));
        }
        return result;
    }

    public static GraphPayload Payload(GraphGroupSpec spec) =>
        new("graph", "undirected", "force",
            spec.Nodes.Select(n => new GraphNodeDto(n, n)).ToList(),
            CanonicalEdges(spec).Select(e => new GraphEdgeDto(e.Id, e.A, e.B)).ToList(),
            ShapeName, RuleName(spec.Rule)) { K = spec.K };

    /// <summary>
    /// The frame for a grouping at one step. <c>incremental</c> says whether the step comes from an incremental run
    /// (<see cref="SolveRun.Incremental"/>), whose grouping so far is committed, so its clique edges stay <c>Committed</c> until
    /// the answer; <c>endsRun</c> whether it is an Accept that ends the run (<see cref="GraphSubsetFrames.EndsRun"/>).
    /// </summary>
    public static GraphFrame Frame(GraphGroupSpec spec, Groups partial, StepEvent ev, string[] focus, string caption, bool? ok = null, bool incremental = false, bool endsRun = false) {
        var nodeIds = new HashSet<string>(spec.Nodes, StringComparer.Ordinal);
        var groups = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (id, group) in partial.Of)
            if (nodeIds.Contains(id)) groups[id] = group;
        var edges = CanonicalEdges(spec);
        var edgeIds = new HashSet<string>(edges.Select(e => e.Id), StringComparer.Ordinal);

        var nodes = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var edgeStates = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var phantoms = new List<GraphPhantom>();

        foreach (var (id, group) in groups)
            if (group >= spec.K) GraphSubsetFrames.Raise(nodes, id, PictureState.Rejected);

        switch (spec.Rule) {
            case GroupRule.Coloring:
                foreach (var e in edges)
                    if (groups.TryGetValue(e.A, out int ga) && groups.TryGetValue(e.B, out int gb) && ga == gb)
                        GraphSubsetFrames.Raise(edgeStates, e.Id, PictureState.Rejected);
                break;

            case GroupRule.CliqueCover:
                // A step that judges a whole candidate (a Reject of a run that is not incremental, or a failed Done that
                // carries one) shows a node in no group as Rejected: the candidate covers it with no clique.
                bool judgesCandidate = (ev == StepEvent.Reject && !incremental) || (ev == StepEvent.Done && ok != true && groups.Count > 0);
                if (judgesCandidate)
                    foreach (var id in spec.Nodes)
                        if (!groups.ContainsKey(id)) GraphSubsetFrames.Raise(nodes, id, PictureState.Rejected);
                foreach (var members in groups.GroupBy(g => g.Value).OrderBy(g => g.Key).Select(g => g.Select(p => p.Key).ToList())) {
                    for (int i = 0; i < members.Count; i++)
                        for (int j = i + 1; j < members.Count; j++) {
                            string id = GraphSubsetFrames.EdgeId(members[i], members[j]);
                            if (edgeIds.Contains(id)) GraphSubsetFrames.Raise(edgeStates, id, GraphSubsetFrames.Picked(ev, incremental, endsRun, ok == true));
                            else phantoms.Add(new GraphPhantom(members[i], members[j], PictureState.Rejected));
                        }
                }
                break;
        }

        // Focus is what the solver is looking at; in a Reject step it is what failed.
        var mark = ev == StepEvent.Reject ? PictureState.Rejected : PictureState.ElementHighlight;
        foreach (var id in focus) {
            if (nodeIds.Contains(id)) GraphSubsetFrames.Raise(nodes, id, mark);
            else if (edgeIds.Contains(id)) GraphSubsetFrames.Raise(edgeStates, id, mark);
        }

        return new GraphFrame(ev, caption, focus, GraphSubsetFrames.ToRefs(nodes), GraphSubsetFrames.ToRefs(edgeStates), phantoms) {
            Ok = ok,
            Groups = groups,
        };
    }

    // ---- Reading answers ----

    private static readonly Regex Class = new(@"\{([^{}]*)\}", RegexOptions.Compiled);

    /// <summary>
    /// The groups named by an answer such as <c>{{a,d},{b},{c}}</c> (or the bare <c>{1,2},{3}</c>): the n-th innermost
    /// brace pair is group n, so an empty class keeps its place. <c>{}</c> and anything unreadable give no groups.
    /// </summary>
    public static Groups ParseClasses(string answer) {
        var of = new List<KeyValuePair<string, int>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (Match m in Class.Matches(answer)) {
            foreach (var node in m.Groups[1].Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
                if (seen.Add(node)) of.Add(new(node, index));
            index++;
        }
        return new Groups(of);
    }

    // ---- Captions and reasons, for solvers to call inside a recorder lambda so they cost nothing when recording is off ----

    /// <summary>"{1,2},{},{3}": one brace pair per group index, in order, empty ones kept so a position is its index.</summary>
    public static string Classes(IReadOnlyDictionary<string, int> of, int? count = null) {
        int n = count ?? (of.Count == 0 ? 0 : of.Values.Max() + 1);
        var classes = Enumerable.Range(0, n).Select(g =>
            Words.Braces(of.Where(p => p.Value == g).Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal)));
        return string.Join(",", classes);
    }

    /// <summary>
    /// A node's colored neighbours and the colors they use, for the why of a coloring step: "neighbour a uses color 0",
    /// "neighbours a and b use colors 0 and 1" (with <paramref name="already"/>, "neighbours a and b already use ..."), or
    /// "no neighbour is colored yet" when there are none.
    /// </summary>
    public static string NeighboursUse(IReadOnlyList<string> neighbours, IReadOnlyList<int> colors, bool already = false) {
        if (neighbours.Count == 0) return "no neighbour is colored yet";
        string who = neighbours.Count == 1 ? "neighbour" : "neighbours";
        string verb = neighbours.Count == 1 ? "uses" : "use";
        string what = colors.Count == 1 ? "color" : "colors";
        return $"{who} {Words.List(neighbours)} {(already ? "already " : "")}{verb} {what} {Words.List(colors.Select(c => c.ToString()).ToList())}";
    }

    /// <summary>
    /// The first thing wrong with a candidate grouping, or null if it is fine. Coloring: two neighbours that share a color
    /// ("1 and 2 are both color 0"), else a node that has no color. Clique cover: the nodes in no clique, all of them
    /// ("node 1 is in no clique"), else a pair in one clique that is not joined ("1 and 4 are in one group but
    /// aren't joined"). Sizes against K are the solver's own business.
    /// </summary>
    public static GroupFailure? Why(GroupRule rule, IReadOnlyList<string> nodes, IReadOnlyList<KeyValuePair<string, string>> edges, IReadOnlyDictionary<string, int> of) {
        if (rule == GroupRule.Coloring) {
            foreach (var e in edges)
                if (e.Key != e.Value && of.TryGetValue(e.Key, out int a) && of.TryGetValue(e.Value, out int b) && a == b)
                    return new($"{e.Key} and {e.Value} are both color {a}", [e.Key, e.Value]);
        } else {
            // A node in no clique is the first thing wrong with a cover candidate, so name every such node.
            var left = nodes.Where(n => !of.ContainsKey(n)).Distinct(StringComparer.Ordinal).ToList();
            if (left.Count > 0)
                return new(left.Count == 1 ? $"node {left[0]} is in no clique" : $"nodes {Words.List(left)} are in no clique", left.ToArray());
            var joined = new HashSet<string>(edges.Select(e => GraphSubsetFrames.EdgeId(e.Key, e.Value)), StringComparer.Ordinal);
            foreach (var members in of.GroupBy(p => p.Value).OrderBy(g => g.Key).Select(g => g.Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).ToList()))
                for (int i = 0; i < members.Count; i++)
                    for (int j = i + 1; j < members.Count; j++)
                        if (!joined.Contains(GraphSubsetFrames.EdgeId(members[i], members[j])))
                            return new($"{members[i]} and {members[j]} are in one group but aren't joined", [members[i], members[j]]);
        }
        string? missing = nodes.FirstOrDefault(n => !of.ContainsKey(n));
        return missing == null ? null : new($"{missing} has no color", [missing]);
    }

    // ---- The whole response ----

    // A stopped run's last frame: blank, with the empty grouping every groups frame carries.
    private static GraphFrame StoppedFrame(string caption) =>
        GraphSubsetFrames.StoppedFrame(caption) with { Groups = new SortedDictionary<string, int>(StringComparer.Ordinal) };

    /// <summary>
    /// The whole response: the payload, a frame per kept step, then a solved frame built from the answer unless the last
    /// step already is the Done step. A run stopped by its time limit ends with a blank Done frame (see
    /// <see cref="GraphSubsetFrames.StoppedFrame"/>) with empty <c>groups</c> instead, and its answer string is never read.
    /// </summary>
    public static FramesResponse Build(GraphGroupSpec spec, SolveRun run, Func<string, Groups> parseAnswer, Func<string, bool> verifyAnswer) {
        var frames = new List<object>();
        SolverStep<Groups>? last = null;
        foreach (var (step, endsRun, index) in GraphSubsetFrames.DrawnSteps<Groups>(run)) {
            frames.Add((run.TimedOut && step.Event == StepEvent.Done
                ? StoppedFrame(step.Caption)
                : Frame(spec, step.Partial, step.Event, step.Focus, step.Caption, step.Ok, run.Incremental, endsRun)) with { Step = index });
            last = step;
        }
        if (run.TimedOut) {
            if (last is not { Event: StepEvent.Done }) frames.Add(StoppedFrame(GraphSubsetFrames.StoppedCaption));
        } else if (last is not { Event: StepEvent.Done }) {
            Groups answer = parseAnswer(run.Answer);
            frames.Add(Frame(spec, answer, StepEvent.Done, [],
                answer.Of.Count == 0 ? "No answer was found." : $"The answer is {Classes(answer.Of)}.",
                verifyAnswer(run.Answer)));
        }
        return new FramesResponse(nameof(VisualizationType.Graph), Payload(spec), frames);
    }
}

/// <summary>
/// A visualization for a graph problem whose answer splits the nodes into groups (Graph Coloring, Clique Cover). The
/// visualization declares only what the problem is (<see cref="Describe"/>); the frames hook then comes for free.
/// Implement members explicitly so nothing new appears in /info. The class must also declare
/// <c>Type? IVisualization.StepShape =&gt; typeof(Groups)</c>. A solver's steps carry the grouping so far
/// (<see cref="Groups"/>: node id to group index); only the solved frame of a solver that records no steps has to be
/// rebuilt from the answer string.
/// </summary>
interface IGraphGroupsVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    GraphGroupSpec Describe(TProblem problem);

    /// <summary>Reads a solver's answer string as groups. The default accepts <c>{{a,d},{b},{c}}</c>: the n-th class is group n.</summary>
    Groups ParseAnswer(TProblem problem, string answer) => GraphGroupFrames.ParseClasses(answer);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return GraphGroupFrames.Build(Describe(problem), run,
            answer => ParseAnswer(problem, answer),
            answer => {
                // The problem's own verifier decides; a certificate it cannot read (such as "{}") is not correct.
                try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
            });
    }
}
