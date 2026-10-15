using System.Globalization;
using System.Text.Json.Serialization;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Interfaces.Geometry;

/// <summary>
/// What a Convex Hull instance hands the Geometry picture: its points in instance order, with their coordinates. A point
/// listed twice is two points. No ids, no states; the base derives those.
/// </summary>
sealed record GeometrySpec(IReadOnlyList<(double X, double Y)> Points);

// --- JSON shapes (explicit names so the contract does not depend on serializer naming options) ---

/// <summary>A point: its id (<c>P1</c>, by position) and its coordinates.</summary>
sealed record GeometryPointDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y);

/// <summary>The static part of a Geometry picture, sent once per response: the points, drawn to scale on real axes.</summary>
sealed record GeometryPayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("points")] IReadOnlyList<GeometryPointDto> Points);

/// <summary>
/// A hull drawn in a frame: its corners counterclockwise, and its role. <c>left</c> and <c>right</c> are the two hulls being
/// merged (drawn in two group colors); <c>piece</c> is a finished hull waiting to be merged. A role is an annotation, not a
/// state.
/// </summary>
sealed record GeometryPieceDto(
    [property: JsonPropertyName("points")] IReadOnlyList<string> Points,
    [property: JsonPropertyName("role")] string Role);

/// <summary>
/// One picture of the points.
/// <list type="bullet">
/// <item><c>pieces</c>: the hulls on the table, each with its role.</item>
/// <item><c>chain</c>: the chain the merge has built so far, in the order it walked them (drawn as a line).</item>
/// <item><c>hull</c>: the answer's corners in output order (drawn with order badges); empty until the Done frame.</item>
/// <item><c>points</c>: the state of each point whose state is not <see cref="PictureState.Background"/>.</item>
/// </list>
/// </summary>
sealed record GeometryFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("pieces")] IReadOnlyList<GeometryPieceDto> Pieces,
    [property: JsonPropertyName("chain")] IReadOnlyList<string> Chain,
    [property: JsonPropertyName("hull")] IReadOnlyList<string> Hull,
    [property: JsonPropertyName("points")] IReadOnlyDictionary<string, StateRef> Points) {
    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }
}

/// <summary>
/// Derives the Geometry picture (Convex Hull) from (spec, a step's <see cref="HullBuild"/>, step event, focus). Nothing here
/// knows about a particular solver.
/// <para>
/// <b>Ids.</b> The i-th point (0-based) is <c>P{i+1}</c>. Ids are positions, never coordinates, so equal points stay two
/// points and no id needs escaping. A solver works with distinct points, so steps name a repeated point by its first copy
/// (<see cref="FirstOf"/>); every copy takes that copy's state.
/// </para>
/// <para>
/// <b>States</b> (precedence <c>Rejected &gt; ElementHighlight &gt; Solution &gt; Committed &gt; Covered &gt; Blocked &gt; Background</c>; see
/// <see cref="GraphSubsetFrames.Raise"/>):
/// </para>
/// <list type="bullet">
/// <item>A corner of a hull on the table (a piece, or one of the two being merged) and a point on the chain are
/// <c>Committed</c> before the confirmed hull: in every step of an incremental run (<see cref="SolveRun.Incremental"/>) and
/// in an Accept or Backtrack the run goes on from, they are kept so far but not the answer, so Solution colour never shows
/// while the solver is still working (#849, #886); the piece outlines and the chain show the progress too. A whole
/// candidate that is only being tried (Try, Reject of a run that is not incremental) is <c>ElementHighlight</c>, and an
/// Accept that ends the run is <c>Solution</c> (<see cref="GraphSubsetFrames.Picked"/>).</item>
/// <item>A dropped point (found inside a hull, or in the middle of an edge) is <c>Rejected</c>.</item>
/// <item>The focus is <c>ElementHighlight</c>; in a Reject step of an incremental run it is what failed (a point popped from
/// the chain), so <c>Rejected</c> (<see cref="GraphSubsetFrames.FocusMark"/>).</item>
/// <item>In the Done frame the hull's corners are <c>Solution</c> and every other point is <c>Covered</c> (inside or on the
/// hull); when the answer is wrong (ok = false) its corners are <c>Rejected</c> and the rest <c>Background</c>
/// (the shared rule, <see cref="GraphSubsetFrames.Picked"/>).</item>
/// <item>A run stopped by its time limit ends with a blank Done frame, ok = false (<see cref="StoppedFrame"/>).</item>
/// </list>
/// Blocked and Untraveled are not used.
/// </summary>
static class GeometryFrames {
    public const string Kind = "geometry";

