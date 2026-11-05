using System.Text.Json.Serialization;
using API.Interfaces.Steps;

namespace API.Interfaces.Graphs;

// --- JSON shapes (explicit names so the contract does not depend on serializer naming options) ---

/// <summary>
/// One occurrence of a variable in a clause: an edge from the variable (<c>a</c>) to the clause (<c>b</c>). <c>positive</c> is
/// false for a negated literal, which the frontend draws dashed.
/// </summary>
sealed record FactorGraphEdgeDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("a")] string A,
    [property: JsonPropertyName("b")] string B,
    [property: JsonPropertyName("positive")] bool Positive);

/// <summary>
/// The static part of the SAT factor graph, a Bipartite Graph whose left column holds the variables (<c>elements</c>) and
/// whose right column holds the clauses (<c>sets</c>). Sent once per response.
/// </summary>
sealed record FactorGraphPayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("layout")] string Layout,
    [property: JsonPropertyName("pickSide")] string PickSide,
    [property: JsonPropertyName("rule")] string Rule,
    [property: JsonPropertyName("elements")] IReadOnlyList<GraphNodeDto> Elements,
    [property: JsonPropertyName("sets")] IReadOnlyList<BipartiteSetDto> Sets,
    [property: JsonPropertyName("edges")] IReadOnlyList<FactorGraphEdgeDto> Edges);

/// <summary>
/// One picture of the factor graph: the Bipartite frame (<c>nodes</c> and <c>edges</c> list only ids whose state is not
/// Background; <c>hits</c> is how many true literals each clause has, for every clause with at least one) plus the
/// <c>assignment</c> so far, keyed by variable id (an unassigned variable is absent), as in the clause graph.
/// </summary>
sealed record FactorGraphFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("nodes")] IReadOnlyDictionary<string, StateRef> Nodes,
    [property: JsonPropertyName("edges")] IReadOnlyDictionary<string, StateRef> Edges,
    [property: JsonPropertyName("hits")] IReadOnlyDictionary<string, int> Hits,
    [property: JsonPropertyName("assignment")] IReadOnlyDictionary<string, bool> Assignment) {
    /// <summary>The solver step this frame was drawn from (see <see cref="FramesResponse"/>, #895); null for an added frame. Always written.</summary>
    [JsonPropertyName("step")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? Step { get; init; }

    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }
}

/// <summary>
/// The second picture of a CNF problem (SAT, 3SAT): its factor graph, drawn as a Bipartite Graph with the variables on the
/// left, the clauses on the right and one edge per literal occurrence (dashed for a negated literal). It reads the same
/// <see cref="GroupedGraphSpec"/> and the same <see cref="Assignment"/> steps as the clause graph
/// (<see cref="GroupedGraphFrames"/>), so every SAT solver animates on both pictures with no extra code.
/// <para>
/// <b>Ids</b> are the clause graph's, so the two pictures (and the Reduction View) can be linked: variable <c>v:{name}</c>
/// (<see cref="GroupedGraphFrames.VariableId"/>, the name escaped), clause <c>c{i}</c>, and the edge of the <c>j</c>-th literal
/// of clause <c>i</c> is that literal's id, <c>c{i}-{j}</c>, so a variable written twice in one clause has two edges. Every
/// edge gives its variable as <c>a</c> and its clause as <c>b</c>.
/// </para>
/// <para>
/// <b>States.</b> An edge whose literal is true is <c>Solution</c> in the confirmed answer (the Done, or an Accept that ends
/// the run), <c>Rejected</c> in a Done with ok = false (the run found nothing), <c>Committed</c> in another step that keeps
/// the assignment (#886) and
/// <c>ElementHighlight</c> in a step that only tries a whole assignment (<see cref="GraphSubsetFrames.Picked"/>); a variable
/// assigned true is the same. An edge whose literal is false, and a variable assigned false, are <c>False</c>; anything
/// unassigned is <c>Background</c>. A clause with a true literal is <c>Covered</c>; one whose literals are all assigned and
/// all false is <c>Rejected</c>, and so are its edges, as the clause graph marks its literals (judged from the assignment
/// alone, whatever the event; a clause with an unassigned literal is not Rejected). The variable being tried (a <c>v:</c> id in the focus) is
/// <c>ElementHighlight</c>, and so are its edges except false ones, which stay <c>False</c> as in the clause graph. A clause in
/// the focus is <c>ElementHighlight</c>, or <c>Rejected</c> in a Reject step. Precedence:
/// <c>Rejected &gt; ElementHighlight &gt; Solution &gt; Committed &gt; Covered &gt; Blocked &gt; False &gt; Background</c>.
/// </para>
/// </summary>
static class FactorGraphFrames {
    public const string KindName = BipartiteFrames.KindName;
    public const string LayoutName = BipartiteFrames.LayoutName;
    /// <summary>The factor graph's answer is an assignment of the left column, not a pick of either column.</summary>
    public const string PickSideName = "assignment";
    public const string RuleName = GroupedGraphFrames.RuleName;

