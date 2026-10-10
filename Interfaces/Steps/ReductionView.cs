using System.Text.Json;
using System.Text.Json.Nodes;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;

namespace API.Interfaces.Steps;

/// <summary>A Reduction View request the reduction or its two problems cannot serve; the endpoint answers it with a 400.</summary>
sealed class ReductionViewException(string error, string detail) : Exception(detail) {
    /// <summary>The machine-readable error code, e.g. <c>no_source_picture</c>.</summary>
    public string Error { get; } = error;
}

/// <summary>
/// Builds the payload of <c>/ProblemProvider/reductionView</c> (picture type <see cref="VisualizationType.Reduction"/>):
/// the source and target instances, each with its own picture payload, the gadget map between them, and one list of frames in
/// three phases. Nothing here draws: every pane's states come from the problem's own <see cref="IFramesVisualization"/>, so a
/// frontend renders each pane with the renderer it already has and only adds the cross-pane links.
/// <para>
/// <b>Frame.</b> <c>{ phase, event, caption, ok?, bound?, gadgets?, sourceFocus, targetFocus, source, target }</c>. <c>phase</c>
/// is <c>build</c>, <c>solve</c> or <c>map</c>. <c>sourceFocus</c> and <c>targetFocus</c> are ids of the source and target picture
/// (the sides are told apart by field, so ids carry no prefix); a frontend highlights them across the panes and links them
/// through the gadget list. <c>source</c> and <c>target</c> are the states of each pane, exactly the per-step fields of that
/// picture's own frames (<c>nodes</c>, <c>edges</c>, <c>groups</c>, <c>assignment</c>, ...) without the event, caption, focus and
/// ok, which are lifted to the frame.
/// </para>
/// <para>
/// <b>Build.</b> One frame per construction step (<see cref="BuildDelta"/>). The source pane highlights the source ids the step
/// was built from. The target pane highlights the target nodes and edges the step added. Target elements not yet built are
/// hidden by position, not by a state: <c>target.buildOrder</c> lists every target node and edge in the order the steps add them, and
/// a build frame's <c>target.builtNodes</c> / <c>target.builtEdges</c> say how many of each are on screen (the first n). That is
/// stateless, so a frame renders on its own when scrubbed to, and it costs one number per frame where a list of the built ids
/// would repeat a quadratic edge list in every frame. Solve and map frames omit both: the target is complete. <c>target.skipped</c>
/// lists the pairs the rule considered and left without an edge, as <c>{ id, a, b }</c>: they are not edges of the picture, so
/// each gives its two ends (the id is the one the picture would give that edge, never something to split).
/// </para>
/// <para>
/// <b>Solve.</b> The chosen solver runs on the target; each of its steps is drawn by the target's picture and the source pane
/// stays neutral. The last frame's <c>ok</c> is the target problem's own verifier on the solver's answer, except that "{}" is
/// judged by <see cref="IReduction.targetAcceptsEmptyAnswer"/> (the empty set can be a real answer, which some verifiers refuse as
/// unreadable). A solver stopped by the time limit ends the phase with its stopped Done frame (<c>ok</c> false).
/// </para>
/// <para>
/// <b>Map.</b> The backward map's steps, drawn by the source's picture applied to the partial answer; the target pane keeps
/// the solution and highlights the element being read. Each step's <c>targetFocus</c> is the target element read, its
/// <c>sourceFocus</c> the source ids of the smallest gadgets that element belongs to. The last frame's <c>ok</c> is the backward
/// map's own Done verdict: the source problem's verifier on the mapped-back answer. When the solver was stopped there is no
/// answer, so the phase is a single Done frame (<c>ok</c> false) saying there is nothing to map back.
/// </para>
/// </summary>
static class ReductionView {
    public const string Build = "build", Solve = "solve", Map = "map";

    /// <summary>The state fields a frame of a picture carries that are not the frame's own envelope.</summary>
    private static readonly string[] Envelope = ["event", "caption", "focus", "ok"];