    // ---- Ids ----

    /// <summary><c>P{i+1}</c> for the point at 0-based position <paramref name="index"/>.</summary>
    public static string PointId(int index) => $"P{index + 1}";

    /// <summary>
    /// The id of each point's first copy, by position: <c>FirstOf(spec)[i]</c> is <c>P{j+1}</c> for the first j whose
    /// coordinates equal point i's (so i's own id when it is not a repeat).
    /// </summary>
    public static string[] FirstOf(GeometrySpec spec) {
        var first = new Dictionary<(double, double), string>();
        return spec.Points.Select((p, i) => first.TryGetValue(p, out var id) ? id : first[p] = PointId(i)).ToArray();
    }

    /// <summary>The id of the first point at each coordinate, for a solver that works with distinct points.</summary>
    public static Dictionary<(double X, double Y), string> IdsByPoint(GeometrySpec spec) {
        var ids = new Dictionary<(double X, double Y), string>();
        for (int i = 0; i < spec.Points.Count; i++) ids.TryAdd(spec.Points[i], PointId(i));
        return ids;
    }

    // ---- The payload ----

    public static GeometryPayload Payload(GeometrySpec spec) =>
        new(Kind, spec.Points.Select((p, i) => new GeometryPointDto(PointId(i), p.X, p.Y)).ToList());

    // ---- Frames ----

    /// <summary>
    /// The frame for one step. <paramref name="incremental"/> says whether the step comes from an incremental run
    /// (<see cref="SolveRun.Incremental"/>); <paramref name="endsRun"/> whether it is an Accept that ends the run
    /// (<see cref="GraphSubsetFrames.EndsRun"/>).
    /// </summary>
    public static GeometryFrame Frame(GeometrySpec spec, HullBuild partial, StepEvent ev, string[] focus, string caption, bool? ok = null, bool incremental = false, bool endsRun = false) {
        var firstOf = FirstOf(spec);
        var ids = new HashSet<string>(firstOf, StringComparer.Ordinal);
        List<string> Known(IEnumerable<string> list) => list.Where(ids.Contains).ToList();
        var states = new Dictionary<string, PictureState>(StringComparer.Ordinal);

        var pieces = new List<GeometryPieceDto>();
        var chain = Known(partial.Chain);
        var hull = new List<string>();
        if (ev == StepEvent.Done) {
            // The answer: its corners in output order; everything else is inside it, or the answer is wrong.
            if (partial.Pieces.Length == 1) hull = Known(partial.Pieces[0]);
            chain = [];
            foreach (var id in hull) GraphSubsetFrames.Raise(states, id, GraphSubsetFrames.Picked(ev, incremental, endsRun, ok == true));
            if (ok == true && hull.Count > 0)
                foreach (var id in ids) GraphSubsetFrames.Raise(states, id, PictureState.Covered);
        } else {
            pieces.AddRange(partial.Pieces.Select(p => new GeometryPieceDto(Known(p), "piece")));
            if (partial.Left.Length > 0) pieces.Add(new GeometryPieceDto(Known(partial.Left), "left"));
            if (partial.Right.Length > 0) pieces.Add(new GeometryPieceDto(Known(partial.Right), "right"));
            // Mid-run a hull's corners are kept but not the answer yet, so they are Committed (#849, #886); a whole candidate
            // being tried is highlighted.
            var picked = GraphSubsetFrames.Picked(ev, incremental, endsRun, ok == true);
            foreach (var id in pieces.SelectMany(p => p.Points).Concat(chain)) GraphSubsetFrames.Raise(states, id, picked);
            foreach (var id in Known(partial.Dropped)) GraphSubsetFrames.Raise(states, id, PictureState.Rejected);
        }

        var mark = incremental ? GraphSubsetFrames.FocusMark(ev) : PictureState.ElementHighlight;
        foreach (var id in Known(focus)) GraphSubsetFrames.Raise(states, id, mark);

        // A repeated point is drawn as its first copy is.
        for (int i = 0; i < firstOf.Length; i++)
            if (firstOf[i] != PointId(i) && states.TryGetValue(firstOf[i], out var state)) states[PointId(i)] = state;

        return new GeometryFrame(ev, caption, focus, pieces, chain, hull, GraphSubsetFrames.ToRefs(states)) { Ok = ok };
    }

