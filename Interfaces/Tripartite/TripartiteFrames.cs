using System.Globalization;
using System.Text.Json.Serialization;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Interfaces.Tripartite;

/// <summary>One triple of M, as positions in X, Y and Z (0-based).</summary>
sealed record TripartiteTriple(int X, int Y, int Z);

/// <summary>
/// What a 3-Dimensional Matching instance hands the Tripartite Matching picture: the names in X, Y and Z, in instance order
/// (a name may be in more than one of them), and M's triples in instance order (a triple listed twice is two triples). No
/// ids, no states; the base derives those.
/// </summary>
sealed record TripartiteSpec(IReadOnlyList<string> X, IReadOnlyList<string> Y, IReadOnlyList<string> Z, IReadOnlyList<TripartiteTriple> Triples);

// --- JSON shapes (explicit names so the contract does not depend on serializer naming options) ---

/// <summary>An element of X, Y or Z: its id (<c>x1</c>, <c>y1</c>, <c>z1</c>, by position) and its name as the instance writes
/// it.</summary>
sealed record TripartiteElementDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name);

/// <summary>A triple of M: its id (<c>T1</c>, by position) and the ids of its element of X, of Y and of Z.</summary>
sealed record TripartiteTripleDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("x")] string X,
    [property: JsonPropertyName("y")] string Y,
    [property: JsonPropertyName("z")] string Z);

/// <summary>
/// The static part of a Tripartite Matching picture, sent once per response: the three columns X, Y and Z, and M's triples,
/// each a line through one element of each column.
/// </summary>
sealed record TripartitePayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("x")] IReadOnlyList<TripartiteElementDto> X,
    [property: JsonPropertyName("y")] IReadOnlyList<TripartiteElementDto> Y,
    [property: JsonPropertyName("z")] IReadOnlyList<TripartiteElementDto> Z,
    [property: JsonPropertyName("triples")] IReadOnlyList<TripartiteTripleDto> Triples);

/// <summary>
/// One picture of the matching.
/// <list type="bullet">
/// <item><c>chosen</c>: the ids of the triples chosen, in M's order.</item>
/// <item><c>repeats</c>: each element the chosen triples use more than once, with how many times (drawn as a ×2 badge).</item>
/// <item><c>elements</c>: the state of each element whose state is not <see cref="PictureState.Background"/>.</item>
/// <item><c>triples</c>: the state of each triple whose state is not <see cref="PictureState.Background"/>.</item>
/// </list>
/// </summary>
sealed record TripartiteFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("chosen")] IReadOnlyList<string> Chosen,
    [property: JsonPropertyName("repeats")] IReadOnlyDictionary<string, int> Repeats,
    [property: JsonPropertyName("elements")] IReadOnlyDictionary<string, StateRef> Elements,
    [property: JsonPropertyName("triples")] IReadOnlyDictionary<string, StateRef> Triples) {
    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }
}

