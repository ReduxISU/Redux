using System.Text.Json.Serialization;
using API.Interfaces.Steps;
using SPADE;

namespace API.Interfaces.Graphs;

/// <summary>Which column of the Bipartite picture the answer names: the sets (Set Cover, Exact Cover) or the elements (Hitting Set).</summary>
enum BipartitePick { Sets, Elements }

/// <summary>
/// How often every node of the other column must be hit by the picked nodes: at least once (Set Cover) or exactly once
/// (Exact Cover, Exact Hitting Set).
/// </summary>
enum BipartiteRule { AtLeastOnce, ExactlyOnce }

/// <summary>
/// What a set problem hands the Bipartite picture: the universe, the sets, the optional K, the column the answer picks and
/// the rule. No layout, no colors, no edges; the base derives those.
/// </summary>
/// <param name="Universe">The element names, in instance order.</param>
/// <param name="Sets">Each set's element names, in instance order. A set is named by its position (<c>S1</c>, <c>S2</c>, ...),
/// never by its contents, so two sets with the same elements are still two sets.</param>
/// <param name="K">The instance's K (Set Cover's budget), or null for problems without one.</param>
/// <param name="Pick">The column the answer names.</param>
/// <param name="Rule">How often each node of the other column must be hit.</param>
sealed record BipartiteSpec(
    IReadOnlyList<string> Universe,
    IReadOnlyList<IReadOnlyList<string>> Sets,
    int? K,
    BipartitePick Pick,
    BipartiteRule Rule);

// --- JSON shapes (explicit names so the contract does not depend on serializer naming options) ---

/// <summary>A set: its id (<c>S1</c>), its label (the same) and the ids of its elements, in instance order.</summary>
sealed record BipartiteSetDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("members")] IReadOnlyList<string> Members);

/// <summary>The static part of a Bipartite Graph picture, sent once per response.</summary>
sealed record BipartitePayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("layout")] string Layout,
    [property: JsonPropertyName("pickSide")] string PickSide,
    [property: JsonPropertyName("rule")] string Rule,
    [property: JsonPropertyName("elements")] IReadOnlyList<GraphNodeDto> Elements,
    [property: JsonPropertyName("sets")] IReadOnlyList<BipartiteSetDto> Sets,
    [property: JsonPropertyName("edges")] IReadOnlyList<GraphEdgeDto> Edges) {
    [JsonPropertyName("k")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? K { get; init; }
}

/// <summary>
/// One picture of the bipartite graph. <c>nodes</c> (elements and sets, whose ids never collide) and <c>edges</c> list
/// only elements whose state is not <see cref="PictureState.Background"/>; an id that is absent is Background.
/// <c>hits</c> says how many picked nodes hit each node of the other column, for every node hit at least once: an
/// annotation, not a state (the frontend draws a ×2 badge for a count above 1).
/// </summary>
sealed record BipartiteFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("nodes")] IReadOnlyDictionary<string, StateRef> Nodes,
    [property: JsonPropertyName("edges")] IReadOnlyDictionary<string, StateRef> Edges,
    [property: JsonPropertyName("hits")] IReadOnlyDictionary<string, int> Hits) {
    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }
}

