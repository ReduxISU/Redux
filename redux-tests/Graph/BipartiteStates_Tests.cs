using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Bipartite picture's state rules (BipartiteStates), one test per rule, then the precedence between them. Pure: piece
// facts and step facts in, a state and the rule's name out. BipartiteFrames_Tests covers the same rules through whole frames.
public class BipartiteStates_Tests {
    private static BipartiteStepFacts Step(StepEvent ev, bool incremental = false, bool endsRun = false, bool ok = false,
                                           int chosen = 1, BipartiteRule rule = BipartiteRule.AtLeastOnce) =>
        new(rule, ev, incremental, endsRun, ok, chosen);

    private static readonly BipartitePieceFacts ChosenPick = new(BipartitePieceKind.Pick, Chosen: true);
    private static readonly BipartitePieceFacts ChosenEdge = new(BipartitePieceKind.Edge, Chosen: true);

    private static void Is(PictureState state, string rule, BipartitePieceFacts piece, BipartiteStepFacts step) =>
        Assert.Equal((state, rule), BipartiteStates.Of(piece, step));

    [Fact]
    public void Picked_In_The_Confirmed_Answer_Is_Solution() {
        Is(PictureState.Solution, "picked, in the confirmed answer", ChosenPick, Step(StepEvent.Done, ok: true));
        Is(PictureState.Solution, "picked, in the confirmed answer", ChosenEdge, Step(StepEvent.Accept, endsRun: true));
    }

    [Fact]
    public void Picked_In_A_Run_That_Found_Nothing_Is_Rejected() =>
        Is(PictureState.Rejected, "picked, in a run that found nothing", ChosenPick, Step(StepEvent.Done, ok: false));

    [Fact]
    public void Picked_In_A_Whole_Candidate_Being_Tried_Is_ElementHighlight() {
        Is(PictureState.ElementHighlight, "picked, in a whole candidate being tried", ChosenPick, Step(StepEvent.Try));
        Is(PictureState.ElementHighlight, "picked, in a whole candidate being tried", ChosenEdge, Step(StepEvent.Reject));
    }

    [Fact]
    public void Picked_And_Kept_So_Far_Is_Committed() {
        Is(PictureState.Committed, "picked, kept so far", ChosenPick, Step(StepEvent.Accept));
        Is(PictureState.Committed, "picked, kept so far", ChosenPick, Step(StepEvent.Try, incremental: true));
        Is(PictureState.Committed, "picked, kept so far", ChosenEdge, Step(StepEvent.Backtrack));
    }

    [Fact]
    public void A_Constraint_Hit_Twice_Under_Exactly_Once_Is_Rejected() =>
        Is(PictureState.Rejected, "hit more than once under exactly-once", new(BipartitePieceKind.Constraint, Hits: 2),
            Step(StepEvent.Accept, rule: BipartiteRule.ExactlyOnce));

    [Fact]
    public void A_Hit_Constraint_Is_Covered() {
        Is(PictureState.Covered, "hit", new(BipartitePieceKind.Constraint, Hits: 1), Step(StepEvent.Try, rule: BipartiteRule.ExactlyOnce));
        // At least once, more hits are still just covered.
        Is(PictureState.Covered, "hit", new(BipartitePieceKind.Constraint, Hits: 3), Step(StepEvent.Done, ok: true));
    }

    [Fact]
    public void A_Constraint_Not_Hit_In_A_Judged_Candidate_Is_Rejected() {
        var missed = new BipartitePieceFacts(BipartitePieceKind.Constraint, Hits: 0);
        Is(PictureState.Rejected, "not hit, in a judged candidate", missed, Step(StepEvent.Done, ok: false));
        Is(PictureState.Rejected, "not hit, in a judged candidate", missed, Step(StepEvent.Reject));
        // Not judged: an answer still being built, or nothing picked at all.
        Is(PictureState.Background, BipartiteStates.NoRule, missed, Step(StepEvent.Try, incremental: true));
        Is(PictureState.Background, BipartiteStates.NoRule, missed, Step(StepEvent.Done, chosen: 0));
    }

    [Fact]
    public void A_Pick_That_Would_Double_Up_Is_Blocked_Unless_A_Whole_Candidate_Is_Only_Tried() {
        var touching = new BipartitePieceFacts(BipartitePieceKind.Pick, TouchesCovered: true);
        Is(PictureState.Blocked, "would hit a constraint twice", touching, Step(StepEvent.Accept, incremental: true, rule: BipartiteRule.ExactlyOnce));
        Is(PictureState.Background, BipartiteStates.NoRule, touching, Step(StepEvent.Try, rule: BipartiteRule.ExactlyOnce));
        Is(PictureState.Background, BipartiteStates.NoRule, touching, Step(StepEvent.Accept, incremental: true));
    }

    [Fact]
    public void The_Focus_Is_ElementHighlight() =>
        Is(PictureState.ElementHighlight, "the focus", new(BipartitePieceKind.Constraint, InFocus: true), Step(StepEvent.Try, incremental: true));

    [Fact]
    public void The_Focus_Of_An_Incremental_Reject_Failed() =>
        Is(PictureState.Rejected, "the focus, which failed", new(BipartitePieceKind.Edge, InFocus: true), Step(StepEvent.Reject, incremental: true));

    [Fact]
    public void Nothing_Applies_To_A_Piece_Nothing_Touches() =>
        Is(PictureState.Background, BipartiteStates.NoRule, new(BipartitePieceKind.Pick), Step(StepEvent.Try));

    [Fact]
    public void Precedence_Is_Rejected_ElementHighlight_Solution_Committed_Covered_Blocked_Background() {
        Assert.Equal([PictureState.Rejected, PictureState.ElementHighlight, PictureState.Solution, PictureState.Committed,
            PictureState.Covered, PictureState.Blocked, PictureState.Background], BipartiteStates.Precedence);
        // The focus outranks what is picked: a kept pick under test is highlighted, a confirmed one too.
        Is(PictureState.ElementHighlight, "the focus", ChosenPick with { InFocus = true }, Step(StepEvent.Accept));
        Is(PictureState.ElementHighlight, "the focus", ChosenPick with { InFocus = true }, Step(StepEvent.Done, ok: true));
        // A failure outranks the focus: a doubled constraint in focus stays Rejected.
        Is(PictureState.Rejected, "hit more than once under exactly-once", new(BipartitePieceKind.Constraint, Hits: 2, InFocus: true),
            Step(StepEvent.Try, rule: BipartiteRule.ExactlyOnce));
    }

    [Fact]
    public void Every_Rule_Has_A_Unique_Name_And_A_State_In_The_Precedence() {
        Assert.Equal(BipartiteStates.Rules.Count, BipartiteStates.Rules.Select(r => r.Name).Distinct().Count());
        Assert.All(BipartiteStates.Rules, r => Assert.Contains(r.State, BipartiteStates.Precedence));
    }
}
