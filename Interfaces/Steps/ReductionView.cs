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
/// lists edge ids the rule considered and left out.
/// </para>
/// <para>
/// <b>Solve.</b> The chosen solver runs on the target; each of its steps is drawn by the target's picture and the source pane
/// stays neutral. The last frame's <c>ok</c> is the target problem's own verifier on the solver's answer.
/// </para>
/// <para>
/// <b>Map.</b> The backward map's steps, drawn by the source's picture applied to the partial answer; the target pane keeps
/// the solution and highlights the element being read. Each step's <c>targetFocus</c> is the target element read, its
/// <c>sourceFocus</c> the source ids of the smallest gadgets that element belongs to. The last frame's <c>ok</c> is the source
/// problem's verifier on the mapped-back answer.
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
    public static JsonObject Create(IReduction red, string sourceInstance, ISolver solver,
        IFramesVisualization sourceVis, IFramesVisualization targetVis, Func<string, SolveRun> mapBack) {
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
            foreach (string e in step.Partial.Edges)
                if (ids.Canonical(e) is string id && ids.Edges.Contains(id) && edgeSeen.Add(id)) { edgeOrder.Add(id); added.Add(id); }
            counts.Add((nodeOrder.Count, edgeOrder.Count));
            string[] focus = step.Focus.Concat(added).Distinct(StringComparer.Ordinal).ToArray();
            targetFocuses.Add(focus);
            targetItems.Add(NewStep(targetShape, emptyTarget, StepEvent.Try, focus, ""));
            sourceItems.Add(NewStep(sourceShape, emptySource, StepEvent.Try, step.Partial.SourceFocus, ""));
        }
        // The recorder keeps a bounded number of steps, so the steps may not announce everything; the closing frame always
        // shows the whole target.
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
                target["skipped"] = ToArray(step.Partial.Skipped.Select(s => ids.Canonical(s) ?? s));
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
        SolveRun solved = solver.Run(targetInstance, withSteps: true);
        var solveFrames = Render(targetVis, targetInstance, solved.StepsFor(targetShape), targetShape, solved.Answer, exact: false).Frames;
        for (int i = 0; i < solveFrames.Count; i++) {
            var f = solveFrames[i];
            bool last = i == solveFrames.Count - 1;
            // The solved picture is judged by the problem's own verifier, whatever the solver reported.
            bool? ok = last ? Verify(red.reductionTo, targetInstance, solved.Answer) : f.Ok;
            frames.Add(Frame(Solve, f.Event, f.Caption, ok, [], f.Focus, neutralSource.DeepClone().AsObject(), f.Pane));
        }
        // What the target pane shows while the answer is mapped back: the solved picture. The solver's Done step holds the
        // answer in the picture's own shape; a solver that records no steps leaves only the last solve frame to reuse.
        object? solvedPartial = solved.StepShape == targetShape
            ? solved.Steps.Select(ReadStep).LastOrDefault(s => s.Event == StepEvent.Done)?.Partial
            : null;
        JsonObject solvedPane = solveFrames[^1].Pane;

        // ---- Map ----
        SolveRun mapped = mapBack(solved.Answer);
        if (mapped.StepShape != sourceShape)
            throw new ReductionViewException("no_source_picture", $"{red.reductionFrom.problemName}'s picture cannot draw the backward map's steps.");
        var mapSteps = mapped.Steps.Select(ReadStep).ToList();
        var sourceFocuses = mapSteps.Select(s => SourceIdsOf(gadgets, s.Focus)).ToList();
        var mapSourceFrames = Render(sourceVis, sourceInstance,
            mapSteps.Select((s, i) => NewStep(sourceShape, s.Partial, s.Event, sourceFocuses[i], s.Caption, s.Ok)).ToList(), sourceShape, mapped.Answer).Frames;
        var mapTargetPanes = solvedPartial == null || mapSteps.Count == 0 ? null
            : Render(targetVis, targetInstance, mapSteps.Select(s => NewStep(targetShape, solvedPartial, StepEvent.Try, s.Focus, "")).ToList(), targetShape).Frames;
        for (int i = 0; i < mapSteps.Count; i++) {
            var s = mapSteps[i];
            bool last = i == mapSteps.Count - 1;
            bool? ok = last ? Verify(red.reductionFrom, sourceInstance, mapped.Answer) : s.Ok;
            JsonObject target = mapTargetPanes != null ? mapTargetPanes[i].Pane : solvedPane.DeepClone().AsObject();
            frames.Add(Frame(Map, s.Event, s.Caption, ok, sourceFocuses[i], s.Focus, mapSourceFrames[i].Pane, target));
        }

        if (mapSteps.Count == 0 || mapSteps[^1].Event != StepEvent.Done)
            frames.Add(Frame(Map, StepEvent.Done, $"{red.reductionFrom.problemName} has the answer {mapped.Answer}.",
                Verify(red.reductionFrom, sourceInstance, mapped.Answer), [], [], neutralSource.DeepClone().AsObject(), solvedPane.DeepClone().AsObject()));

        return new JsonObject {
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
    }

    // ---- Panes ----

    /// <summary>One frame of a picture, taken apart: its envelope, and the rest (its pane).</summary>
    private sealed record Rendered(JsonObject Pane, StepEvent Event, string Caption, string[] Focus, bool? Ok);

    /// <summary>
    /// Draws <paramref name="steps"/> (typed <see cref="SolverStep{TPartial}"/> of <paramref name="shape"/>) with the picture's
    /// own frame builder, one pane per step in order. The builder closes a run that does not end in a Done step with a solved frame
    /// built from <paramref name="answer"/>; that frame is cut when <paramref name="exact"/> (so panes line up with steps) and
    /// kept otherwise (the solve phase wants it).
    /// </summary>
    private static (FramesResponse Response, List<Rendered> Frames) Render(
        IFramesVisualization vis, string instance, IReadOnlyList<object> steps, Type shape, string answer = "{}", bool exact = true) {
        var response = vis.BuildFrames(instance, new SolveRun(answer, steps.ToList(), shape));
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
    private static object EmptyAnswer(Type shape, string problem, string error) {
        if (shape == typeof(NodeSet)) return new NodeSet([]);
        if (shape == typeof(EdgeSet)) return new EdgeSet([]);
        if (shape == typeof(Groups)) return new Groups([]);
        if (shape == typeof(Assignment)) return new Assignment([]);
        if (shape == typeof(ChosenSets)) return new ChosenSets([]);
        if (shape == typeof(Tour)) return new Tour([]);
        throw new ReductionViewException(error, $"{problem}'s picture draws {shape.Name} answers, which the Reduction View cannot show at rest yet.");
    }

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
    /// The node and edge ids of a Graph payload, and a way to turn a step's edge ("a-b", either order) into the payload's id.
    /// Node names may contain '-', so an edge is split only where both halves are nodes.
    /// </summary>
    private sealed class TargetIds {
        public List<string> NodeList { get; } = [];
        public List<string> EdgeList { get; } = [];
        public HashSet<string> Nodes { get; }
        public HashSet<string> Edges { get; }
        private readonly Dictionary<string, string> _raw = new(StringComparer.Ordinal);

        public TargetIds(JsonNode payload) {
            foreach (var n in payload["nodes"]?.AsArray() ?? new JsonArray())
                NodeList.Add(n!["id"]!.GetValue<string>());
            foreach (var e in payload["edges"]?.AsArray() ?? new JsonArray()) {
                string id = e!["id"]!.GetValue<string>(), a = e["a"]!.GetValue<string>(), b = e["b"]!.GetValue<string>();
                EdgeList.Add(id);
                _raw[a + "-" + b] = id;
                _raw[b + "-" + a] = id;
            }
            Nodes = new HashSet<string>(NodeList, StringComparer.Ordinal);
            Edges = new HashSet<string>(EdgeList, StringComparer.Ordinal);
        }

        /// <summary>The picture's id for an edge written "a-b", or the canonical id of two known nodes even if the picture has no such edge, or null.</summary>
        public string? Canonical(string edge) {
            if (_raw.TryGetValue(edge, out string? id)) return id;
            for (int i = 1; i < edge.Length - 1; i++)
                if (edge[i] == '-' && Nodes.Contains(edge[..i]) && Nodes.Contains(edge[(i + 1)..]))
                    return GraphSubsetFrames.EdgeId(edge[..i], edge[(i + 1)..]);
            return null;
        }
    }
}
