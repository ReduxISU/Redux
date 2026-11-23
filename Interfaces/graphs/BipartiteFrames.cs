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
    BipartiteRule Rule) {
    /// <summary>
    /// Throws a plain error naming <paramref name="describedBy"/>'s Describe when this spec cannot be right: a null list, name
    /// or set; a set member that is not in the universe (every set problem's grammar makes each set a subset of U, so this
    /// means Describe passed the wrong lists); a K below zero; a pick or rule that is not one of its enum's values. The
    /// builder calls it; it changes nothing for a valid spec.
    /// </summary>
    public void Validate(string? describedBy = null) {
        var problems = new List<string>();
        SpecCheck.NoNulls(problems, "Universe", Universe);
        SpecCheck.NoNulls(problems, "Sets", Sets);
        if (Universe != null && Sets != null && !Sets.Any(s => s is null)) {
            var universe = new HashSet<string>(Universe.Where(u => u != null), StringComparer.Ordinal);
            for (int i = 0; i < Sets.Count; i++) {
                SpecCheck.NoNulls(problems, $"set {SetPieces.SetId(i)}", Sets[i]);
                var outside = Sets[i].Where(m => m != null && !universe.Contains(m)).Distinct(StringComparer.Ordinal).ToList();
                if (outside.Count > 0)
                    problems.Add($"set {SetPieces.SetId(i)} holds {string.Join(", ", outside.Take(5))}, which {(outside.Count == 1 ? "is" : "are")} not in the universe");
            }
        }
        SpecCheck.NotNegative(problems, "K", K);
        SpecCheck.Defined(problems, "Pick", Pick);
        SpecCheck.Defined(problems, "Rule", Rule);
        SpecCheck.ThrowIfAny(nameof(BipartiteSpec), describedBy, problems);
    }
}

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
    /// <summary>The solver step this frame was drawn from (see <see cref="FramesResponse"/>, #895); null for an added frame. Always written.</summary>
    [JsonPropertyName("step")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? Step { get; init; }

    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }
}

