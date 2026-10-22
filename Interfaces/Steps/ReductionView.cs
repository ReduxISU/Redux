using System.Text.Json;
using System.Text.Json.Nodes;
using API.Interfaces.JSON_Objects;

namespace API.Interfaces.Steps;

/// <summary>A Reduction View request the reduction or its two problems cannot serve; the endpoint answers it with a 400.</summary>
sealed class ReductionViewException(string error, string detail) : Exception(detail) {
    /// <summary>The machine-readable error code, e.g. <c>no_source_picture</c>.</summary>
    public string Error { get; } = error;
}

/// <summary>
/// The backward map could not read the answer the solver gave for the target. The input was fine (the solver produced the
/// answer, not the caller), so the endpoint answers it with a 500 that names the solver and its answer, and logs it.
/// </summary>
sealed class SolverAnswerUnmappableException(string solver, string answer, string detail, Exception inner) : Exception(detail, inner) {
    /// <summary>The solver's display name.</summary>
    public string Solver { get; } = solver;
    /// <summary>The answer the backward map could not read.</summary>
    public string Answer { get; } = answer;
}

/// <summary>
/// Builds the payload of <c>/ProblemProvider/reductionView</c> (picture type <see cref="VisualizationType.Reduction"/>):
/// the source and target instances, each with its own picture payload, the gadget map between them, and one list of frames in
/// three phases. Nothing here draws: every pane's states come from the problem's own <see cref="IFramesVisualization"/>, so a
/// frontend renders each pane with the renderer it already has and only adds the cross-pane links.
/// <para>
/// <b>Frame.</b> <c>{ phase, event, caption, step, ok?, bound?, gadgets?, sourceFocus, targetFocus, source, target }</c>. <c>phase</c>
/// is <c>build</c>, <c>solve</c> or <c>map</c>. <c>sourceFocus</c> and <c>targetFocus</c> are ids of the source and target picture
/// (the sides are told apart by field, so ids carry no prefix); a frontend highlights them across the panes and links them
/// through the gadget list. <c>source</c> and <c>target</c> are the states of each pane, exactly the per-step fields of that
/// picture's own frames (<c>nodes</c>, <c>edges</c>, <c>groups</c>, <c>assignment</c>, ...) without the event, caption, focus and
/// ok, which are lifted to the frame.
/// </para>
/// <para>
/// <b>Step.</b> Every frame carries <c>step</c> (#895), always written: the 0-based index of the step it was drawn from in its
/// own phase's list: the construction steps in <c>build</c>, the solver run's full step list in <c>solve</c> (as in every
/// <c>format=frames</c> response), the backward map's steps in <c>map</c>. It is null for a frame drawn from no step: the
/// solved frame the target's picture adds, a stopped solve whose steps the picture cannot draw, and the Done the view adds
/// at the end of the map. The panes never carry it: it belongs to the frame.
/// </para>
/// <para>
/// <b>Build.</b> One frame per construction step (<see cref="BuildDelta"/>). The source pane highlights the source ids the step
/// was built from. The target pane highlights the target nodes and edges the step added. Both panes are drawn at rest (nothing
/// chosen, nothing judged), so no element is Solution or Rejected while the target is being built. Target elements not yet built are
/// hidden by position, not by a state: <c>target.buildOrder</c> lists every target node and edge (the picture's pieces: see <see cref="BuildDelta"/>) in the order the steps add them, and
/// a build frame's <c>target.builtNodes</c> / <c>target.builtEdges</c> say how many of each are on screen (the first n). That is
/// stateless, so a frame renders on its own when scrubbed to, and it costs one number per frame where a list of the built ids
/// would repeat a quadratic edge list in every frame. The steps announce the whole target (past the cap the rest are folded into
/// one step), so the build ends showing all of it, on a plain "target built" frame: the construction's own Done step is drawn
/// as an Accept without <c>ok</c>, since nothing has been solved yet (#840). Solve and map frames omit both: the target is complete. <c>target.skipped</c>
/// lists the pairs the rule considered and left without an edge, as <c>{ id, a, b }</c>: they are not edges of the picture, so
/// each gives its two ends (the id is the one the picture would give that edge, never something to split).
/// </para>
/// <para>
/// <b>Another picture (#912).</b> The endpoint lets the caller choose either side's picture (<c>source.visualization</c> and
/// <c>target.visualization</c> say which was drawn). The construction steps name the pieces of the target problem's default
/// picture, so <c>target.buildReveal</c> says how a build frame shows the target: <c>pieces</c> (by <c>buildOrder</c>, as
/// above), or <c>whole</c> when the chosen picture lacks some of those pieces (a factor graph has no literal nodes, a search
/// tree none of the instance): then <c>buildOrder</c> is empty, build frames carry no counts, and the picture is shown as it
/// is while the steps' captions and focus still say what is built. A picture that has every node the steps add and no edges at
/// all (the Formula, #925) is revealed by its nodes alone: <c>pieces</c>, with no edges in <c>buildOrder</c> and
/// <c>builtEdges</c> 0. A search tree is empty until the solve. A picture of the
/// search (one that cannot draw an answer alone) keeps the solve's last pane through the map phase.
/// </para>
/// <para>
/// <b>Ids of another picture (#929).</b> A reduction names both sides' pieces by their default pictures' ids (in its gadgets,
/// construction steps and backward map). A chosen picture that names them otherwise says how
/// (<see cref="IFramesVisualization.IdsFromDefaultPicture"/>: the 0-1 program's Formula names the Table's <c>r1c0</c> as
/// <c>r0-x0</c>), and every gadget id and focus id of that side, and the focus its panes are drawn with, is written in the
/// chosen picture's ids. buildOrder stays in the steps' own ids, so the reveal rules above are unchanged. An edge a build step
/// adds is focused only in a picture that draws it (the Formula has no edges), and a build step's source focus, in a source
/// picture other than the default, keeps only the pieces that picture has. The default pictures' ids are never changed.
/// </para>
/// <para>
/// <b>Solve.</b> The chosen solver runs on the target; each of its steps is drawn by the target's picture and the source pane
/// stays neutral. The phase does not end the view, so its last frame is not a Done and has no <c>ok</c> (#840): it is an Accept
/// when the target problem's own verifier accepts the solver's answer ("{}" included: a set-valued problem's verifier reads it
/// as the empty set, which can be a real answer) and a Reject otherwise. Its pane is the solver's closing picture, so a solver
/// stopped by the time limit ends the phase with its blank stopped pane, as a Reject.
/// </para>
/// <para>
/// <b>Map.</b> The backward map's steps, drawn by the source's picture applied to the partial answer as an incremental run (the
/// part mapped so far stays Committed until the map ends on it, #886; only the element being read is highlighted, or Rejected when
/// it is left out); the target pane keeps
/// the solution and highlights the element being read. Each step's <c>targetFocus</c> is the target element read, its
/// <c>sourceFocus</c> the source ids of the smallest gadgets that element belongs to. A step about a source element no target
/// element stands for (a variable no clique node mentions) focuses that source id itself (an id no gadget lists as a target id):
/// it goes to <c>sourceFocus</c> as it is, not to <c>targetFocus</c>. The last frame's <c>ok</c> is the backward
/// map's own Done verdict: the source problem's verifier on the mapped-back answer. When the solver was stopped there is no
/// answer, so the phase is a single Done frame (<c>ok</c> false) saying there is nothing to map back. When the backward map
/// cannot read the solver's answer, <see cref="Create"/> throws <see cref="SolverAnswerUnmappableException"/>.
/// </para>
/// <para>
/// <b>One verdict.</b> The view has exactly one Done frame, the last one (the end of the map phase), and it is the only frame
/// with <c>ok</c>: a player shows a verdict only once the answer has been mapped back and checked on the source (#840).
/// </para>
/// </summary>
static class ReductionView {
    public const string Build = "build", Solve = "solve", Map = "map";