    /// <summary>The blank Done frame, ok = false, that closes a run stopped by its time limit: no hulls, nothing judged.</summary>
    public static GeometryFrame StoppedFrame(string caption) =>
        new(StepEvent.Done, caption, [], [], [], [], new SortedDictionary<string, StateRef>(StringComparer.Ordinal)) { Ok = false };

    /// <summary>
    /// A hull as an answer: its corners (by the id of each corner's first copy) as the only piece, and every other point
    /// dropped. No corners (an empty answer) is no pieces.
    /// </summary>
    public static HullBuild Answer(GeometrySpec spec, IReadOnlyList<(double X, double Y)> corners) {
        var ids = IdsByPoint(spec);
        if (corners.Any(c => !ids.ContainsKey(c))) return new HullBuild([], [], [], [], []);
        var hull = corners.Select(c => ids[c]).ToList();
        var dropped = ids.Values.Except(hull, StringComparer.Ordinal);
        return new HullBuild(hull.Count == 0 ? [] : [hull], [], [], [], dropped);
    }

    // ---- The whole response ----

    /// <summary>
    /// The whole response: the payload, a frame per kept step, then a solved frame built from the answer unless the last step
    /// already is the Done step. A run stopped by its time limit ends with <see cref="StoppedFrame"/> instead, and its answer
    /// string (<see cref="SolveRun.TimedOutAnswer"/>) is never read.
    /// </summary>
    public static FramesResponse Build(GeometrySpec spec, SolveRun run, Func<string, HullBuild> parseAnswer, Func<string, bool> verifyAnswer) {
        var frames = new List<object>();
        SolverStep<HullBuild>? last = null;
        foreach (var (step, endsRun) in GraphSubsetFrames.WithEndsRun(run.StepsFor(typeof(HullBuild)).OfType<SolverStep<HullBuild>>())) {
            frames.Add(run.TimedOut && step.Event == StepEvent.Done
                ? StoppedFrame(step.Caption)
                : Frame(spec, step.Partial, step.Event, step.Focus, step.Caption, step.Ok, run.Incremental, endsRun));
            last = step;
        }
        if (run.TimedOut) {
            if (last is not { Event: StepEvent.Done }) frames.Add(StoppedFrame(GraphSubsetFrames.StoppedCaption));
        } else if (last is not { Event: StepEvent.Done }) {
            bool ok = verifyAnswer(run.Answer);
            frames.Add(Frame(spec, ok ? parseAnswer(run.Answer) : new HullBuild([], [], [], [], []), StepEvent.Done, [],
                ok ? $"The answer is {run.Answer}." : "No answer was found.", ok));
        }
        return new FramesResponse(nameof(VisualizationType.Geometry), Payload(spec), frames);
    }

    // ---- Captions ----

    /// <summary>A number as captions write it, with thousands separators (1,234).</summary>
    public static string Count(int n) => n.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>"a", "a and b", "a, b and c" for captions; past <paramref name="most"/> items, "a, b, c and 12 more".</summary>
    public static string AndList(IReadOnlyList<string> items, int most = 12) =>
        items.Count > most ? string.Join(", ", items.Take(most)) + $" and {items.Count - most} more"
        : items.Count <= 2 ? string.Join(" and ", items) : string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1];
}

/// <summary>
/// A visualization for a problem drawn as the Geometry picture. The visualization declares only what the instance is
/// (<see cref="Describe"/>) and how to read an answer as a hull (<see cref="ParseAnswer"/>); the frames hook then comes for
/// free. Implement members explicitly so nothing new appears in /info. The class must also declare
/// <c>Type? IVisualization.StepShape =&gt; typeof(HullBuild)</c>.
/// </summary>
interface IGeometryVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    GeometrySpec Describe(TProblem problem);

    /// <summary>Reads a solver's answer string as a hull (used only when the run's steps cannot be drawn).</summary>
    HullBuild ParseAnswer(TProblem problem, string answer);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return GeometryFrames.Build(Describe(problem), run,
            answer => ParseAnswer(problem, answer),
            answer => {
                // The problem's own verifier decides; a certificate it cannot read is not correct.
                try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
            });
    }
}