/// <summary>
/// Derives the Bipartite Graph picture (Set Cover, Exact Cover, Exact Hitting Set) from (universe, sets, rule, the picked
/// nodes of one step, step event, focus). Nothing here knows about a particular problem or solver.
/// <para>
/// <b>Ids.</b> The pieces are named by <see cref="SetPieces"/>: element <c>x</c> is <c>e:{x}</c>, set <c>i</c> (0-based) is
/// <c>S{i+1}</c> and a membership edge is <c>{elementId}~{setId}</c> (<c>e:1~S2</c>). An id is a label, never something
/// to split: every payload edge gives its element as <c>a</c> and its set as <c>b</c>, and every set lists its
/// <c>members</c>.
/// </para>
/// <para>
/// <b>Picks.</b> The column the answer names (<see cref="BipartiteSpec.Pick"/>) holds the <i>picks</i>; the other column holds
/// the <i>constraints</i>, each of which must be hit at least once or exactly once (<see cref="BipartiteSpec.Rule"/>). A
/// solver's steps carry the picks so far: <see cref="ChosenSets"/> of set ids (<c>S1</c>, ...) when the answer picks sets,
/// <see cref="NodeSet"/> of element names when it picks elements.
/// </para>
/// <para>
/// <b>States.</b> A picked node and its edges are <c>Solution</c> in the confirmed answer (the Done, or an Accept that ends
/// the run), <c>Rejected</c> in a Done with ok = false (the run found nothing), <c>Committed</c> in another step that keeps the picks (an Accept mid-run, a Backtrack, and every step of an
/// incremental run; #886) and <c>ElementHighlight</c> in a step that only tries a whole candidate (Try,
/// Reject of a run that is not <see cref="SolveRun.Incremental"/>; see <see cref="GraphSubsetFrames.Picked"/>). A constraint
/// hit by the picks is <c>Covered</c>; under the exactly-once rule a constraint hit more than once is <c>Rejected</c> in
/// every event. A constraint that is not hit is <c>Rejected</c> only when a complete candidate is judged (Done, or a Try or
/// Reject of a whole candidate) and something is picked: while an answer is still being built, and for an empty pick
/// (no answer), nothing is failed yet. Under the exactly-once rule a pick that is not picked but hits a covered constraint
/// would double up, so it is <c>Blocked</c>, except while a whole candidate is only being tried. The focus is
/// <c>ElementHighlight</c>; in a Reject step of an incremental run it is what failed, so <c>Rejected</c>
/// (<see cref="GraphSubsetFrames.FocusMark"/>). A pick in the focus marks its edges too. When several states apply to one
/// element the order is <c>Rejected &gt; ElementHighlight &gt; Solution &gt; Committed &gt; Covered &gt; Blocked &gt; Background</c>.
/// These rules and their order are one table, <see cref="BipartiteStates"/>, which also names the rule behind each state.
/// </para>
/// </summary>
static class BipartiteFrames {
    public const string KindName = "bipartiteGraph";
    public const string LayoutName = "columns";

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
            var elementIds = ElementNames.Select(SetPieces.ElementId).ToList();
            var setIds = Enumerable.Range(0, SetMembers.Count).Select(SetPieces.SetId).ToList();
            for (int i = 0; i < elementIds.Count; i++) Names[elementIds[i]] = ElementNames[i];
            foreach (var id in setIds) Names[id] = id;
            foreach (var id in elementIds.Concat(setIds)) {
                Hits[id] = new List<string>();
            }
            for (int i = 0; i < SetMembers.Count; i++)
                foreach (var name in SetMembers[i]) {
                    string element = SetPieces.ElementId(name), set = setIds[i], edge = SetPieces.EdgeId(element, set);
                    Edges.Add((edge, element, set));
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
            m.ElementNames.Select(n => new GraphNodeDto(SetPieces.ElementId(n), n)).ToList(),
            m.SetMembers.Select((members, i) => new BipartiteSetDto(SetPieces.SetId(i), SetPieces.SetId(i), members.Select(SetPieces.ElementId).ToList())).ToList(),
            m.Edges.Select(e => new GraphEdgeDto(e.Id, e.A, e.B)).ToList()) { K = spec.K };
    }

    // ---- Frames ----

    /// <summary>
    /// The frame for the picked node ids (<see cref="PickIds"/>) at one step. <c>incremental</c> says whether the step comes
    /// from an incremental run (<see cref="SolveRun.Incremental"/>); <c>endsRun</c> whether it is an Accept that ends the run
    /// (<see cref="GraphSubsetFrames.EndsRun"/>).
    /// </summary>
    public static BipartiteFrame Frame(BipartiteSpec spec, IEnumerable<string> picked, StepEvent ev, string[] focus, string caption, bool? ok = null, bool incremental = false, bool endsRun = false) =>
        Traced(spec, picked, ev, focus, caption, ok, incremental, endsRun).Frame;

    /// <summary>
    /// <see cref="Frame"/>, with the rule behind every piece's state: for each node and edge id, its state and the name of the
    /// <see cref="BipartiteStates"/> rule that gave it (Background pieces included, as <see cref="BipartiteStates.NoRule"/>).
    /// A developer's tool for explaining a colour (the tests' <c>BipartiteTrace</c>); the frame is the same either way.
    /// </summary>
    internal static (BipartiteFrame Frame, IReadOnlyDictionary<string, (PictureState State, string Rule)> Why) Traced(
        BipartiteSpec spec, IEnumerable<string> picked, StepEvent ev, string[] focus, string caption, bool? ok = null, bool incremental = false, bool endsRun = false) {
        var m = new Model(spec);
        var picks = new HashSet<string>(m.Picks, StringComparer.Ordinal);
        var chosen = new HashSet<string>(picked.Where(picks.Contains), StringComparer.Ordinal);
        var count = m.Constraints.ToDictionary(c => c, _ => 0, StringComparer.Ordinal);
        foreach (var p in chosen)
            foreach (var c in m.Hits[p]) count[c]++;
        var inFocus = new HashSet<string>(focus, StringComparer.Ordinal);

        // Every piece's facts, then one table lookup each (BipartiteStates holds the rules and their precedence).
        var step = new BipartiteStepFacts(spec.Rule, ev, incremental, endsRun, ok == true, chosen.Count);
        var why = new Dictionary<string, (PictureState State, string Rule)>(StringComparer.Ordinal);
        var nodes = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var edges = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        void Set(Dictionary<string, PictureState> into, string id, BipartitePieceFacts piece) {
            var (state, rule) = BipartiteStates.Of(piece, step);
            why[id] = (state, rule);
            if (state != PictureState.Background) into[id] = state;
        }
        foreach (var p in m.Picks)
            Set(nodes, p, new(BipartitePieceKind.Pick, Chosen: chosen.Contains(p), TouchesCovered: m.Hits[p].Any(c => count[c] > 0), InFocus: inFocus.Contains(p)));
        foreach (var c in m.Constraints)
            Set(nodes, c, new(BipartitePieceKind.Constraint, Hits: count[c], InFocus: inFocus.Contains(c)));
        foreach (var (id, element, set) in m.Edges) {
            // An edge belongs to the pick at one of its ends: its set when the answer picks sets, else its element.
            string pick = spec.Pick == BipartitePick.Sets ? set : element;
            Set(edges, id, new(BipartitePieceKind.Edge, Chosen: chosen.Contains(pick), InFocus: inFocus.Contains(id) || inFocus.Contains(pick)));
        }

        var hits = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (c, n) in count)
            if (n > 0) hits[c] = n;
        var frame = new BipartiteFrame(ev, caption, focus, GraphSubsetFrames.ToRefs(nodes), GraphSubsetFrames.ToRefs(edges), hits) { Ok = ok };
        return (frame, why);
    }

    /// <summary>
    /// The picked node ids a step's partial names: a <see cref="ChosenSets"/> holds set ids already, a <see cref="NodeSet"/>
    /// holds element names, which become <see cref="SetPieces.ElementId"/>s.
    /// </summary>
    public static IEnumerable<string> PickIds(object partial) => partial switch {
        ChosenSets sets => sets.Sets,
        NodeSet elements => elements.Nodes.Select(SetPieces.ElementId),
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
            return parsed.ToList().Select(item => SetPieces.ElementId(item.ToString())).Distinct(StringComparer.Ordinal).ToArray();

        var contents = spec.Sets.Select(s => new HashSet<string>(s, StringComparer.Ordinal)).ToList();
        var used = new bool[contents.Count];
        var ids = new List<string>();
        foreach (UtilCollection set in parsed.ToList()) {
            var wanted = new HashSet<string>(set.ToList().Select(item => item.ToString()), StringComparer.Ordinal);
            int match = Enumerable.Range(0, contents.Count).FirstOrDefault(i => !used[i] && contents[i].SetEquals(wanted), -1);
            if (match < 0) return null;
            used[match] = true;
            ids.Add(SetPieces.SetId(match));
        }
        return ids.ToArray();
    }

    // ---- Reasons, for solvers to call inside a recorder lambda so they cost nothing when recording is off ----

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
        if (missing.Count > 0) why.Add($"{Words.List(missing)} {(missing.Count == 1 ? "isn't" : "aren't")} {verb}");
        if (doubled.Count > 0) why.Add($"{Words.List(doubled)} {(doubled.Count == 1 ? "is" : "are")} {verb} more than once");
        return string.Join("; ", why);
    }

    /// <summary>
    /// What a brute force's Reject says of picks the verifier turned down: <see cref="Why"/>'s reason, or "the verifier
    /// rejects it" when every constraint is hit as the rule asks (the verifier said no for a reason of its own). For a
    /// <c>$"Try {...}: {WhyRejected(spec, picked)}."</c> hole, so it runs only for a kept step.
    /// </summary>
    public static string WhyRejected(BipartiteSpec spec, IEnumerable<string> picked) =>
        Why(spec, picked) is { Length: > 0 } why ? why : "the verifier rejects it";

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
            return $"The answer is {Words.Braces(picks.Select(p => names.GetValueOrDefault(p, p)))}.";
        }
        return ok ? "The answer is {}: nothing needs to be picked." : "No answer was found.";
    }

    /// <summary>
    /// The whole response: the payload, a frame per kept step, then a solved frame built from the answer unless the last
    /// step already is the Done step. A run stopped by its time limit ends with <see cref="StoppedFrame"/> instead, and its
    /// answer string (<see cref="SolveRun.TimedOutAnswer"/>) is never read. Given <paramref name="declaredBy"/> (the
    /// visualization whose frames these are), its declared <see cref="IVisualization.StepShape"/> must be the shape the spec's
    /// pick draws, or this throws: the endpoints decide which runs a picture can draw by the declared shape and this reads the
    /// steps by the pick, so the two can never quietly disagree.
    /// </summary>
    public static FramesResponse Build(BipartiteSpec spec, SolveRun run, Func<string, string[]?> parseAnswer, Func<string, bool> verifyAnswer,
                                       IVisualization? declaredBy = null) =>
        BuildTraced(spec, run, parseAnswer, verifyAnswer, declaredBy).Response;

    /// <summary>
    /// <see cref="Build"/>, with the rule behind every piece's state in every frame (<see cref="Traced"/>; a stopped frame has
    /// none). A developer's tool for explaining a colour; the response is the same either way.
    /// </summary>
    internal static (FramesResponse Response, List<IReadOnlyDictionary<string, (PictureState State, string Rule)>> Why) BuildTraced(
        BipartiteSpec spec, SolveRun run, Func<string, string[]?> parseAnswer, Func<string, bool> verifyAnswer, IVisualization? declaredBy = null) {
        spec.Validate(declaredBy?.GetType().Name);
        var why = new List<IReadOnlyDictionary<string, (PictureState State, string Rule)>>();
        BipartiteFrame Draw(IEnumerable<string> picked, StepEvent ev, string[] focus, string caption, bool? ok, bool incremental = false, bool endsRun = false) {
            var (frame, rules) = Traced(spec, picked, ev, focus, caption, ok, incremental, endsRun);
            why.Add(rules);
            return frame;
        }
        BipartiteFrame Stopped(string caption) {
            why.Add(new Dictionary<string, (PictureState, string)>(StringComparer.Ordinal));
            return StoppedFrame(caption);
        }
        var frames = new List<object>();
        bool lastIsDone = false;
        Type shape = StepShape(spec.Pick);
        if (declaredBy != null && declaredBy.StepShape != shape)
            throw new InvalidOperationException(
                $"{declaredBy.GetType().Name} declares StepShape {declaredBy.StepShape?.Name ?? "null"}, but its Describe picks " +
                $"{PickSideName(spec.Pick)}, which draws {shape.Name} steps. Make the two agree (see BipartiteFrames.StepShape).");
        var steps = run.StepsFor(shape).Select(step => step switch {
            SolverStep<ChosenSets> s => ((object)s.Partial, s.Event, s.Focus, s.Caption, s.Ok),
            SolverStep<NodeSet> s => (s.Partial, s.Event, s.Focus, s.Caption, s.Ok),
            _ => throw new InvalidOperationException($"A Bipartite picture cannot draw a step of type {step.GetType().Name}."),
        }).ToList();
        for (int i = 0; i < steps.Count; i++) {
            var (partial, ev, focus, caption, ok) = steps[i];
            bool endsRun = i + 1 < steps.Count && GraphSubsetFrames.EndsRun(ev, steps[i + 1].Event, steps[i + 1].Ok);
            frames.Add((run.TimedOut && ev == StepEvent.Done
                ? Stopped(caption)
                : Draw(PickIds(partial), ev, focus, caption, ok, run.Incremental, endsRun)) with { Step = i });
            lastIsDone = ev == StepEvent.Done;
        }
        if (run.TimedOut) {
            if (!lastIsDone) frames.Add(Stopped(GraphSubsetFrames.StoppedCaption));
        } else if (!lastIsDone) {
            string[]? picks = parseAnswer(run.Answer);
            bool ok = verifyAnswer(run.Answer);
            frames.Add(Draw(picks ?? [], StepEvent.Done, [], AnswerCaption(spec, picks, ok), ok));
        }
        return (new FramesResponse(nameof(VisualizationType.BipartiteGraph), Payload(spec), frames), why);
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
    /// <summary>
    /// The steps this picture draws, which depend on what <see cref="Describe"/> picks (<see cref="BipartiteFrames.StepShape"/>).
    /// Abstract again here, so a visualization that forgets it does not compile (CS0535) instead of quietly drawing only the
    /// answer; the builder checks it against the pick.
    /// </summary>
    abstract Type? IVisualization.StepShape { get; }

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
            }, this);
    }
}