    public static FactorGraphPayload Payload(GroupedGraphSpec spec) {
        var variables = spec.Variables.Select(n => new GraphNodeDto(GroupedGraphFrames.VariableId(n), n)).ToList();
        var clauses = new List<BipartiteSetDto>();
        var edges = new List<FactorGraphEdgeDto>();
        for (int i = 0; i < spec.Clauses.Count; i++) {
            var members = new List<string>();
            for (int j = 0; j < spec.Clauses[i].Length; j++) {
                int literal = spec.Clauses[i][j];
                string variable = GroupedGraphFrames.VariableId(spec.Variables[Math.Abs(literal) - 1]);
                if (!members.Contains(variable)) members.Add(variable);
                edges.Add(new FactorGraphEdgeDto(GroupedGraphFrames.LiteralId(i, j), variable, GroupedGraphFrames.ClauseId(i), literal > 0));
            }
            clauses.Add(new BipartiteSetDto(GroupedGraphFrames.ClauseId(i), $"C{i + 1}", members));
        }
        return new FactorGraphPayload(KindName, LayoutName, PickSideName, RuleName, variables, clauses, edges);
    }

    /// <summary>
    /// The frame for an assignment at one step. <c>incremental</c> says whether the step comes from an incremental run
    /// (<see cref="SolveRun.Incremental"/>); <c>endsRun</c> whether it is an Accept that ends the run
    /// (<see cref="GraphSubsetFrames.EndsRun"/>).
    /// </summary>
    public static FactorGraphFrame Frame(GroupedGraphSpec spec, Assignment partial, StepEvent ev, string[] focus, string caption, bool? ok = null, bool incremental = false, bool endsRun = false) {
        var values = new Dictionary<int, bool>();
        var assignment = new SortedDictionary<string, bool>(StringComparer.Ordinal);
        for (int v = 1; v <= spec.Variables.Count; v++)
            if (partial.Values.TryGetValue(spec.Variables[v - 1], out var text) && ParseValue(text) is bool value) {
                values[v] = value;
                assignment[GroupedGraphFrames.VariableId(spec.Variables[v - 1])] = value;
            }

        var picked = GraphSubsetFrames.Picked(ev, incremental, endsRun, ok == true);
        var nodes = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var edges = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var hits = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var edgesOfVariable = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var clauseIds = new HashSet<string>(StringComparer.Ordinal);
        var edgeIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (v, value) in values)
            GraphSubsetFrames.Raise(nodes, GroupedGraphFrames.VariableId(spec.Variables[v - 1]), value ? picked : PictureState.False);

        for (int i = 0; i < spec.Clauses.Count; i++) {
            int[] clause = spec.Clauses[i];
            string clauseId = GroupedGraphFrames.ClauseId(i);
            clauseIds.Add(clauseId);
            int trues = 0, falses = 0;
            for (int j = 0; j < clause.Length; j++) {
                string edge = GroupedGraphFrames.LiteralId(i, j);
                edgeIds.Add(edge);
                int variable = Math.Abs(clause[j]);
                string variableId = GroupedGraphFrames.VariableId(spec.Variables[variable - 1]);
                if (!edgesOfVariable.TryGetValue(variableId, out var list)) edgesOfVariable[variableId] = list = [];
                list.Add(edge);
                if (!values.TryGetValue(variable, out bool assigned)) continue;   // unassigned: Background
                if (assigned == clause[j] > 0) {
                    trues++;
                    GraphSubsetFrames.Raise(edges, edge, picked);
                } else {
                    falses++;
                    GraphSubsetFrames.Raise(edges, edge, PictureState.False);
                }
            }
            if (trues > 0) {
                GraphSubsetFrames.Raise(nodes, clauseId, PictureState.Covered);
                hits[clauseId] = trues;
            } else if (clause.Length > 0 && falses == clause.Length) {
                // Every literal assigned and false: the clause and its edges are Rejected, as the clause graph marks the
                // clause and its literals. Judged from the assignment alone, whatever the event.
                GraphSubsetFrames.Raise(nodes, clauseId, PictureState.Rejected);
                for (int j = 0; j < clause.Length; j++) GraphSubsetFrames.Raise(edges, GroupedGraphFrames.LiteralId(i, j), PictureState.Rejected);
            }
        }