/// <summary>
/// Derives the Bipartite Graph picture (Set Cover, Exact Cover, Exact Hitting Set) from (universe, sets, rule, the picked
/// nodes of one step, step event, focus). Nothing here knows about a particular problem or solver.
/// <para>
/// <b>Ids.</b> Element <c>x</c> is <c>e:{x}</c>, with the name written by <see cref="EscapeName"/> (a <c>\</c> before every
/// <c>\</c>, <c>~</c> and <c>:</c> in it). Set <c>i</c> (0-based, instance order) is <c>S{i+1}</c>: sets are named by
/// position, so two sets with the same elements are two nodes. A membership edge is <c>{elementId}~{setId}</c>
/// (<c>e:1~S2</c>). Set ids hold no name and element ids all start with <c>e:</c>, so no two nodes share an id, and the
/// escaping keeps a separator from ever coming bare from a name. An id is a label, never something to split: every payload
/// edge gives its element as <c>a</c> and its set as <c>b</c>, and every set lists its <c>members</c>.
/// </para>
/// <para>
/// <b>Picks.</b> The column the answer names (<see cref="BipartiteSpec.Pick"/>) holds the <i>picks</i>; the other column holds
/// the <i>constraints</i>, each of which must be hit at least once or exactly once (<see cref="BipartiteSpec.Rule"/>). A
/// solver's steps carry the picks so far: <see cref="ChosenSets"/> of set ids (<c>S1</c>, ...) when the answer picks sets,
/// <see cref="NodeSet"/> of element names when it picks elements.
/// </para>
/// <para>
/// <b>States.</b> A picked node and its edges are <c>Solution</c> in a step that keeps the picks (Accept, Backtrack, Done,
/// and every step of an incremental run) and <c>ElementHighlight</c> in a step that only tries a whole candidate (Try,
/// Reject of a run that is not <see cref="SolveRun.Incremental"/>; see <see cref="GraphSubsetFrames.Picked"/>). A constraint
/// hit by the picks is <c>Covered</c>; under the exactly-once rule a constraint hit more than once is <c>Rejected</c> in
/// every event. A constraint that is not hit is <c>Rejected</c> only when a complete candidate is judged (Done, or a Try or
/// Reject of a whole candidate) and something is picked: while an answer is still being built, and for an empty pick
/// (no answer), nothing is failed yet. Under the exactly-once rule a pick that is not picked but hits a covered constraint
/// would double up, so it is <c>Blocked</c>, except while a whole candidate is only being tried. The focus is
/// <c>ElementHighlight</c>; in a Reject step of an incremental run it is what failed, so <c>Rejected</c>
/// (<see cref="GraphSubsetFrames.FocusMark"/>). A pick in the focus marks its edges too. When several states apply to one
/// element the order is <c>Rejected &gt; ElementHighlight &gt; Solution &gt; Covered &gt; Blocked &gt; Background</c>.
/// </para>
/// </summary>
static class BipartiteFrames {
    public const string KindName = "bipartiteGraph";
    public const string LayoutName = "columns";

    // ---- Ids ----

    /// <summary><c>e:{name}</c>, the name written with <see cref="EscapeName"/>.</summary>
    public static string ElementId(string name) => "e:" + EscapeName(name);

    /// <summary><c>S{index + 1}</c> for the set at 0-based <paramref name="index"/>.</summary>
    public static string SetId(int index) => $"S{index + 1}";

    /// <summary>The id of the membership edge between an element id and a set id: <c>{element}~{set}</c>.</summary>
    public static string EdgeId(string elementId, string setId) => $"{elementId}~{setId}";

    /// <summary>
    /// The membership of element <paramref name="element"/> (its name) in set <paramref name="setIndex"/> (0-based) as a
    /// construction step adds it: its id, the element's id and the set's id.
    /// </summary>
    public static BuildEdge BuildMembership(string element, int setIndex) =>
        new(EdgeId(ElementId(element), SetId(setIndex)), ElementId(element), SetId(setIndex));