/// <summary>
/// Derives the Tripartite Matching picture (3-Dimensional Matching) from (spec, a step's <see cref="ChosenTriples"/>, step
/// event, focus). Nothing here knows about a particular solver.
/// <para>
/// <b>Ids.</b> The i-th element (0-based) of X, Y and Z is <c>x{i+1}</c>, <c>y{i+1}</c> and <c>z{i+1}</c>; the i-th triple of
/// M is <c>T{i+1}</c>. Ids are positions, never names, so a name in more than one set, a triple listed twice and names with
/// separators in them all stay distinct, and no id needs escaping.
/// </para>
/// <para>
/// <b>States</b> (precedence <c>Rejected &gt; ElementHighlight &gt; Solution &gt; Committed &gt; Covered &gt; Blocked &gt; Background</c>; see
/// <see cref="GraphSubsetFrames.Raise"/>):
/// </para>
/// <list type="bullet">
/// <item>A chosen triple is <c>ElementHighlight</c> while a whole candidate is only being tried (Try, Reject of a run that is
/// not <see cref="SolveRun.Incremental"/>), <c>Committed</c> while it is kept but not confirmed (in every step of an
/// incremental run, what it has committed, and in an Accept the run goes on from; #886), and <c>Solution</c> only in the
/// confirmed answer: a correct Done, or an Accept that ends the run (<see cref="GraphSubsetFrames.Picked"/>). In a Done frame with ok = false
/// it is <c>Rejected</c>. A chosen triple that shares an element with another chosen triple is <c>Rejected</c>.</item>
/// <item>An element the chosen triples use once is <c>Covered</c>; one they use more than once is <c>Rejected</c>, and is
/// listed in <c>repeats</c>.</item>
/// <item>A triple that is not chosen but shares an element with a chosen one is <c>Blocked</c>: choosing it too would use
/// that element twice.</item>
/// <item>The focus (elements or triples) is <c>ElementHighlight</c>; in a Reject step of an incremental run it is what failed,
/// so <c>Rejected</c> (<see cref="GraphSubsetFrames.FocusMark"/>).</item>
/// <item>A run stopped by its time limit ends with a blank Done frame, ok = false (<see cref="StoppedFrame"/>).</item>
/// </list>
/// Untraveled is not used.
/// </summary>
static class TripartiteFrames {
    public const string Kind = "tripartite";

    // ---- Ids ----

    public static string XId(int index) => $"x{index + 1}";
    public static string YId(int index) => $"y{index + 1}";
    public static string ZId(int index) => $"z{index + 1}";

    /// <summary><c>T{i+1}</c> for the triple at 0-based position <paramref name="index"/> of M.</summary>
    public static string TripleId(int index) => $"T{index + 1}";

    /// <summary>The ids of a triple's three elements: its element of X, of Y and of Z.</summary>
    public static string[] ElementIds(TripartiteTriple t) => [XId(t.X), YId(t.Y), ZId(t.Z)];

    /// <summary>The chosen triples of 0-based positions in M, as a step's partial.</summary>
    public static ChosenTriples Chosen(IEnumerable<int> indices) => new(indices.Select(TripleId));

    // ---- The payload ----

    public static TripartitePayload Payload(TripartiteSpec spec) =>
        new(Kind,
            spec.X.Select((name, i) => new TripartiteElementDto(XId(i), name)).ToList(),
            spec.Y.Select((name, i) => new TripartiteElementDto(YId(i), name)).ToList(),
            spec.Z.Select((name, i) => new TripartiteElementDto(ZId(i), name)).ToList(),
            spec.Triples.Select((t, i) => new TripartiteTripleDto(TripleId(i), XId(t.X), YId(t.Y), ZId(t.Z))).ToList());

    // ---- Frames ----

    // The chosen triples as 0-based positions in M, in M's order; ids that are not triples of this instance are dropped.
    private static List<int> Indices(TripartiteSpec spec, ChosenTriples partial) {
        var chosen = new HashSet<string>(partial.Triples, StringComparer.Ordinal);
        return Enumerable.Range(0, spec.Triples.Count).Where(i => chosen.Contains(TripleId(i))).ToList();
    }

    /// <summary>How many of the given triples use each element they use, keyed by element id.</summary>
    public static Dictionary<string, int> Uses(TripartiteSpec spec, IEnumerable<int> triples) {
        var uses = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (int i in triples)
            foreach (var id in ElementIds(spec.Triples[i]))
                uses[id] = uses.GetValueOrDefault(id) + 1;
        return uses;
    }