        // A false literal's edge stays False when its variable is the one being looked at, as in the clause graph.
        void HighlightEdge(string edge) {
            if (!edges.TryGetValue(edge, out var state) || state != PictureState.False)
                GraphSubsetFrames.Raise(edges, edge, PictureState.ElementHighlight);
        }
        var clauseMark = ev == StepEvent.Reject ? PictureState.Rejected : PictureState.ElementHighlight;
        foreach (var id in focus) {
            if (edgesOfVariable.TryGetValue(id, out var occurrences)) {
                GraphSubsetFrames.Raise(nodes, id, PictureState.ElementHighlight);
                foreach (var edge in occurrences) HighlightEdge(edge);
            } else if (clauseIds.Contains(id)) {
                GraphSubsetFrames.Raise(nodes, id, clauseMark);
            } else if (edgeIds.Contains(id)) {
                HighlightEdge(id);
            }
        }

        return new FactorGraphFrame(ev, caption, focus, GraphSubsetFrames.ToRefs(nodes), GraphSubsetFrames.ToRefs(edges), hits, assignment) { Ok = ok };
    }

    private static bool? ParseValue(string value) => value.Trim().ToLowerInvariant() switch {
        "true" or "t" => true,
        "false" or "f" => false,
        _ => null,
    };

    /// <summary>
    /// The frame that closes a run that did not finish: a Done frame with ok = false, nothing assigned, nothing hit and every
    /// element Background. See <see cref="GroupedGraphFrames.StoppedFrame"/>.
    /// </summary>
    public static FactorGraphFrame StoppedFrame(string caption) =>
        new(StepEvent.Done, caption, [], new SortedDictionary<string, StateRef>(StringComparer.Ordinal),
            new SortedDictionary<string, StateRef>(StringComparer.Ordinal), new SortedDictionary<string, int>(StringComparer.Ordinal),
            new SortedDictionary<string, bool>(StringComparer.Ordinal)) { Ok = false };

    /// <summary>
    /// The whole response, built exactly as the clause graph's (<see cref="GroupedGraphFrames.Build"/>): the payload, a frame
    /// per kept step, then a solved frame from the answer unless the last step already is the Done step. A run stopped by the
    /// recording time limit or by the solver's own timer ends with <see cref="StoppedFrame"/>, and its answer is never read.
    /// </summary>
    public static FramesResponse Build(GroupedGraphSpec spec, SolveRun run, Func<string, Assignment> parseAnswer, Func<string, bool> verifyAnswer) {
        bool stopped = run.TimedOut || GroupedGraphFrames.IsSolverTimeout(run.Answer);
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
        return new FramesResponse(nameof(VisualizationType.BipartiteGraph), Payload(spec), frames);
    }
}

/// <summary>
/// A visualization that draws a CNF problem (SAT, 3SAT) as its factor graph (<see cref="FactorGraphFrames"/>). It declares
/// only the clauses (<see cref="Describe"/>, as for the clause graph); the frames hook then comes for free. Implement members
/// explicitly so nothing new appears in /info, and declare <c>Type? IVisualization.StepShape =&gt; typeof(Assignment)</c>.
/// </summary>
interface IFactorGraphVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    GroupedGraphSpec Describe(TProblem problem);

    /// <summary>Reads a solver's answer string as an assignment. The default accepts <c>(x1:True,x2:False)</c>.</summary>
    Assignment ParseAnswer(TProblem problem, string answer) => GroupedGraphFrames.ParseAssignment(answer);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return FactorGraphFrames.Build(Describe(problem), run,
            answer => ParseAnswer(problem, answer),
            answer => {
                // The problem's own verifier decides; a certificate it cannot read (such as "No Solution") is not correct.
                try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
            });
    }
}