    /// <summary>
    /// An element name as it is written inside an id: a backslash before every <c>\</c>, <c>~</c> and <c>:</c> (the
    /// separators of this picture's ids). Ordinary names (<c>1</c>, <c>a</c>) are unchanged. The same rule as
    /// <see cref="GraphSubsetFrames.EscapeName"/>, over this picture's separators.
    /// </summary>
    public static string EscapeName(string name) =>
        name.IndexOfAny(['\\', '~', ':']) < 0 ? name : name.Replace(@"\", @"\\").Replace("~", @"\~").Replace(":", @"\:");

    public static string PickSideName(BipartitePick pick) => pick switch {
        BipartitePick.Sets => "sets",
        BipartitePick.Elements => "elements",
        _ => throw new ArgumentOutOfRangeException(nameof(pick)),
    };

    public static string RuleName(BipartiteRule rule) => rule switch {
        BipartiteRule.AtLeastOnce => "atLeastOnce",
        BipartiteRule.ExactlyOnce => "exactlyOnce",
        _ => throw new ArgumentOutOfRangeException(nameof(rule)),
    };

    /// <summary>The answer shape of a solver's steps for <paramref name="pick"/>: <see cref="ChosenSets"/> or <see cref="NodeSet"/>.</summary>
    public static Type StepShape(BipartitePick pick) => pick == BipartitePick.Sets ? typeof(ChosenSets) : typeof(NodeSet);

    // ---- The model both the payload and the frames read ----

    /// <summary>The graph a spec describes, with the picks, the constraints, and what each pick hits.</summary>
    private sealed class Model {
        public readonly List<string> ElementNames = new();
        public readonly List<List<string>> SetMembers = new();
        public readonly List<(string Id, string A, string B)> Edges = new();
        public readonly List<string> Picks = new();
        public readonly List<string> Constraints = new();
        public readonly Dictionary<string, List<string>> Hits = new(StringComparer.Ordinal);
        public readonly Dictionary<string, List<string>> EdgesOf = new(StringComparer.Ordinal);
        public readonly HashSet<string> NodeIds = new(StringComparer.Ordinal);
        public readonly HashSet<string> EdgeIds = new(StringComparer.Ordinal);
        /// <summary>What a caption calls each node: an element's name, a set's id.</summary>
        public readonly Dictionary<string, string> Names = new(StringComparer.Ordinal);

        public Model(BipartiteSpec spec) {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in spec.Universe)
                if (seen.Add(name)) ElementNames.Add(name);
            foreach (var set in spec.Sets) {
                var members = set.Distinct(StringComparer.Ordinal).ToList();
                SetMembers.Add(members);
                // An element that is in a set but not in the universe still gets a node, so no membership is lost.
                foreach (var name in members)
                    if (seen.Add(name)) ElementNames.Add(name);
            }
            var elementIds = ElementNames.Select(ElementId).ToList();
            var setIds = Enumerable.Range(0, SetMembers.Count).Select(SetId).ToList();
            for (int i = 0; i < elementIds.Count; i++) Names[elementIds[i]] = ElementNames[i];
            foreach (var id in setIds) Names[id] = id;
            foreach (var id in elementIds.Concat(setIds)) {
                NodeIds.Add(id);
                Hits[id] = new List<string>();
                EdgesOf[id] = new List<string>();
            }
            for (int i = 0; i < SetMembers.Count; i++)
                foreach (var name in SetMembers[i]) {
                    string element = ElementId(name), set = setIds[i], edge = EdgeId(element, set);
                    Edges.Add((edge, element, set));
                    EdgeIds.Add(edge);
                    EdgesOf[element].Add(edge);
                    EdgesOf[set].Add(edge);
                    Hits[set].Add(element);
                    Hits[element].Add(set);
                }
            bool pickSets = spec.Pick == BipartitePick.Sets;
            Picks.AddRange(pickSets ? setIds : elementIds);
            Constraints.AddRange(pickSets ? elementIds : setIds);
        }
    }

    // ---- The payload ----

    public static BipartitePayload Payload(BipartiteSpec spec) {
        var m = new Model(spec);
        return new BipartitePayload(KindName, LayoutName, PickSideName(spec.Pick), RuleName(spec.Rule),
            m.ElementNames.Select(n => new GraphNodeDto(ElementId(n), n)).ToList(),
            m.SetMembers.Select((members, i) => new BipartiteSetDto(SetId(i), SetId(i), members.Select(ElementId).ToList())).ToList(),
            m.Edges.Select(e => new GraphEdgeDto(e.Id, e.A, e.B)).ToList()) { K = spec.K };
    }

    // ---- Frames ----