    /// <param name="red">The reduction, built from the source instance. It must have build steps and a backward map.</param>
    /// <param name="sourceInstance">The source instance as the caller gave it.</param>
    /// <param name="solver">The solver to run on the target.</param>
    /// <param name="sourceVis">The source problem's picture.</param>
    /// <param name="targetVis">The target problem's picture.</param>
    /// <param name="mapBack">Runs <see cref="IReduction.MapBackRun"/> with steps (the endpoint wraps certificate parse failures).</param>
    /// <param name="solveTimeLimit">The time budget of the solve phase (see <see cref="ISolver.Run"/>); null means no limit.</param>
    public static JsonObject Create(IReduction red, string sourceInstance, ISolver solver,
        IFramesVisualization sourceVis, IFramesVisualization targetVis, Func<string, SolveRun> mapBack, TimeSpan? solveTimeLimit = null) {
        string targetInstance = red.reductionTo.instance;
        Type sourceShape = sourceVis.StepShape ?? throw new ReductionViewException("no_source_picture", $"{red.reductionFrom.problemName}'s picture draws no typed steps.");
        Type targetShape = targetVis.StepShape ?? throw new ReductionViewException("no_target_picture", $"{red.reductionTo.problemName}'s picture draws no typed steps.");
        object emptySource = EmptyAnswer(sourceShape, red.reductionFrom.problemName, "no_source_picture");
        object emptyTarget = EmptyAnswer(targetShape, red.reductionTo.problemName, "no_target_picture");

        // The two pictures' static payloads, and a neutral pane (everything Background) for the side that is at rest.
        var (sourceResponse, neutralSourceFrames) = Render(sourceVis, sourceInstance, [NewStep(sourceShape, emptySource, StepEvent.Try, [], "")], sourceShape);
        var (targetResponse, neutralTargetFrames) = Render(targetVis, targetInstance, [NewStep(targetShape, emptyTarget, StepEvent.Try, [], "")], targetShape);
        JsonObject neutralSource = neutralSourceFrames[0].Pane, neutralTarget = neutralTargetFrames[0].Pane;
        JsonNode targetPayload = ToNode(targetResponse.Payload);
        var ids = new TargetIds(targetPayload);

        var frames = new JsonArray();
        var gadgets = red.gadgets;

        // ---- Build ----
        ConstructionRun built = red.BuildRun(true)!;
        var buildSteps = built.Steps.ToList();
        var nodeOrder = new List<string>();
        var edgeOrder = new List<string>();
        var nodeSeen = new HashSet<string>(StringComparer.Ordinal);
        var edgeSeen = new HashSet<string>(StringComparer.Ordinal);
        var counts = new List<(int Nodes, int Edges)>();
        var targetFocuses = new List<string[]>();
        var targetItems = new List<object>();
        var sourceItems = new List<object>();
        foreach (var step in buildSteps) {
            foreach (string n in step.Partial.Nodes)
                if (ids.Nodes.Contains(n) && nodeSeen.Add(n)) nodeOrder.Add(n);
            var added = new List<string>();
            foreach (var e in step.Partial.Edges)
                if (ids.Edges.Contains(e.Id) && edgeSeen.Add(e.Id)) { edgeOrder.Add(e.Id); added.Add(e.Id); }
            counts.Add((nodeOrder.Count, edgeOrder.Count));
            string[] focus = step.Focus.Concat(added).Distinct(StringComparer.Ordinal).ToArray();
            targetFocuses.Add(focus);
            targetItems.Add(NewStep(targetShape, emptyTarget, StepEvent.Try, focus, ""));
            sourceItems.Add(NewStep(sourceShape, emptySource, StepEvent.Try, step.Partial.SourceFocus, ""));
        }
        // The steps announce the whole target (BuildRun folds the steps past the cap into one), but the closing frame shows
        // every node and edge of the picture whatever the steps said, so a reduction whose steps miss one still ends complete.
        foreach (string n in ids.NodeList) if (nodeSeen.Add(n)) nodeOrder.Add(n);
        foreach (string e in ids.EdgeList) if (edgeSeen.Add(e)) edgeOrder.Add(e);

        var buildTargetPanes = buildSteps.Count == 0 ? new List<Rendered>() : Render(targetVis, targetInstance, targetItems, targetShape).Frames;
        var buildSourcePanes = buildSteps.Count == 0 ? new List<Rendered>() : Render(sourceVis, sourceInstance, sourceItems, sourceShape).Frames;
        for (int i = 0; i < buildSteps.Count; i++) {
            var step = buildSteps[i];
            bool last = i == buildSteps.Count - 1 && step.Event == StepEvent.Done;
            var target = buildTargetPanes[i].Pane;
            target["builtNodes"] = last ? nodeOrder.Count : counts[i].Nodes;
            target["builtEdges"] = last ? edgeOrder.Count : counts[i].Edges;
            if (step.Partial.Skipped.Length > 0)
                target["skipped"] = new JsonArray(step.Partial.Skipped.Select(s => (JsonNode)new JsonObject { ["id"] = s.Id, ["a"] = s.A, ["b"] = s.B }).ToArray());
            var frame = Frame(Build, step.Event, step.Caption, step.Ok, step.Partial.SourceFocus, targetFocuses[i], buildSourcePanes[i].Pane, target);
            if (step.Partial.Bound is int bound) frame["bound"] = bound;
            if (step.Partial.Gadgets.Length > 0) frame["gadgets"] = ToArray(step.Partial.Gadgets);
            frames.Add(frame);
        }
        if (buildSteps.Count == 0 || buildSteps[^1].Event != StepEvent.Done) {
            var closing = neutralTarget.DeepClone().AsObject();
            closing["builtNodes"] = nodeOrder.Count;
            closing["builtEdges"] = edgeOrder.Count;
            frames.Add(Frame(Build, StepEvent.Done, $"{red.reductionTo.problemName} is built.", true, [], [], neutralSource.DeepClone().AsObject(), closing));
        }

        // ---- Solve ----
        SolveRun solved = solver.Run(targetInstance, withSteps: true, solveTimeLimit);
        var solveSteps = solved.StepsFor(targetShape);
        // A stopped run has no answer to draw: if its steps cannot be drawn either (another shape), keep just its stopped Done step.
        if (solved.TimedOut && solveSteps.Count == 0)
            solveSteps = [NewStep(targetShape, emptyTarget, StepEvent.Done, [], ReadStep(solved.Steps[^1]).Caption, false)];
        // A stopped run stays stopped when it is drawn, so the picture closes it with its blank stopped Done frame.
        var solveFrames = Render(targetVis, targetInstance, solveSteps, targetShape, solved.Answer, exact: false, timedOut: solved.TimedOut, incremental: solved.Incremental).Frames;
        for (int i = 0; i < solveFrames.Count; i++) {
            var f = solveFrames[i];
            bool last = i == solveFrames.Count - 1;
            // The solved picture is judged by the problem's own verifier, whatever the solver reported; a stopped solve has no answer.
            bool? ok = last ? !solved.TimedOut && SolvedOk(red, targetInstance, solved.Answer) : f.Ok;
            frames.Add(Frame(Solve, f.Event, f.Caption, ok, [], f.Focus, neutralSource.DeepClone().AsObject(), f.Pane));
        }
        // What the target pane shows while the answer is mapped back: the solved picture. The solver's Done step holds the
        // answer in the picture's own shape; a solver that records no steps leaves only the last solve frame to reuse.
        object? solvedPartial = solved.StepShape == targetShape
            ? solved.Steps.Select(ReadStep).LastOrDefault(s => s.Event == StepEvent.Done)?.Partial
            : null;
        JsonObject solvedPane = solveFrames[^1].Pane;

        // ---- Map ----
        if (solved.TimedOut) {
            frames.Add(Frame(Map, StepEvent.Done, $"The solver had not finished, so there is no {red.reductionTo.problemName} answer to map back.",
                false, [], [], neutralSource.DeepClone().AsObject(), solvedPane.DeepClone().AsObject()));
            return Result(red, sourceInstance, targetInstance, solver, sourceResponse, targetResponse, nodeOrder, edgeOrder, gadgets, frames);
        }
        SolveRun mapped = mapBack(solved.Answer);
        if (mapped.StepShape != sourceShape)
            throw new ReductionViewException("no_source_picture", $"{red.reductionFrom.problemName}'s picture cannot draw the backward map's steps.");
        var mapSteps = mapped.Steps.Select(ReadStep).ToList();
        var sourceFocuses = mapSteps.Select(s => SourceIdsOf(gadgets, s.Focus)).ToList();
        var mapSourceFrames = Render(sourceVis, sourceInstance,
            mapSteps.Select((s, i) => NewStep(sourceShape, s.Partial, s.Event, sourceFocuses[i], s.Caption, s.Ok)).ToList(), sourceShape, mapped.Answer).Frames;
        var mapTargetPanes = solvedPartial == null || mapSteps.Count == 0 ? null
            // Drawn as the solve's Done step (with its ok), so the pane is the solved picture: a Try would draw it as a
            // candidate still being tried (highlighted), not as the answer.
            : Render(targetVis, targetInstance, mapSteps.Select(s => NewStep(targetShape, solvedPartial, StepEvent.Done, s.Focus, "", solveFrames[^1].Ok)).ToList(), targetShape).Frames;
        for (int i = 0; i < mapSteps.Count; i++) {
            var s = mapSteps[i];
            bool last = i == mapSteps.Count - 1;
            // The backward map's Done step already carries the source verifier's verdict (MapBackRun), including
            // when "{}" is a real empty answer, which the verifier alone would refuse as unreadable.
            bool? ok = last && s.Event != StepEvent.Done ? Verify(red.reductionFrom, sourceInstance, mapped.Answer) : s.Ok;
            JsonObject target = mapTargetPanes != null ? mapTargetPanes[i].Pane : solvedPane.DeepClone().AsObject();
            frames.Add(Frame(Map, s.Event, s.Caption, ok, sourceFocuses[i], s.Focus, mapSourceFrames[i].Pane, target));
        }

        if (mapSteps.Count == 0 || mapSteps[^1].Event != StepEvent.Done)
            frames.Add(Frame(Map, StepEvent.Done, $"{red.reductionFrom.problemName} has the answer {mapped.Answer}.",
                Verify(red.reductionFrom, sourceInstance, mapped.Answer), [], [], neutralSource.DeepClone().AsObject(), solvedPane.DeepClone().AsObject()));

        return Result(red, sourceInstance, targetInstance, solver, sourceResponse, targetResponse, nodeOrder, edgeOrder, gadgets, frames);
    }

