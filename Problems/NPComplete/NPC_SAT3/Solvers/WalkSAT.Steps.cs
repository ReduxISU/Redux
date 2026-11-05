using API.Problems.NPComplete.NPC_SAT3.Steps;
using static API.Interfaces.Graphs.GroupedGraphFrames;

namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

// The steps WalkSAT records. Each try starts with a Try (its random start; "Restart." for every try after the first), then
// one step per flip: the clause it picked (no true literal) and the variable it flips are the focus, and the partial is the
// whole assignment after the flip, a candidate (WalkSAT is not incremental). A failed try ends with a Reject; the flip that
// makes every clause true is an Accept, followed by the Done.
//
// The search cannot know which try will succeed, so it reports its tries as it runs and RestartingSearchSteps records them
// once it is over: the first try and the successful one in full, the tries in between as one Reject ("Tries 2–37: ..."); a
// try too long for the recorder's cap keeps its first and last flips and says how many it skipped. Recording draws nothing
// from the generator, so the answer is the same with steps or without. When every try fails, the Done says it gave up:
// WalkSAT can miss an answer, so that is no proof that there is none.
partial class WalkSAT {
    // One flip of a try: the clause it repairs (0-based), the variable it flips (an index into the variables), how it chose
    // it, and how many clauses it turns false.
    private readonly record struct Flip(int Clause, int Variable, FlipKind Kind, int Breaks);

    // How WalkSAT's tries read: each move is a flip of a variable of a clause with no true literal.
    private sealed class Steps(SatFormula formula, bool recording, int maxFlips) : RestartingSearchSteps<Flip>(formula, recording) {
        public void Flipped(int clause, int variable, FlipKind kind, int breaks) => Add(new Flip(clause, variable, kind, breaks));

        protected override string SolverLabel => "WalkSAT";
        protected override string MoveNoun => "flip";
        protected override string EachTry => $"ends after {Count(maxFlips, "flip")}";

        protected override void Apply(Flip f, bool?[] values) => values[f.Variable] = !values[f.Variable];

        protected override string[] MoveFocus(Flip f) => [ClauseId(f.Clause), VariableId(Formula.Variables[f.Variable])];

        // "Flip 3: C2 (x1 | !x2 | x3) has no true literal. Flip x2 to False: a free flip, no clause turns false. Now ..."
        protected override string MoveCaption(SatTry<Flip> t, Flip f, bool?[] after, int number) {
            string flip = $"{Formula.Variables[f.Variable]} to {after[f.Variable]}";
            string turns = f.Breaks == 0 ? "no clause turns false" : $"{Count(f.Breaks, "clause")} {(f.Breaks == 1 ? "turns" : "turn")} false";
            string how = f.Kind switch {
                FlipKind.Free => $"Flip {flip}: a free flip, {turns}.",
                FlipKind.Random => $"No variable of C{f.Clause + 1} flips free, so flip one at random: {flip}, and {turns}.",
                _ => $"No variable of C{f.Clause + 1} flips free, so flip the one that turns the fewest clauses false: {flip}, and {turns}.",
            };
            var still = FalseClauses(after);
            string now = still.Count == 0 ? "Every clause now has a true literal." : $"Now {Falsified(still, lower: true)}";
            return $"Flip {number}: {ClauseLabel(f.Clause)} has no true literal. {how} {now}";
        }
    }
}
