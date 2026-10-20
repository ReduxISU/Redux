using System.Text.Json.Serialization;
using API.Interfaces.Steps;

namespace API.Interfaces.Graphs;

// --- JSON shapes (explicit names so the contract does not depend on serializer naming options) ---

/// <summary>
/// The static part of a Formula picture, sent once per response: the Grouped Graph's groups (clauses), nodes (literal
/// occurrences) and variables, without its edges. A frontend writes the clauses out as the formula reads, joined by ∧, with
/// the literals of each joined by ∨.
/// </summary>
sealed record FormulaPayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("layout")] string Layout,
    [property: JsonPropertyName("shape")] string Shape,
    [property: JsonPropertyName("rule")] string Rule,
    [property: JsonPropertyName("groups")] IReadOnlyList<GroupedGraphGroupDto> Groups,
    [property: JsonPropertyName("nodes")] IReadOnlyList<GroupedGraphNodeDto> Nodes,
    [property: JsonPropertyName("variables")] IReadOnlyList<GroupedGraphVariableDto> Variables);

/// <summary>
/// One picture of the formula: the Grouped Graph frame (<c>nodes</c> = literal states, <c>groups</c> = clause states,
/// <c>assignment</c>) plus <c>variables</c>, the state of each variable's chip in the assignment strip. Like the other maps it
/// lists only variables whose state is not <see cref="PictureState.Background"/>: an unassigned variable is absent.
/// </summary>
sealed record FormulaFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("nodes")] IReadOnlyDictionary<string, StateRef> Nodes,
    [property: JsonPropertyName("groups")] IReadOnlyDictionary<string, StateRef> Groups,
    [property: JsonPropertyName("assignment")] IReadOnlyDictionary<string, bool> Assignment,
    [property: JsonPropertyName("variables")] IReadOnlyDictionary<string, StateRef> Variables) {
    /// <summary>The solver step this frame was drawn from (see <see cref="FramesResponse"/>, #895); null for an added frame. Always written.</summary>
    [JsonPropertyName("step")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? Step { get; init; }

    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }
}

/// <summary>
/// Derives the Formula picture of a CNF problem (SAT, 3SAT; #925): the formula as it is written, one box per clause. It is a
/// second view of the Grouped Graph and shares everything with it: the spec (<see cref="GroupedGraphSpec"/>), the ids
/// (<c>c{i}</c>, <c>c{i}-{j}</c>, <c>v:{name}</c>; see <see cref="GroupedGraphFrames"/>), the <see cref="Assignment"/> steps and
/// every literal and clause state, which <see cref="GroupedGraphFrames.Frame"/> works out. So the clause graph, the factor
/// graph and the formula always color a literal alike, and the Reduction View matches gadgets on the same ids.
/// <para>
/// <b>Variables.</b> The one thing it adds is the assignment strip's states, <c>variables</c>: a true variable takes the state
/// of a picked literal in that step (<see cref="GraphSubsetFrames.Picked"/>: <c>ElementHighlight</c> while a whole candidate is
/// tried, <c>Committed</c> while it is kept, <c>Solution</c> in the confirmed answer, <c>Rejected</c> in a Done with ok =
/// false), a false variable is <c>False</c>, an unassigned one <c>Background</c>, and the variable in the focus is
/// <c>ElementHighlight</c>. The order is the shared one (<see cref="GraphSubsetFrames.Rank"/>).
/// </para>
/// <para>
/// A literal written twice is two pills, as in the Grouped Graph (ids are per occurrence). A run that finds nothing ends on the
/// Done its solver records; Redux's SAT solvers give it no assignment, so it is blank apart from its caption. A run that was
/// stopped ends on a blank Done.
/// </para>
/// </summary>
static class FormulaFrames {
    public const string KindName = "formula";
    public const string LayoutName = "cnf";

    // ---- The payload ----

    public static FormulaPayload Payload(GroupedGraphSpec spec) {
        var graph = GroupedGraphFrames.Payload(spec);
        return new FormulaPayload(KindName, LayoutName, GroupedGraphFrames.ShapeName, GroupedGraphFrames.RuleName, graph.Groups, graph.Nodes, graph.Variables);
    }

    // ---- Frames ----