    /// <summary>
    /// The frame for the picked node ids (<see cref="PickIds"/>) at one step. <c>incremental</c> says whether the step comes
    /// from an incremental run (<see cref="SolveRun.Incremental"/>).
    /// </summary>
    public static BipartiteFrame Frame(BipartiteSpec spec, IEnumerable<string> picked, StepEvent ev, string[] focus, string caption, bool? ok = null, bool incremental = false) {
        var m = new Model(spec);
        var picks = new HashSet<string>(m.Picks, StringComparer.Ordinal);
        var chosen = new HashSet<string>(picked.Where(picks.Contains), StringComparer.Ordinal);
        var count = m.Constraints.ToDictionary(c => c, _ => 0, StringComparer.Ordinal);
        foreach (var p in chosen)
            foreach (var c in m.Hits[p]) count[c]++;

        var nodes = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var edges = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var pickedState = GraphSubsetFrames.Picked(ev, incremental);
        foreach (var p in chosen) {
            GraphSubsetFrames.Raise(nodes, p, pickedState);
            foreach (var e in m.EdgesOf[p]) GraphSubsetFrames.Raise(edges, e, pickedState);
        }

        bool exact = spec.Rule == BipartiteRule.ExactlyOnce;
        // A complete candidate is judged; one still being built, or an empty pick (no answer), is not failed yet.
        bool judged = chosen.Count > 0 && (ev == StepEvent.Done || (!incremental && ev is StepEvent.Try or StepEvent.Reject));
        foreach (var c in m.Constraints) {
            int n = count[c];
            if (exact && n > 1) GraphSubsetFrames.Raise(nodes, c, PictureState.Rejected);
            else if (n > 0) GraphSubsetFrames.Raise(nodes, c, PictureState.Covered);
            else if (judged) GraphSubsetFrames.Raise(nodes, c, PictureState.Rejected);
        }

        // Under exactly-once, a pick that would double up on a covered constraint is blocked, unless a whole candidate is
        // only being tried (then nothing is taken, so nothing is blocked).
        if (exact && pickedState == PictureState.Solution)
            foreach (var p in m.Picks)
                if (!chosen.Contains(p) && m.Hits[p].Any(c => count[c] > 0)) GraphSubsetFrames.Raise(nodes, p, PictureState.Blocked);

        var focusMark = incremental ? GraphSubsetFrames.FocusMark(ev) : PictureState.ElementHighlight;
        foreach (var id in focus) {
            if (m.NodeIds.Contains(id)) {
                GraphSubsetFrames.Raise(nodes, id, focusMark);
                if (picks.Contains(id))
                    foreach (var e in m.EdgesOf[id]) GraphSubsetFrames.Raise(edges, e, focusMark);
            } else if (m.EdgeIds.Contains(id)) {
                GraphSubsetFrames.Raise(edges, id, focusMark);
            }
        }

        var hits = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (c, n) in count)
            if (n > 0) hits[c] = n;
        return new BipartiteFrame(ev, caption, focus, GraphSubsetFrames.ToRefs(nodes), GraphSubsetFrames.ToRefs(edges), hits) { Ok = ok };
    }

    /// <summary>
    /// The picked node ids a step's partial names: a <see cref="ChosenSets"/> holds set ids already, a <see cref="NodeSet"/>
    /// holds element names, which become <see cref="ElementId"/>s.
    /// </summary>
    public static IEnumerable<string> PickIds(object partial) => partial switch {
        ChosenSets sets => sets.Sets,
        NodeSet elements => elements.Nodes.Select(ElementId),
        _ => [],
    };

    /// <summary>
    /// The frame that closes a run stopped by its time limit (<see cref="SolveRun.TimedOut"/>): a Done frame with ok = false
    /// in which every element is Background and nothing is hit. See <see cref="GraphSubsetFrames.StoppedFrame"/>.
    /// </summary>
    public static BipartiteFrame StoppedFrame(string caption) =>
        new(StepEvent.Done, caption, [], new SortedDictionary<string, StateRef>(StringComparer.Ordinal),
            new SortedDictionary<string, StateRef>(StringComparer.Ordinal), new SortedDictionary<string, int>(StringComparer.Ordinal)) { Ok = false };

    // ---- Reading answers ----

    /// <summary>
    /// The picks named by a certificate: for an answer that picks sets, a list of sets by contents (<c>{{1,2,3},{4,5}}</c>),
    /// each matched to the first set of the instance with the same elements that is not matched yet (the text is read as a
    /// set, as the verifiers read it, so a set written twice counts once); for one that picks elements, a set of element
    /// names (<c>{1,2}</c>). <c>{}</c> picks nothing. Null when the text cannot be read (an error message, <c>""</c>) or
    /// names a set the instance does not have.
    /// </summary>
    public static string[]? ParseAnswer(BipartiteSpec spec, string answer) {
        string text = answer.Trim();
        if (!text.StartsWith('{') || !text.EndsWith('}')) return null;
        UtilCollection parsed;
        try {
            parsed = new UtilCollection(text);
        } catch {
            return null;
        }
        if (spec.Pick == BipartitePick.Elements)
            return parsed.ToList().Select(item => ElementId(item.ToString())).Distinct(StringComparer.Ordinal).ToArray();

        var contents = spec.Sets.Select(s => new HashSet<string>(s, StringComparer.Ordinal)).ToList();
        var used = new bool[contents.Count];
        var ids = new List<string>();
        foreach (UtilCollection set in parsed.ToList()) {
            var wanted = new HashSet<string>(set.ToList().Select(item => item.ToString()), StringComparer.Ordinal);
            int match = Enumerable.Range(0, contents.Count).FirstOrDefault(i => !used[i] && contents[i].SetEquals(wanted), -1);
            if (match < 0) return null;
            used[match] = true;
            ids.Add(SetId(match));
        }
        return ids.ToArray();
    }

    // ---- Captions and reasons, for solvers to call inside a recorder lambda so they cost nothing when recording is off ----

    /// <summary>"{1,2,3}" for captions.</summary>
    public static string Braces(IEnumerable<string> items) => "{" + string.Join(",", items) + "}";

    /// <summary>"S2 {2,4}": a set's name and its elements, as the instance lists them.</summary>
    public static string SetText(IReadOnlyList<IReadOnlyList<string>> sets, int index) => $"{SetId(index)} {Braces(sets[index])}";

    /// <summary>"4", "4 and 5", "3, 4 and 5".</summary>
    public static string List(IReadOnlyList<string> names) =>
        names.Count <= 1 ? string.Join("", names) : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];

    /// <summary>
    /// A plain reason the picks fail <paramref name="spec"/>'s rule ("4 and 5 aren't covered; 2 is covered more than once"),
    /// or "" when every constraint is hit as the rule asks. Sizes against K are the solver's own business.
    /// <paramref name="picked"/> holds pick ids, as in <see cref="PickIds"/>.
    /// </summary>
    public static string Why(BipartiteSpec spec, IEnumerable<string> picked) {
        var m = new Model(spec);
        var chosen = new HashSet<string>(picked, StringComparer.Ordinal);
        var count = m.Constraints.ToDictionary(c => c, _ => 0, StringComparer.Ordinal);
        foreach (var p in m.Picks.Where(chosen.Contains))
            foreach (var c in m.Hits[p]) count[c]++;
        var missing = m.Constraints.Where(c => count[c] == 0).Select(c => m.Names[c]).ToList();
        var doubled = spec.Rule == BipartiteRule.ExactlyOnce ? m.Constraints.Where(c => count[c] > 1).Select(c => m.Names[c]).ToList() : [];
        string verb = spec.Pick == BipartitePick.Sets ? "covered" : "hit";
        var why = new List<string>();
        if (missing.Count > 0) why.Add($"{List(missing)} {(missing.Count == 1 ? "isn't" : "aren't")} {verb}");
        if (doubled.Count > 0) why.Add($"{List(doubled)} {(doubled.Count == 1 ? "is" : "are")} {verb} more than once");
        return string.Join("; ", why);
    }

    // ---- The whole response ----

    /// <summary>
    /// The caption of a solved frame built from an answer string: the picks; that nothing needs picking when the empty pick
    /// is correct; "No answer was found." when the solver says there is none; and, for text it could not read, that the
    /// answer could not be drawn, so a failure is not passed off as a proof that no answer exists.
    /// </summary>
    internal static string AnswerCaption(BipartiteSpec spec, string[]? picks, bool ok) {
        if (picks is null) return "The solver's answer could not be read, so there is nothing to draw.";
        if (picks.Length > 0) {
            var names = new Model(spec).Names;
            return $"The answer is {Braces(picks.Select(p => names.GetValueOrDefault(p, p)))}.";
        }
        return ok ? "The answer is {}: nothing needs to be picked." : "No answer was found.";
    }

    /// <summary>
    /// The whole response: the payload, a frame per kept step, then a solved frame built from the answer unless the last
    /// step already is the Done step. A run stopped by its time limit ends with <see cref="StoppedFrame"/> instead, and its
    /// answer string (<see cref="SolveRun.TimedOutAnswer"/>) is never read.
    /// </summary>
    public static FramesResponse Build(BipartiteSpec spec, SolveRun run, Func<string, string[]?> parseAnswer, Func<string, bool> verifyAnswer) {
        var frames = new List<object>();
        bool lastIsDone = false;
        foreach (var step in run.StepsFor(StepShape(spec.Pick))) {
            var (partial, ev, focus, caption, ok) = step switch {
                SolverStep<ChosenSets> s => ((object)s.Partial, s.Event, s.Focus, s.Caption, s.Ok),
                SolverStep<NodeSet> s => (s.Partial, s.Event, s.Focus, s.Caption, s.Ok),
                _ => throw new InvalidOperationException($"A Bipartite picture cannot draw a step of type {step.GetType().Name}."),
            };
            frames.Add(run.TimedOut && ev == StepEvent.Done
                ? StoppedFrame(caption)
                : Frame(spec, PickIds(partial), ev, focus, caption, ok, run.Incremental));
            lastIsDone = ev == StepEvent.Done;
        }
        if (run.TimedOut) {
            if (!lastIsDone) frames.Add(StoppedFrame(GraphSubsetFrames.StoppedCaption));
        } else if (!lastIsDone) {
            string[]? picks = parseAnswer(run.Answer);
            bool ok = verifyAnswer(run.Answer);
            frames.Add(Frame(spec, picks ?? [], StepEvent.Done, [], AnswerCaption(spec, picks, ok), ok));
        }
        return new FramesResponse(nameof(VisualizationType.BipartiteGraph), Payload(spec), frames);
    }
}

/// <summary>
/// A visualization for a set problem drawn as a Bipartite Graph (Set Cover, Exact Cover, Exact Hitting Set). The
/// visualization declares only what the problem is (<see cref="Describe"/>); the frames hook then comes for free. Implement
/// members explicitly so nothing new appears in /info. The class must also declare
/// <c>Type? IVisualization.StepShape =&gt; typeof(ChosenSets)</c> for an answer that picks sets, or <c>typeof(NodeSet)</c> for
/// one that picks elements (<see cref="BipartiteFrames.StepShape"/>).
/// </summary>
interface IBipartiteVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    BipartiteSpec Describe(TProblem problem);

    /// <summary>
    /// Reads a solver's answer string as pick ids (see <see cref="BipartiteFrames.ParseAnswer"/>), or null when it cannot be
    /// read. Only used for the last frame of a solver whose steps do not end in a Done step.
    /// </summary>
    string[]? ParseAnswer(TProblem problem, string answer) => BipartiteFrames.ParseAnswer(Describe(problem), answer);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return BipartiteFrames.Build(Describe(problem), run,
            answer => ParseAnswer(problem, answer),
            answer => {
                // The problem's own verifier decides; a certificate it cannot read is not correct.
                try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
            });
    }
}
