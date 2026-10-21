using System.Text.Json.Serialization;
using API.Interfaces.Steps;

namespace API.Interfaces.Graphs;

// The payload-driven Formula (#929): layouts, expression trees, the operator table, typed variables, and the frames of a
// formula that is not a CNF list. The CNF Formula of SAT and 3SAT (#925, FormulaFrames.cs) is the same picture with
// layout "cnf" and literal lists; nothing here changes what it writes.

/// <summary>The Formula's <c>layout</c> values and <c>arrange</c> values.</summary>
static class FormulaLayouts {
    /// <summary>Groups as boxes joined by <c>and</c>, literals inside joined by <c>or</c> (a preset of <see cref="Rows"/>).</summary>
    public const string Cnf = "cnf";
    /// <summary>Groups as boxes joined by <c>or</c>, literals inside joined by <c>and</c> (a preset of <see cref="Rows"/>).</summary>
    public const string Dnf = "dnf";
    /// <summary>Groups as boxes joined by the payload's <c>outer</c> operator; a literal list joined by its <c>inner</c> one.</summary>
    public const string Rows = "rows";
    /// <summary>Each group one expression on its own line(s), unboxed until it has a state.</summary>
    public const string Equation = "equation";

    /// <summary><c>arrange</c>: boxes flow left to right and wrap at group boundaries (a frontend's default).</summary>
    public const string Flow = "flow";
    /// <summary><c>arrange</c>: a line per box, the outer operator leading every later one.</summary>
    public const string Stack = "stack";
}

/// <summary>
/// The operator names an expression may use (<see cref="FormulaExpr.Op"/>), from the one table every frontend keeps:
/// relations <c>eq le ge lt gt</c>, <c>or</c>, <c>and</c>, <c>plus minus</c>, <c>times</c>, <c>mul</c> (juxtaposition:
/// 2x), prefix <c>not neg</c>, and the call <c>sum</c> (Σ(a, b, …)). A frontend draws a name it does not know as
/// <c>name(a, b)</c>, so this list can grow without breaking one; Redux's own builders use only these.
/// </summary>
static class FormulaOps {
    public const string Eq = "eq", Le = "le", Ge = "ge", Lt = "lt", Gt = "gt";
    public const string Or = "or", And = "and";
    public const string Plus = "plus", Minus = "minus", Times = "times", Mul = "mul";
    public const string Not = "not", Neg = "neg", Sum = "sum";

    /// <summary>Every name in the table.</summary>
    public static readonly IReadOnlySet<string> Names = new HashSet<string>(StringComparer.Ordinal) {
        Eq, Le, Ge, Lt, Gt, Or, And, Plus, Minus, Times, Mul, Not, Neg, Sum,
    };
}

