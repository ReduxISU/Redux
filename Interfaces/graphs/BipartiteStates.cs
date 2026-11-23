using API.Interfaces.Steps;

namespace API.Interfaces.Graphs;

/// <summary>Which kind of piece of the Bipartite picture a state is worked out for.</summary>
enum BipartitePieceKind {
    /// <summary>A node of the column the answer names (a set for Set Cover, an element for Hitting Set).</summary>
    Pick,
    /// <summary>A node of the other column, which the picks must hit (at least once, or exactly once).</summary>
    Constraint,
    /// <summary>A membership edge; it belongs to the pick at one of its ends.</summary>
    Edge,
}

/// <summary>
/// What one step says about the whole picture, the same for every piece: the rule, the event, whether the run is incremental,
/// whether the step is an Accept that ends the run, the Done's ok, and how many picks are chosen.
/// </summary>
readonly record struct BipartiteStepFacts(BipartiteRule Rule, StepEvent Event, bool Incremental, bool EndsRun, bool Ok, int ChosenCount) {
    /// <summary>The state of what is picked at this step (<see cref="GraphSubsetFrames.Picked"/>).</summary>
    public PictureState PickedState => GraphSubsetFrames.Picked(Event, Incremental, EndsRun, Ok);

    /// <summary>A complete candidate is judged (a Done, or a Try or Reject of a whole candidate) and something is picked.</summary>
    public bool Judged => ChosenCount > 0 && (Event == StepEvent.Done || (!Incremental && Event is StepEvent.Try or StepEvent.Reject));

    /// <summary>A failed focus: the piece under test in a Reject step of an incremental run.</summary>
    public bool FocusFailed => Incremental && Event == StepEvent.Reject;
}

/// <summary>
/// What one piece is at one step: its kind; whether it is chosen (a pick in the step's picks, or an edge whose pick is); how
/// many chosen picks hit it (a constraint); whether it hits a constraint that some chosen pick already hits (a pick); and
/// whether it is in the step's focus (for an edge: it, or its pick, is named in the focus).
/// </summary>
readonly record struct BipartitePieceFacts(BipartitePieceKind Kind, bool Chosen = false, int Hits = 0, bool TouchesCovered = false, bool InFocus = false);

/// <summary>
/// The state rules of the Bipartite picture as one table: each rule names when it applies and the state it gives. A piece
/// gets the state of the strongest rule that applies (<see cref="Precedence"/>), and <see cref="Of"/> also says which rule that
/// was, so a wrong colour can be traced to its rule without reading the builder (<c>BipartiteTrace</c> in the tests). A pure
/// function of the facts: no spec, no ids.
/// </summary>
static class BipartiteStates {
    /// <summary>One rule: its name (what a trace shows), the state it gives, and when it applies.</summary>
    internal sealed record Rule(string Name, PictureState State, Func<BipartitePieceFacts, BipartiteStepFacts, bool> Applies);

    /// <summary>The order states win in when several rules apply to one piece, strongest first; Background when none applies.</summary>
    public static readonly IReadOnlyList<PictureState> Precedence = [
        PictureState.Rejected, PictureState.ElementHighlight, PictureState.Solution, PictureState.Committed,
        PictureState.Covered, PictureState.Blocked, PictureState.Background,
    ];

    /// <summary>The name <see cref="Of"/> gives when no rule applies.</summary>
    public const string NoRule = "nothing applies";

    private static bool Picked(BipartitePieceFacts p) => p.Kind is BipartitePieceKind.Pick or BipartitePieceKind.Edge && p.Chosen;
    private static bool Exact(BipartiteStepFacts s) => s.Rule == BipartiteRule.ExactlyOnce;

    /// <summary>Every rule, in the order a tie between two rules of the same state is named.</summary>
    internal static readonly IReadOnlyList<Rule> Rules = [
        // What is picked (a pick node and its edges), by what the step does with it (GraphSubsetFrames.Picked).
        new("picked, in the confirmed answer", PictureState.Solution, (p, s) => Picked(p) && s.PickedState == PictureState.Solution),
        new("picked, in a run that found nothing", PictureState.Rejected, (p, s) => Picked(p) && s.PickedState == PictureState.Rejected),
        new("picked, in a whole candidate being tried", PictureState.ElementHighlight, (p, s) => Picked(p) && s.PickedState == PictureState.ElementHighlight),
        new("picked, kept so far", PictureState.Committed, (p, s) => Picked(p) && s.PickedState == PictureState.Committed),
        // The constraints, by how often the picks hit them.
        new("hit more than once under exactly-once", PictureState.Rejected, (p, s) => p.Kind == BipartitePieceKind.Constraint && Exact(s) && p.Hits > 1),
        new("hit", PictureState.Covered, (p, s) => p.Kind == BipartitePieceKind.Constraint && p.Hits > 0 && !(Exact(s) && p.Hits > 1)),
        new("not hit, in a judged candidate", PictureState.Rejected, (p, s) => p.Kind == BipartitePieceKind.Constraint && p.Hits == 0 && s.Judged),
        // Under exactly-once, a pick that would double up on a hit constraint, unless a whole candidate is only being tried.
        new("would hit a constraint twice", PictureState.Blocked, (p, s) =>
            p.Kind == BipartitePieceKind.Pick && !p.Chosen && Exact(s) && s.PickedState != PictureState.ElementHighlight && p.TouchesCovered),
        // The focus: what the solver is looking at, or in an incremental Reject what failed.
        new("the focus", PictureState.ElementHighlight, (p, s) => p.InFocus && !s.FocusFailed),
        new("the focus, which failed", PictureState.Rejected, (p, s) => p.InFocus && s.FocusFailed),
    ];

    /// <summary>The state of one piece at one step, and the name of the rule that gave it (<see cref="NoRule"/> for Background).</summary>
    public static (PictureState State, string Rule) Of(BipartitePieceFacts piece, BipartiteStepFacts step) {
        Rule? best = null;
        foreach (var rule in Rules)
            if (rule.Applies(piece, step) && (best == null || Strength(rule.State) < Strength(best.State))) best = rule;
        return best == null ? (PictureState.Background, NoRule) : (best.State, best.Name);
    }

    private static readonly Dictionary<PictureState, int> StrengthOf = Precedence.Select((state, i) => (state, i)).ToDictionary(p => p.state, p => p.i);

    private static int Strength(PictureState state) => StrengthOf.GetValueOrDefault(state, Precedence.Count);
}