    private static JsonObject Result(IReduction red, string sourceInstance, string targetInstance, ISolver solver,
        FramesResponse sourceResponse, FramesResponse targetResponse, List<string> nodeOrder, List<string> edgeOrder,
        IReadOnlyList<Gadget> gadgets, JsonArray frames) =>
        new JsonObject {
            ["type"] = nameof(VisualizationType.Reduction),
            ["kind"] = "reduction",
            ["reduction"] = red.GetType().Name,
            ["reductionName"] = red.reductionName,
            ["solver"] = solver.solverName,
            ["source"] = new JsonObject {
                ["problem"] = red.reductionFrom.problemName,
                ["instance"] = sourceInstance,
                ["picture"] = Picture(sourceResponse),
            },
            ["target"] = new JsonObject {
                ["problem"] = red.reductionTo.problemName,
                ["instance"] = targetInstance,
                ["picture"] = Picture(targetResponse),
                ["buildOrder"] = new JsonObject { ["nodes"] = ToArray(nodeOrder), ["edges"] = ToArray(edgeOrder) },
            },
            ["gadgets"] = new JsonArray(gadgets.Select(GadgetJson).ToArray()),
            ["frames"] = frames,
        };

    // ---- Panes ----

    /// <summary>One frame of a picture, taken apart: its envelope, and the rest (its pane).</summary>
    private sealed record Rendered(JsonObject Pane, StepEvent Event, string Caption, string[] Focus, bool? Ok);