    /// <summary>
    /// The frame for an assignment at one step: <see cref="GroupedGraphFrames.Frame"/>'s, plus the variables' states.
    /// <c>incremental</c> and <c>endsRun</c> are as there.
    /// </summary>
    public static FormulaFrame Frame(GroupedGraphSpec spec, Assignment partial, StepEvent ev, string[] focus, string caption, bool? ok = null, bool incremental = false, bool endsRun = false) {
        var graph = GroupedGraphFrames.Frame(spec, partial, ev, focus, caption, ok, incremental, endsRun);
        var picked = GraphSubsetFrames.Picked(ev, incremental, endsRun, ok == true);
        var variables = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        // The frame's assignment holds exactly the variables of this formula that the step assigns, keyed by variable id.
        foreach (var (id, value) in graph.Assignment)
            GraphSubsetFrames.Raise(variables, id, value ? picked : PictureState.False);
        var known = new HashSet<string>(spec.Variables.Select(GroupedGraphFrames.VariableId), StringComparer.Ordinal);
        foreach (var id in focus)
            if (known.Contains(id)) GraphSubsetFrames.Raise(variables, id, PictureState.ElementHighlight);
        return new FormulaFrame(graph.Event, graph.Caption, graph.Focus, graph.Nodes, graph.Groups, graph.Assignment, GraphSubsetFrames.ToRefs(variables)) { Ok = graph.Ok };
    }

    /// <summary>
    /// The frame that closes a stopped run: a Done frame with ok = false, nothing assigned and every element Background (see
    /// <see cref="GroupedGraphFrames.StoppedFrame"/>).
    /// </summary>
    public static FormulaFrame StoppedFrame(string caption) =>
        new(StepEvent.Done, caption, [], new SortedDictionary<string, StateRef>(StringComparer.Ordinal),
            new SortedDictionary<string, StateRef>(StringComparer.Ordinal), new SortedDictionary<string, bool>(StringComparer.Ordinal),
            new SortedDictionary<string, StateRef>(StringComparer.Ordinal)) { Ok = false };

    // ---- The whole response ----

    /// <summary>
    /// The whole response, built as <see cref="GroupedGraphFrames.Build"/> builds the Grouped Graph's: the payload, a frame per
    /// kept step, then a solved frame built from the answer unless the last step already is the Done step. A run stopped by the
    /// recording time limit or by the solver's own timer (the answer <see cref="GroupedGraphFrames.SolverTimeoutAnswer"/>) ends
    /// with <see cref="StoppedFrame"/> instead, and its answer string is never read.
    /// </summary>
    public static FramesResponse Build(GroupedGraphSpec spec, SolveRun run, Func<string, Assignment> parseAnswer, Func<string, bool> verifyAnswer) {
        bool stopped = run.TimedOut || string.Equals(run.Answer.Trim(), GroupedGraphFrames.SolverTimeoutAnswer, StringComparison.OrdinalIgnoreCase);
        var frames = new List<object>();
        SolverStep<Assignment>? last = null;
        foreach (var (step, endsRun, index) in GraphSubsetFrames.DrawnSteps<Assignment>(run)) {
            frames.Add((stopped && step.Event == StepEvent.Done
                ? StoppedFrame(step.Caption)
                : Frame(spec, step.Partial, step.Event, step.Focus, step.Caption, step.Ok, run.Incremental, endsRun)) with { Step = index });
            last = step;
        }
        if (stopped) {
            if (last is not { Event: StepEvent.Done })
                frames.Add(StoppedFrame(run.TimedOut ? GraphSubsetFrames.StoppedCaption : GroupedGraphFrames.SolverTimeoutCaption));
        } else if (last is not { Event: StepEvent.Done }) {
            Assignment answer = parseAnswer(run.Answer);
            frames.Add(Frame(spec, answer, StepEvent.Done, [], GroupedGraphFrames.AnswerCaption(run.Answer, answer), verifyAnswer(run.Answer)));
        }
        return new FramesResponse(nameof(VisualizationType.Formula), Payload(spec), frames);
    }
}

/// <summary>
/// A visualization that draws a CNF problem (SAT, 3SAT) as its formula (#925). Like <see cref="IGroupedGraphVisualization{TProblem}"/>
/// it declares only what the problem is (<see cref="Describe"/>: its clauses), and the frames hook comes for free. Implement
/// members explicitly so nothing new appears in /info, and declare <c>Type? IVisualization.StepShape =&gt; typeof(Assignment)</c>,
/// so every solver that draws on the Grouped Graph draws on the Formula too.
/// </summary>
interface IFormulaVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    GroupedGraphSpec Describe(TProblem problem);

    /// <summary>Reads a solver's answer string as an assignment. The default accepts <c>(x1:True,x2:False)</c>.</summary>
    Assignment ParseAnswer(TProblem problem, string answer) => GroupedGraphFrames.ParseAssignment(answer);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return FormulaFrames.Build(Describe(problem), run,
            answer => ParseAnswer(problem, answer),
            answer => {
                // The problem's own verifier decides; a certificate it cannot read (such as "No Solution") is not correct.
                try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
            });
    }
}