    /// <summary>
    /// The frame for one step. <paramref name="incremental"/> says whether the step comes from an incremental run
    /// (<see cref="SolveRun.Incremental"/>); <paramref name="endsRun"/> whether it is an Accept that ends the run
    /// (<see cref="GraphSubsetFrames.EndsRun"/>).
    /// </summary>
    public static TripartiteFrame Frame(TripartiteSpec spec, ChosenTriples partial, StepEvent ev, string[] focus, string caption, bool? ok = null, bool incremental = false, bool endsRun = false) {
        var chosen = Indices(spec, partial);
        var uses = Uses(spec, chosen);
        var elements = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var triples = new Dictionary<string, PictureState>(StringComparer.Ordinal);

        foreach (var (id, n) in uses)
            GraphSubsetFrames.Raise(elements, id, n > 1 ? PictureState.Rejected : PictureState.Covered);

        var picked = GraphSubsetFrames.Picked(ev, incremental, endsRun, ok == true);
        var isChosen = new HashSet<int>(chosen);
        for (int i = 0; i < spec.Triples.Count; i++) {
            var ids = ElementIds(spec.Triples[i]);
            if (isChosen.Contains(i)) {
                GraphSubsetFrames.Raise(triples, TripleId(i), picked);
                if (ids.Any(id => uses[id] > 1)) GraphSubsetFrames.Raise(triples, TripleId(i), PictureState.Rejected);
            } else if (ids.Any(uses.ContainsKey)) {
                GraphSubsetFrames.Raise(triples, TripleId(i), PictureState.Blocked);
            }
        }

        var elementIds = new HashSet<string>(
            spec.X.Select((_, i) => XId(i)).Concat(spec.Y.Select((_, i) => YId(i))).Concat(spec.Z.Select((_, i) => ZId(i))), StringComparer.Ordinal);
        var tripleIds = new HashSet<string>(Enumerable.Range(0, spec.Triples.Count).Select(TripleId), StringComparer.Ordinal);
        var mark = incremental ? GraphSubsetFrames.FocusMark(ev) : PictureState.ElementHighlight;
        foreach (var id in focus) {
            if (elementIds.Contains(id)) GraphSubsetFrames.Raise(elements, id, mark);
            else if (tripleIds.Contains(id)) GraphSubsetFrames.Raise(triples, id, mark);
        }

        var repeats = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (id, n) in uses)
            if (n > 1) repeats[id] = n;
        return new TripartiteFrame(ev, caption, focus, chosen.Select(TripleId).ToList(), repeats,
            GraphSubsetFrames.ToRefs(elements), GraphSubsetFrames.ToRefs(triples)) { Ok = ok };
    }

    /// <summary>The blank Done frame, ok = false, that closes a run stopped by its time limit: nothing chosen.</summary>
    public static TripartiteFrame StoppedFrame(string caption) =>
        new(StepEvent.Done, caption, [], [], new SortedDictionary<string, int>(StringComparer.Ordinal),
            new SortedDictionary<string, StateRef>(StringComparer.Ordinal), new SortedDictionary<string, StateRef>(StringComparer.Ordinal)) { Ok = false };

    /// <summary>
    /// A 3-Dimensional Matching certificate (<c>{{Paul,Austin,Jake},{Sally,Madison,Frank}}</c>, triples by their names) as the
    /// triples of M it names: each listed triple is the first triple of M with those three names that is not taken yet, so a
    /// triple listed twice in M can be chosen twice. Anything that does not name triples of M (<c>{}</c>, no answer, included)
    /// is no triples.
    /// </summary>
    public static ChosenTriples ParseMatching(TripartiteSpec spec, string answer) {
        string s = answer.Trim();
        if (s.Length < 4 || !s.StartsWith("{{", StringComparison.Ordinal) || !s.EndsWith("}}", StringComparison.Ordinal)) return new ChosenTriples([]);
        var taken = new bool[spec.Triples.Count];
        var ids = new List<string>();
        foreach (var listed in s[2..^2].Split("},{")) {
            var names = listed.Split(',');
            if (names.Length != 3) return new ChosenTriples([]);
            int i = Enumerable.Range(0, spec.Triples.Count).FirstOrDefault(j => !taken[j]
                && spec.X[spec.Triples[j].X] == names[0] && spec.Y[spec.Triples[j].Y] == names[1] && spec.Z[spec.Triples[j].Z] == names[2], -1);
            if (i < 0) return new ChosenTriples([]);
            taken[i] = true;
            ids.Add(TripleId(i));
        }
        return new ChosenTriples(ids);
    }

    // ---- The whole response ----

    /// <summary>
    /// The whole response: the payload, a frame per kept step, then a solved frame built from the answer unless the last step
    /// already is the Done step. A run stopped by its time limit ends with <see cref="StoppedFrame"/> instead, and its answer
    /// string (<see cref="SolveRun.TimedOutAnswer"/>) is never read.
    /// </summary>
    public static FramesResponse Build(TripartiteSpec spec, SolveRun run, Func<string, ChosenTriples> parseAnswer, Func<string, bool> verifyAnswer) {
        var frames = new List<object>();
        SolverStep<ChosenTriples>? last = null;
        foreach (var (step, endsRun) in GraphSubsetFrames.WithEndsRun(run.StepsFor(typeof(ChosenTriples)).OfType<SolverStep<ChosenTriples>>())) {
            frames.Add(run.TimedOut && step.Event == StepEvent.Done
                ? StoppedFrame(step.Caption)
                : Frame(spec, step.Partial, step.Event, step.Focus, step.Caption, step.Ok, run.Incremental, endsRun));
            last = step;
        }
        if (run.TimedOut) {
            if (last is not { Event: StepEvent.Done }) frames.Add(StoppedFrame(GraphSubsetFrames.StoppedCaption));
        } else if (last is not { Event: StepEvent.Done }) {
            bool ok = verifyAnswer(run.Answer);
            frames.Add(Frame(spec, ok ? parseAnswer(run.Answer) : new ChosenTriples([]), StepEvent.Done, [],
                ok ? $"The answer is {run.Answer}." : "No answer was found.", ok));
        }
        return new FramesResponse(nameof(VisualizationType.TripartiteMatching), Payload(spec), frames);
    }

    // ---- Captions ----

    /// <summary>A number as captions write it, with thousands separators (1,234).</summary>
    public static string Count(long n) => n.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>"a", "a and b", "a, b and c" for captions.</summary>
    public static string AndList(IReadOnlyList<string> items) =>
        items.Count <= 2 ? string.Join(" and ", items) : string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1];

    /// <summary><c>T2 (Paul, Austin, Jake)</c>: a triple's id and its three names.</summary>
    public static string TripleLabel(TripartiteSpec spec, int index) {
        var t = spec.Triples[index];
        return $"{TripleId(index)} ({spec.X[t.X]}, {spec.Y[t.Y]}, {spec.Z[t.Z]})";
    }

    /// <summary>
    /// An element's name for a caption: just its name, or its name and set (<c>1 in Y</c>) when the name is in more than one
    /// of X, Y and Z.
    /// </summary>
    public static string ElementName(TripartiteSpec spec, string id) {
        var (set, names) = id[0] switch { 'x' => ("X", spec.X), 'y' => ("Y", spec.Y), _ => ("Z", spec.Z) };
        string name = names[int.Parse(id.AsSpan(1), CultureInfo.InvariantCulture) - 1];
        int sets = (spec.X.Contains(name) ? 1 : 0) + (spec.Y.Contains(name) ? 1 : 0) + (spec.Z.Contains(name) ? 1 : 0);
        return sets > 1 ? $"{name} in {set}" : name;
    }
}

/// <summary>
/// A visualization for 3-Dimensional Matching drawn as the Tripartite Matching picture. The visualization declares only what
/// the instance is (<see cref="Describe"/>) and how to read an answer as chosen triples (<see cref="ParseAnswer"/>); the frames
/// hook then comes for free. Implement members explicitly so nothing new appears in /info. The class must also declare
/// <c>Type? IVisualization.StepShape =&gt; typeof(ChosenTriples)</c>.
/// </summary>
interface ITripartiteVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    TripartiteSpec Describe(TProblem problem);

    /// <summary>Reads a solver's answer string as chosen triples (used only when the run's steps cannot be drawn).</summary>
    ChosenTriples ParseAnswer(TProblem problem, string answer);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return TripartiteFrames.Build(Describe(problem), run,
            answer => ParseAnswer(problem, answer),
            answer => {
                // The problem's own verifier decides; a certificate it cannot read is not correct.
                try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
            });
    }
}