    /// <summary>
    /// Draws <paramref name="steps"/> (typed <see cref="SolverStep{TPartial}"/> of <paramref name="shape"/>) with the picture's
    /// own frame builder, one pane per step in order. The builder closes a run that does not end in a Done step with a solved frame
    /// built from <paramref name="answer"/>; that frame is cut when <paramref name="exact"/> (so panes line up with steps) and
    /// kept otherwise (the solve phase wants it). <paramref name="incremental"/> is the solver's
    /// <see cref="SolveRun.Incremental"/>, so the solve phase draws an incremental solver's committed part as the answer so far.
    /// </summary>
    private static (FramesResponse Response, List<Rendered> Frames) Render(
        IFramesVisualization vis, string instance, IReadOnlyList<object> steps, Type shape, string answer = "{}", bool exact = true, bool timedOut = false, bool incremental = false) {
        var response = vis.BuildFrames(instance, new SolveRun(answer, steps.ToList(), shape) { TimedOut = timedOut, Incremental = incremental });
        var frames = new List<Rendered>();
        foreach (object frame in response.Frames) {
            var node = (JsonObject)JsonSerializer.SerializeToNode(frame, frame.GetType(), FramesResponse.JsonOptions)!;
            string ev = node["event"]!.GetValue<string>();
            string caption = node["caption"]!.GetValue<string>();
            string[] focus = node["focus"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray();
            bool? ok = node["ok"]?.GetValue<bool>();
            foreach (string key in Envelope) node.Remove(key);
            frames.Add(new Rendered(node, Enum.Parse<StepEvent>(ev), caption, focus, ok));
        }
        // One-for-one with the steps unless the caller wants the builder's own closing frame: cut what it appended.
        if (exact && steps.Count > 0 && frames.Count > steps.Count) frames.RemoveRange(steps.Count, frames.Count - steps.Count);
        return (response, frames);
    }

    private static JsonObject Picture(FramesResponse response) =>
        new() { ["type"] = response.Type, ["payload"] = ToNode(response.Payload) };

    private static JsonNode ToNode(object value) =>
        JsonSerializer.SerializeToNode(value, value.GetType(), FramesResponse.JsonOptions)!;

    private static JsonArray ToArray<T>(IEnumerable<T> items) =>
        new(items.Select(i => JsonSerializer.SerializeToNode(i)).ToArray());

    private static JsonObject Frame(string phase, StepEvent ev, string caption, bool? ok, IEnumerable<string> sourceFocus,
        IEnumerable<string> targetFocus, JsonObject source, JsonObject target) {
        var frame = new JsonObject {
            ["phase"] = phase,
            ["event"] = ev.ToString(),
            ["caption"] = caption,
        };
        if (ok is bool b) frame["ok"] = b;
        frame["sourceFocus"] = ToArray(sourceFocus);
        frame["targetFocus"] = ToArray(targetFocus);
        frame["source"] = source;
        frame["target"] = target;
        return frame;
    }

    /// <summary>The gadget as the Reduction View reads it: its kind and stable ids, without the legacy fields.</summary>
    private static JsonNode GadgetJson(Gadget g) {
        var node = new JsonObject {
            ["gadgetKind"] = JsonSerializer.SerializeToNode(g.gadgetKind),
            ["sourceIds"] = ToArray(g.sourceIds),
            ["targetIds"] = ToArray(g.targetIds),
        };
        if (g.note != null) node["note"] = g.note;
        return node;
    }

    // ---- Typed steps, by reflection (a step's TPartial is only known as a Type) ----

    private sealed record StepInfo(object Partial, StepEvent Event, string[] Focus, string Caption, bool? Ok);

    private static StepInfo ReadStep(object step) {
        Type t = step.GetType();
        return new StepInfo(t.GetProperty("Partial")!.GetValue(step)!, (StepEvent)t.GetProperty("Event")!.GetValue(step)!,
            (string[])t.GetProperty("Focus")!.GetValue(step)!, (string)t.GetProperty("Caption")!.GetValue(step)!, (bool?)t.GetProperty("Ok")!.GetValue(step));
    }

    private static object NewStep(Type shape, object partial, StepEvent ev, string[] focus, string caption, bool? ok = null) {
        Type t = typeof(SolverStep<>).MakeGenericType(shape);
        object step = Activator.CreateInstance(t, partial, ev, focus, caption)!;
        if (ok != null) t.GetProperty("Ok")!.SetValue(step, ok);
        return step;
    }

    /// <summary>An empty answer in a picture's own shape, so a pane can be drawn at rest (only the focus highlighted).</summary>
    private static object EmptyAnswer(Type shape, string problem, string error) =>
        Shapes.EmptyAnswer(shape) ?? throw new ReductionViewException(error, $"{problem}'s picture draws {shape.Name} answers, which the Reduction View cannot show at rest yet.");

    /// <summary>
    /// Whether the solver's answer is correct for the target. "{}" is the no-answer convention, but for a set-valued target it is
    /// also the empty set, which can be a real answer that the verifier refuses as unreadable; the reduction says which it is.
    /// </summary>
    private static bool SolvedOk(IReduction red, string targetInstance, string answer) =>
        ReductionBack.IsNoAnswer(answer) ? red.targetAcceptsEmptyAnswer() : Verify(red.reductionTo, targetInstance, answer);

    private static bool Verify(IProblem problem, string instance, string answer) {
        // The problem's own verifier decides; a certificate it cannot read (such as "{}") is not correct.
        try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
    }

    /// <summary>
    /// The source ids a target element was built from: the sources of the smallest gadgets (fewest target ids) that contain it,
    /// so a node reads as its own literal, not as the whole clause it sits in.
    /// </summary>
    private static string[] SourceIdsOf(IReadOnlyList<Gadget> gadgets, IEnumerable<string> targetIds) {
        var result = new List<string>();
        foreach (string target in targetIds) {
            var holders = gadgets.Where(g => g.targetIds.Contains(target)).ToList();
            if (holders.Count == 0) continue;
            int smallest = holders.Min(g => g.targetIds.Count);
            foreach (var g in holders.Where(g => g.targetIds.Count == smallest))
                foreach (string id in g.sourceIds)
                    if (!result.Contains(id)) result.Add(id);
        }
        return result.ToArray();
    }

    // ---- The target picture's ids ----

    /// <summary>
    /// The node and edge ids of a Graph payload. A construction step's edges carry the same ids (<see cref="BuildEdge"/>), so
    /// they are looked up, never split.
    /// </summary>
    private sealed class TargetIds {
        public List<string> NodeList { get; } = [];
        public List<string> EdgeList { get; } = [];
        public HashSet<string> Nodes { get; }
        public HashSet<string> Edges { get; }

        public TargetIds(JsonNode payload) {
            foreach (var n in payload["nodes"]?.AsArray() ?? new JsonArray())
                NodeList.Add(n!["id"]!.GetValue<string>());
            foreach (var e in payload["edges"]?.AsArray() ?? new JsonArray())
                EdgeList.Add(e!["id"]!.GetValue<string>());
            Nodes = new HashSet<string>(NodeList, StringComparer.Ordinal);
            Edges = new HashSet<string>(EdgeList, StringComparer.Ordinal);
        }
    }
}