    /// <summary>
    /// How many steps the endpoint keeps of the target solve (<see cref="ISolver.Run"/>'s step cap), against the
    /// <see cref="StepRecorder{TPartial}.DefaultCap"/> of 150 everywhere else. The Solve phase was about 89% of a brute-force
    /// view's bytes; 20 steps still show how the search goes, and past them the recorder keeps the last Accept and the Done
    /// says how many were not shown (#910).
    /// </summary>
    public const int SolveStepCap = 20;

    /// <summary>The state fields a frame of a picture carries that are not the frame's own envelope.</summary>
    private static readonly string[] Envelope = ["event", "caption", "focus", "ok", "step"];

    /// <param name="red">The reduction, built from the source instance. It must have a backward map.</param>
    /// <param name="sourceInstance">The source instance as the caller gave it.</param>
    /// <param name="buildSteps">The construction steps recorded while <paramref name="red"/> was built (the endpoint builds once
    /// and records on the way), ending in Done, past the cap folded as <see cref="ConstructionSteps.Limit"/> does. They must
    /// announce every node and edge of the target: the view shows what they say and adds nothing.</param>
    /// <param name="solver">The solver to run on the target.</param>
    /// <param name="sourceVis">The source problem's picture.</param>
    /// <param name="targetVis">The target problem's picture.</param>
    /// <param name="mapBack">Runs <see cref="IReduction.MapBackRun"/> with steps (the endpoint wraps certificate parse failures).</param>
    /// <param name="solveTimeLimit">The time budget of the solve phase (see <see cref="ISolver.Run"/>); null means no limit.</param>
    /// <param name="solveStepCap">The step cap of the solve phase (see <see cref="ISolver.Run"/>); null means the recorder's default.
    /// The endpoint passes <see cref="SolveStepCap"/>.</param>
    public static JsonObject Create(IReduction red, string sourceInstance, IReadOnlyList<SolverStep<BuildDelta>> buildSteps, ISolver solver,
        IFramesVisualization sourceVis, IFramesVisualization targetVis, Func<string, SolveRun> mapBack, TimeSpan? solveTimeLimit = null,
        int? solveStepCap = null) {
        string targetInstance = red.reductionTo.instance;
        Type sourceShape = sourceVis.StepShape ?? throw new ReductionViewException("no_source_picture", $"{red.reductionFrom.problemName}'s picture draws no typed steps.");
        Type targetShape = targetVis.StepShape ?? throw new ReductionViewException("no_target_picture", $"{red.reductionTo.problemName}'s picture draws no typed steps.");
        object emptySource = EmptyAnswer(sourceShape, red.reductionFrom.problemName, "no_source_picture");
        object emptyTarget = EmptyAnswer(targetShape, red.reductionTo.problemName, "no_target_picture");

        // The two pictures' static payloads, and a neutral pane (everything Background) for the side that is at rest.
        var (sourceResponse, neutralSourceFrames) = Render(sourceVis, sourceInstance, [AtRest(sourceShape, emptySource, [])], sourceShape);
        var (targetResponse, _) = Render(targetVis, targetInstance, [AtRest(targetShape, emptyTarget, [])], targetShape);
        JsonObject neutralSource = neutralSourceFrames[0].Pane;

        var frames = new JsonArray();
        var gadgets = red.gadgets;
        // The ids the reduction names are its default pictures'; a chosen picture that names them otherwise maps them (#929).
        var toSource = IdMap(sourceVis, red.reductionFrom, sourceInstance);
        var toTarget = IdMap(targetVis, red.reductionTo, targetInstance);
        bool defaultPicture = targetVis.GetType() == red.reductionTo.defaultVisualization?.GetType();
        var pictureIds = PayloadIds(targetResponse.Payload);
        // A source picture other than the default draws only the pieces it has: a build step's source focus keeps those (#929).
        HashSet<string>? sourcePictureIds = sourceVis.GetType() == red.reductionFrom.defaultVisualization?.GetType() ? null : PayloadIds(sourceResponse.Payload);

        // ---- Build ----
        // buildOrder is exactly what the steps add, in their order. Nothing is filled in from the picture, so steps that miss an
        // element (or name it by an id the picture does not have) give a target that never completes, which the tests check.
        var nodeOrder = new List<string>();
        var edgeOrder = new List<string>();
        var nodeSeen = new HashSet<string>(StringComparer.Ordinal);
        var edgeSeen = new HashSet<string>(StringComparer.Ordinal);
        var counts = new List<(int Nodes, int Edges)>();
        var targetFocuses = new List<string[]>();
        var targetItems = new List<object>();
        var sourceItems = new List<object>();
        var sourceFocuses = new List<string[]>();
        foreach (var step in buildSteps) {
            foreach (string n in step.Partial.Nodes)
                if (nodeSeen.Add(n)) nodeOrder.Add(n);
            var added = new List<string>();
            foreach (var e in step.Partial.Edges)
                if (edgeSeen.Add(e.Id)) { edgeOrder.Add(e.Id); added.Add(e.Id); }
            counts.Add((nodeOrder.Count, edgeOrder.Count));
            // An edge the step adds is focused only where the picture draws it: another picture may have no such edge (#929).
            var addedShown = Translate(toTarget, added).Where(e => defaultPicture || pictureIds.Contains(e));
            string[] focus = Translate(toTarget, step.Focus).Concat(addedShown).Distinct(StringComparer.Ordinal).ToArray();
            targetFocuses.Add(focus);
            targetItems.Add(AtRest(targetShape, emptyTarget, focus));
            string[] sourceFocus = Translate(toSource, step.Partial.SourceFocus).Where(id => sourcePictureIds?.Contains(id) ?? true).ToArray();
            sourceFocuses.Add(sourceFocus);
            sourceItems.Add(AtRest(sourceShape, emptySource, sourceFocus));
        }

        // The construction steps name the target's pieces by the ids of its problem's default picture. Another picture the
        // caller chose (#912) may draw other pieces (a factor graph has no literal nodes, a search tree none of the instance):
        // unless it has every piece the steps add, it cannot be revealed piece by piece, so it is shown whole during the build
        // (buildReveal "whole", an empty buildOrder and no counts) while the steps still say, and focus, what is built.
        // A picture with no edges at all (the Formula: clauses, literals and variables, #925) that has every node the steps add
        // is revealed by those nodes alone: the edges the steps add are not pieces of it, so buildOrder lists none of them and
        // builtEdges stays 0.
        bool nodesOnly = !defaultPicture && !nodeOrder.Concat(edgeOrder).All(pictureIds.Contains)
            && !HasEdges(targetResponse.Payload) && nodeOrder.All(pictureIds.Contains);
        bool revealPieces = defaultPicture || nodesOnly || nodeOrder.Concat(edgeOrder).All(pictureIds.Contains);
        if (!revealPieces) {
            nodeOrder.Clear();
            edgeOrder.Clear();
        } else if (nodesOnly) {
            edgeOrder.Clear();
            counts = counts.Select(c => (c.Nodes, 0)).ToList();
        }

        var buildTargetPanes = buildSteps.Count == 0 ? new List<Rendered>() : Render(targetVis, targetInstance, targetItems, targetShape).Frames;
        var buildSourcePanes = buildSteps.Count == 0 ? new List<Rendered>() : Render(sourceVis, sourceInstance, sourceItems, sourceShape).Frames;
        for (int i = 0; i < buildSteps.Count; i++) {
            var step = buildSteps[i];
            var target = buildTargetPanes[i].Pane;
            if (revealPieces) {
                target["builtNodes"] = counts[i].Nodes;
                target["builtEdges"] = counts[i].Edges;
            }
            if (step.Partial.Skipped.Length > 0)
                target["skipped"] = new JsonArray(step.Partial.Skipped.Select(s => (JsonNode)new JsonObject { ["id"] = s.Id, ["a"] = s.A, ["b"] = s.B }).ToArray());
            // The construction's Done only says the target is built: drawn as a plain Accept, and no build frame has a verdict.
            var ev = step.Event == StepEvent.Done ? StepEvent.Accept : step.Event;
            var frame = Frame(Build, ev, step.Caption, i, null, sourceFocuses[i], targetFocuses[i], buildSourcePanes[i].Pane, target);
            if (step.Partial.Bound is int bound) frame["bound"] = bound;
            if (step.Partial.Gadgets.Length > 0) frame["gadgets"] = ToArray(step.Partial.Gadgets);
            frames.Add(frame);
        }

        // ---- Solve ----
        SolveRun solved = solver.Run(targetInstance, withSteps: true, solveTimeLimit, solveStepCap);
        var solveSteps = solved.StepsFor(targetShape);
        // A stopped run has no answer to draw: if its steps cannot be drawn either (another shape), keep just its stopped Done step.
        bool solveStepsDrawn = solveSteps.Count > 0;
        if (solved.TimedOut && solveSteps.Count == 0)
            solveSteps = [NewStep(targetShape, emptyTarget, StepEvent.Done, [], ReadStep(solved.Steps[^1]).Caption, false)];
        // A stopped run stays stopped when it is drawn, so the picture closes it with its blank stopped Done frame.
        var solveFrames = Render(targetVis, targetInstance, solveSteps, targetShape, solved.Answer, exact: false, timedOut: solved.TimedOut, incremental: solved.Incremental).Frames;
        // The solved picture is judged by the problem's own verifier, whatever the solver reported; a stopped solve has no answer.
        bool solvedOk = !solved.TimedOut && Verify(red.reductionTo, targetInstance, solved.Answer);
        for (int i = 0; i < solveFrames.Count; i++) {
            var f = solveFrames[i];
            bool last = i == solveFrames.Count - 1;
            // The map phase follows, so the solve does not end the view: its closing frame is an Accept or a Reject, not a Done.
            var ev = last ? (solvedOk ? StepEvent.Accept : StepEvent.Reject) : f.Event == StepEvent.Done ? StepEvent.Accept : f.Event;
            // The frame's step indexes the solver run, unless it was drawn from the stand-in stopped step.
            frames.Add(Frame(Solve, ev, f.Caption, solveStepsDrawn ? f.Step : null, null, [], f.Focus, neutralSource.DeepClone().AsObject(), f.Pane));
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
                null, false, [], [], neutralSource.DeepClone().AsObject(), solvedPane.DeepClone().AsObject()));
            return Result(red, sourceInstance, targetInstance, solver, solved.Seed, sourceResponse, targetResponse, nodeOrder, edgeOrder, revealPieces,
                gadgets.Select(g => GadgetJson(g, toSource, toTarget)), frames);
        }
        SolveRun mapped;
        try {
            mapped = mapBack(solved.Answer);
        } catch (Exception ex) when (ex is ReductionInputException or CertificateParseException || ParseGuard.IsCertificateParseFailure(ex)) {
            // The answer came from the solver, not the caller, so this is not bad input: it is the solver and the backward map
            // disagreeing about the target's certificate format.
            throw new SolverAnswerUnmappableException(solver.solverName, solved.Answer, ex.Message, ex);
        }
        if (mapped.StepShape != sourceShape)
            throw new ReductionViewException("no_source_picture", $"{red.reductionFrom.problemName}'s picture cannot draw the backward map's steps.");
        var mapSteps = mapped.Steps.Select(ReadStep).ToList();
        // A focus id that no gadget lists as a target id names a source element no target element stands for (see
        // IReversibleReduction): it is drawn on the source side only.
        var gadgetTargets = new HashSet<string>(gadgets.SelectMany(g => g.targetIds), StringComparer.Ordinal);
        var mapTargetDefault = mapSteps.Select(s => s.Focus.Where(gadgetTargets.Contains).ToArray()).ToList();
        // Worked out in the default pictures' ids (the gadgets'), then written in the chosen pictures' (#929).
        var mapSourceFocuses = mapSteps.Select((s, i) => Translate(toSource, SourceIdsOf(gadgets, mapTargetDefault[i])
            .Concat(s.Focus.Where(id => !gadgetTargets.Contains(id))).Distinct(StringComparer.Ordinal))).ToList();
        var mapTargetFocuses = mapTargetDefault.Select(ids => Translate(toTarget, ids)).ToList();
        // The backward map builds A's answer one element at a time, so it is drawn as an incremental run: what it has mapped so
        // far stays Committed in every step until the map ends on it (#886), and only the element being read is highlighted
        // (Rejected when it is left out).
        var mapSourceFrames = Render(sourceVis, sourceInstance,
            mapSteps.Select((s, i) => NewStep(sourceShape, s.Partial, s.Event, mapSourceFocuses[i], s.Caption, s.Ok)).ToList(), sourceShape, mapped.Answer,
            incremental: true).Frames;
        // A picture of the search itself (the Search Tree) draws nothing from an answer alone, so it keeps the solve's last pane.
        var mapTargetPanes = solvedPartial == null || mapSteps.Count == 0 || !targetVis.DrawsAnswerAlone() ? null
            // Drawn as the solve's Done step (with its ok), so the pane is the solved picture: a Try would draw it as a
            // candidate still being tried (highlighted), not as the answer.
            : Render(targetVis, targetInstance, mapSteps.Select((s, i) => NewStep(targetShape, solvedPartial, StepEvent.Done, mapTargetFocuses[i], "", solveFrames[^1].Ok)).ToList(), targetShape).Frames;
        for (int i = 0; i < mapSteps.Count; i++) {
            var s = mapSteps[i];
            bool last = i == mapSteps.Count - 1;
            // The backward map's Done step already carries the source verifier's verdict (MapBackRun). Only the view's last
            // frame, a Done, has a verdict: a map that does not end in Done gets one added below.
            bool closes = last && s.Event == StepEvent.Done;
            bool? ok = closes ? s.Ok ?? Verify(red.reductionFrom, sourceInstance, mapped.Answer) : null;
            var ev = s.Event == StepEvent.Done && !closes ? StepEvent.Accept : s.Event;
            JsonObject target = mapTargetPanes != null ? mapTargetPanes[i].Pane : solvedPane.DeepClone().AsObject();
            frames.Add(Frame(Map, ev, s.Caption, i, ok, mapSourceFocuses[i], mapTargetFocuses[i], mapSourceFrames[i].Pane, target));
        }

        if (mapSteps.Count == 0 || mapSteps[^1].Event != StepEvent.Done)
            frames.Add(Frame(Map, StepEvent.Done, $"{red.reductionFrom.problemName} has the answer {mapped.Answer}.",
                null, Verify(red.reductionFrom, sourceInstance, mapped.Answer), [], [], neutralSource.DeepClone().AsObject(), solvedPane.DeepClone().AsObject()));

        return Result(red, sourceInstance, targetInstance, solver, solved.Seed, sourceResponse, targetResponse, nodeOrder, edgeOrder, revealPieces,
                gadgets.Select(g => GadgetJson(g, toSource, toTarget)), frames);
    }

    // seed: the seed a randomized solver ran with (SolveRun.Seed, #932), written after "solver"; no "seed" for any other solver.
    private static JsonObject Result(IReduction red, string sourceInstance, string targetInstance, ISolver solver, int? seed,
        FramesResponse sourceResponse, FramesResponse targetResponse, List<string> nodeOrder, List<string> edgeOrder, bool revealPieces,
        IEnumerable<JsonNode> gadgets, JsonArray frames) {
        var view = new JsonObject {
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
                // "pieces" when buildOrder reveals the picture piece by piece, "whole" when it is shown whole (#912).
                ["buildReveal"] = revealPieces ? "pieces" : "whole",
            },
            ["gadgets"] = new JsonArray(gadgets.ToArray()),
            ["frames"] = frames,
        };
        if (seed is int used) view.Insert(view.IndexOf("solver") + 1, "seed", used);
        return view;
    }

    // ---- Panes ----

    /// <summary>One frame of a picture, taken apart: its envelope, and the rest (its pane).</summary>
    private sealed record Rendered(JsonObject Pane, StepEvent Event, string Caption, string[] Focus, bool? Ok, int? Step);

    /// <summary>
    /// Draws <paramref name="steps"/> (typed <see cref="SolverStep{TPartial}"/> of <paramref name="shape"/>) with the picture's
    /// own frame builder, one pane per step in order. The builder closes a run that does not end in a Done step with a solved frame
    /// built from <paramref name="answer"/>; that frame is cut when <paramref name="exact"/> (so panes line up with steps) and
    /// kept otherwise (the solve phase wants it). <paramref name="incremental"/> is the solver's
    /// <see cref="SolveRun.Incremental"/>, so the solve phase draws an incremental solver's committed part as kept so far
    /// (Committed, #886).
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
            int? step = node["step"]?.GetValue<int>();
            foreach (string key in Envelope) node.Remove(key);
            frames.Add(new Rendered(node, Enum.Parse<StepEvent>(ev), caption, focus, ok, step));
        }
        // One-for-one with the steps unless the caller wants the builder's own closing frame: cut what it appended.
        if (exact && steps.Count > 0 && frames.Count > steps.Count) frames.RemoveRange(steps.Count, frames.Count - steps.Count);
        return (response, frames);
    }

    private static JsonObject Picture(FramesResponse response) =>
        new() { ["type"] = response.Type, ["payload"] = ToNode(response.Payload) };

    /// <summary>Every <c>id</c> a picture payload names, at any depth: the pieces its renderer can show or hide.</summary>
    private static HashSet<string> PayloadIds(object payload) {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<JsonNode?>([ToNode(payload)]);
        while (stack.Count > 0) {
            switch (stack.Pop()) {
                case JsonObject o:
                    foreach (var (key, value) in o) {
                        if (key == "id" && value is JsonValue v && v.TryGetValue<string>(out var id)) ids.Add(id);
                        else stack.Push(value);
                    }
                    break;
                case JsonArray a:
                    foreach (var item in a) stack.Push(item);
                    break;
            }
        }
        return ids;
    }

    /// <summary>Whether a picture payload has an <c>edges</c> list: a picture without one draws no edges to reveal.</summary>
    private static bool HasEdges(object payload) => ToNode(payload) is JsonObject o && o.ContainsKey("edges");

    private static JsonNode ToNode(object value) =>
        JsonSerializer.SerializeToNode(value, value.GetType(), FramesResponse.JsonOptions)!;

    private static JsonArray ToArray<T>(IEnumerable<T> items) =>
        new(items.Select(i => JsonSerializer.SerializeToNode(i)).ToArray());

    private static JsonObject Frame(string phase, StepEvent ev, string caption, int? step, bool? ok, IEnumerable<string> sourceFocus,
        IEnumerable<string> targetFocus, JsonObject source, JsonObject target) {
        var frame = new JsonObject {
            ["phase"] = phase,
            ["event"] = ev.ToString(),
            ["caption"] = caption,
            ["step"] = step,
        };
        if (ok is bool b) frame["ok"] = b;
        frame["sourceFocus"] = ToArray(sourceFocus);
        frame["targetFocus"] = ToArray(targetFocus);
        frame["source"] = source;
        frame["target"] = target;
        return frame;
    }

    /// <summary>
    /// The gadget as the Reduction View reads it: its kind and stable ids, without the legacy fields, each side's ids in that
    /// side's chosen picture (#929).
    /// </summary>
    private static JsonNode GadgetJson(Gadget g, Func<string, IReadOnlyList<string>>? toSource, Func<string, IReadOnlyList<string>>? toTarget) {
        var node = new JsonObject {
            ["gadgetKind"] = JsonSerializer.SerializeToNode(g.gadgetKind),
            ["sourceIds"] = ToArray(Translate(toSource, g.sourceIds)),
            ["targetIds"] = ToArray(Translate(toTarget, g.targetIds)),
        };
        if (g.note != null) node["note"] = g.note;
        return node;
    }

    /// <summary>
    /// How one side's chosen picture names the ids of its problem's default picture (#929): null for the default picture itself
    /// and for a picture that shares its ids.
    /// </summary>
    private static Func<string, IReadOnlyList<string>>? IdMap(IFramesVisualization vis, IProblem problem, string instance) =>
        vis.GetType() == problem.defaultVisualization?.GetType() ? null : vis.IdsFromDefaultPicture(instance);

    /// <summary><paramref name="ids"/> (the default picture's) in the chosen picture's ids, in order and once each.</summary>
    private static string[] Translate(Func<string, IReadOnlyList<string>>? map, IEnumerable<string> ids) =>
        map == null ? ids.ToArray() : ids.SelectMany(map).Distinct(StringComparer.Ordinal).ToArray();

    // ---- Typed steps, by reflection (a step's TPartial is only known as a Type) ----

    private sealed record StepInfo(object Partial, StepEvent Event, string[] Focus, string Caption, bool? Ok);

    private static StepInfo ReadStep(object step) {
        Type t = step.GetType();
        return new StepInfo(t.GetProperty("Partial")!.GetValue(step)!, (StepEvent)t.GetProperty("Event")!.GetValue(step)!,
            (string[])t.GetProperty("Focus")!.GetValue(step)!, (string)t.GetProperty("Caption")!.GetValue(step)!, (bool?)t.GetProperty("Ok")!.GetValue(step));
    }

    /// <summary>
    /// A pane at rest: nothing chosen, only <paramref name="focus"/> highlighted. Drawn as an Accept (a set still being built),
    /// so a picture that judges whole candidates never fails the empty one (an empty cover would mark every edge Rejected).
    /// </summary>
    private static object AtRest(Type shape, object empty, string[] focus) => NewStep(shape, empty, StepEvent.Accept, focus, "");

    private static object NewStep(Type shape, object partial, StepEvent ev, string[] focus, string caption, bool? ok = null) {
        Type t = typeof(SolverStep<>).MakeGenericType(shape);
        object step = Activator.CreateInstance(t, partial, ev, focus, caption)!;
        if (ok != null) t.GetProperty("Ok")!.SetValue(step, ok);
        return step;
    }

    /// <summary>An empty answer in a picture's own shape, so a pane can be drawn at rest (only the focus highlighted).</summary>
    private static object EmptyAnswer(Type shape, string problem, string error) =>
        Shapes.EmptyAnswer(shape) ?? throw new ReductionViewException(error, $"{problem}'s picture draws {shape.Name} answers, which the Reduction View cannot show at rest yet.");

    private static bool Verify(IProblem problem, string instance, string answer) {
        // The problem's own verifier decides; a certificate it cannot read is not correct.
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
}