/// <summary>
/// One piece of a Formula expression tree (a group's <c>expr</c>, or a frame's <c>exprs</c> entry). Exactly one kind:
/// <list type="bullet">
/// <item>an operator node: <see cref="Op"/> (a <see cref="FormulaOps"/> name) over <see cref="Args"/>;</item>
/// <item>a variable occurrence: <see cref="Var"/>, a variable id (<c>v:x1</c>) of the payload's strip;</item>
/// <item>a number: <see cref="Num"/>, optionally tied to a strip variable by <see cref="Variable"/>;</item>
/// <item>a placeholder: <see cref="Label"/> (such as "?"), optionally tied to a variable.</item>
/// </list>
/// <see cref="Id"/> names the piece in frames (<c>nodes</c>, <c>focus</c>), gadget links and the verifier's "show where it
/// fails", and stays the same from frame to frame; an operator node's id owns its symbols. Unset fields are left out of
/// the JSON.
/// </summary>
sealed record FormulaExpr {
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }

    [JsonPropertyName("op")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Op { get; init; }

    [JsonPropertyName("args")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<FormulaExpr>? Args { get; init; }

    [JsonPropertyName("var")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Var { get; init; }

    [JsonPropertyName("num")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Num { get; init; }

    [JsonPropertyName("label")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; init; }

    [JsonPropertyName("variable")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Variable { get; init; }

    /// <summary>An operator node.</summary>
    public static FormulaExpr Operator(string id, string op, params FormulaExpr[] args) => new() { Id = id, Op = op, Args = args };

    /// <summary>An occurrence of the strip variable <paramref name="variableId"/>.</summary>
    public static FormulaExpr VarRef(string id, string variableId) => new() { Id = id, Var = variableId };

    /// <summary>A number, tied to the strip variable <paramref name="variable"/> when given.</summary>
    public static FormulaExpr Number(string id, long n, string? variable = null) => new() { Id = id, Num = n, Variable = variable };

    /// <summary>A placeholder drawn like a number, such as "?" or a number too large for <see cref="Num"/>.</summary>
    public static FormulaExpr Placeholder(string id, string label, string? variable = null) => new() { Id = id, Label = label, Variable = variable };

    /// <summary>This piece and every piece under it, depth first.</summary>
    public IEnumerable<FormulaExpr> Pieces() {
        yield return this;
        foreach (var arg in Args ?? [])
            foreach (var piece in arg.Pieces()) yield return piece;
    }
}

/// <summary>
/// A group's state in a frame: <c>{ "state": … }</c> as every other map writes it, plus an optional <see cref="Note"/>
/// ("total 6 &gt; 5") drawn after the group's label, used only by stacked rows and equations.
/// </summary>
sealed record FormulaGroupState([property: JsonPropertyName("state")] PictureState State) {
    [JsonPropertyName("note")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Note { get; init; }
}

/// <summary>
/// One picture of a payload-driven Formula (#929). The same fields as the CNF Formula's frame (<see cref="FormulaFrame"/>):
/// <c>nodes</c> keys any piece id, <c>groups</c> may carry a note, <c>assignment</c> holds a boolean or a number per
/// variable, <c>variables</c> the strip's chip states. <see cref="Exprs"/> replaces a group's expression for this frame,
/// for a solver that builds the formula as it goes. Like the other maps, the state maps list only what is not
/// <see cref="PictureState.Background"/>.
/// </summary>
sealed record FormulaExprFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("nodes")] IReadOnlyDictionary<string, StateRef> Nodes,
    [property: JsonPropertyName("groups")] IReadOnlyDictionary<string, FormulaGroupState> Groups,
    [property: JsonPropertyName("assignment")] IReadOnlyDictionary<string, object> Assignment,
    [property: JsonPropertyName("variables")] IReadOnlyDictionary<string, StateRef> Variables) {
    /// <summary>The solver step this frame was drawn from; null for an added frame. Always written.</summary>
    [JsonPropertyName("step")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? Step { get; init; }

    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }

    /// <summary>Group id to the expression that replaces the group's own for this frame; left out when nothing is replaced.</summary>
    [JsonPropertyName("exprs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, FormulaExpr>? Exprs { get; init; }
}

/// <summary>
/// A frame of a payload-driven Formula while it is being worked out: set states (the higher-ranked one wins, as in every
/// picture), notes, values and replaced expressions, then <see cref="ToFrame"/>.
/// </summary>
sealed class FormulaFrameDraft {
    private readonly Dictionary<string, PictureState> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PictureState> _variables = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, FormulaGroupState> _groups = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, object> _assignment = new(StringComparer.Ordinal);
    private SortedDictionary<string, FormulaExpr>? _exprs;

    /// <summary>Gives piece <paramref name="id"/> <paramref name="state"/> unless it already has a higher-ranked one.</summary>
    public FormulaFrameDraft Node(string id, PictureState state) { GraphSubsetFrames.Raise(_nodes, id, state); return this; }

    /// <summary>Gives strip variable <paramref name="id"/> <paramref name="state"/> unless it already has a higher-ranked one.</summary>
    public FormulaFrameDraft Var(string id, PictureState state) { GraphSubsetFrames.Raise(_variables, id, state); return this; }

    /// <summary>Sets group <paramref name="id"/>'s state and note (Background with no note clears it).</summary>
    public FormulaFrameDraft Group(string id, PictureState state, string? note = null) {
        if (state == PictureState.Background && note == null) _groups.Remove(id);
        else _groups[id] = new FormulaGroupState(state) { Note = note };
        return this;
    }

    /// <summary>Sets a boolean variable's value.</summary>
    public FormulaFrameDraft Value(string id, bool value) { _assignment[id] = value; return this; }

    /// <summary>Sets an integer variable's value.</summary>
    public FormulaFrameDraft Value(string id, long value) { _assignment[id] = value; return this; }

    /// <summary>Replaces group <paramref name="groupId"/>'s expression for this frame.</summary>
    public FormulaFrameDraft Expr(string groupId, FormulaExpr expr) {
        (_exprs ??= new(StringComparer.Ordinal))[groupId] = expr;
        return this;
    }

    public FormulaExprFrame ToFrame(StepEvent ev, string caption, string[] focus, bool? ok = null) =>
        new(ev, caption, focus, GraphSubsetFrames.ToRefs(_nodes), _groups, _assignment, GraphSubsetFrames.ToRefs(_variables)) {
            Ok = ev == StepEvent.Done ? ok : null,
            Exprs = _exprs,
        };
}

/// <summary>The whole response of a payload-driven Formula, built as the other pictures build theirs.</summary>
static class FormulaExprFrames {
    /// <summary>
    /// The frame that closes a stopped run: a Done with ok = false, nothing assigned and every piece Background, as the CNF
    /// Formula's (<see cref="FormulaFrames.StoppedFrame"/>).
    /// </summary>
    public static FormulaExprFrame StoppedFrame(string caption) => new FormulaFrameDraft().ToFrame(StepEvent.Done, caption, [], ok: false);

    /// <summary>
    /// The payload, a frame per kept step (<paramref name="draw"/>, given whether the step is an Accept that ends the run),
    /// then a solved frame (<paramref name="solved"/>, from the answer string) unless the last step already is the Done step.
    /// A run stopped by the recording time limit or by the solver's own timer (the answer
    /// <see cref="GroupedGraphFrames.SolverTimeoutAnswer"/>, in any case) ends with <see cref="StoppedFrame"/> instead, and
    /// its answer string is never read.
    /// </summary>
    public static FramesResponse Build<TStep>(FormulaPayload payload, SolveRun run, Func<SolverStep<TStep>, bool, FormulaExprFrame> draw,
        Func<string, FormulaExprFrame> solved) {
        bool stopped = run.TimedOut || string.Equals(run.Answer.Trim(), GroupedGraphFrames.SolverTimeoutAnswer, StringComparison.OrdinalIgnoreCase);
        var frames = new List<object>();
        SolverStep<TStep>? last = null;
        foreach (var (step, endsRun, index) in GraphSubsetFrames.DrawnSteps<TStep>(run)) {
            frames.Add((stopped && step.Event == StepEvent.Done ? StoppedFrame(step.Caption) : draw(step, endsRun)) with { Step = index });
            last = step;
        }
        if (stopped) {
            if (last is not { Event: StepEvent.Done })
                frames.Add(StoppedFrame(run.TimedOut ? GraphSubsetFrames.StoppedCaption : GroupedGraphFrames.SolverTimeoutCaption));
        } else if (last is not { Event: StepEvent.Done }) {
            frames.Add(solved(run.Answer) with { Step = null });
        }
        return new FramesResponse(nameof(VisualizationType.Formula), payload, frames);
    }
}

/// <summary>
/// A visualization that draws a problem as a payload-driven Formula (#929): an equation, stacked constraint rows, or any
/// layout of <see cref="FormulaLayouts"/>. It declares what the problem's formula is (<see cref="Describe"/>), how one solver
/// step colors it (<see cref="Draw"/>) and the Done frame of an answer when the solver recorded none (<see cref="Solved"/>);
/// the frames hook comes for free. Implement members explicitly so nothing new appears in /info, and declare
/// <c>Type? IVisualization.StepShape =&gt; typeof(TStep)</c>.
/// <para>
/// It is an <see cref="IOptInVisualization"/>: listed only for clients that ask with <c>includePictures=true</c>, since
/// Redux_GUI cannot draw it yet. Such views are extra visualizations; a problem's default stays what it was.
/// </para>
/// </summary>
interface IFormulaExprVisualization<TProblem, TStep> : IFramesVisualization, IOptInVisualization where TProblem : IProblem {
    FormulaPayload Describe(TProblem problem);

    /// <summary>The frame of one recorded step; <paramref name="endsRun"/>: it is an Accept the run ends on.</summary>
    FormulaExprFrame Draw(TProblem problem, SolverStep<TStep> step, bool incremental, bool endsRun);

    /// <summary>The Done frame for an answer string, with ok from the problem's verifier, for a solver that records no steps.</summary>
    FormulaExprFrame Solved(TProblem problem, string answer);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return FormulaExprFrames.Build<TStep>(Describe(problem), run, (step, endsRun) => Draw(problem, step, run.Incremental, endsRun),
            answer => Solved(problem, answer));
    }
}
