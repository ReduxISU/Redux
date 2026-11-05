using API.Problems.NPComplete.NPC_SAT3.Steps;
using static API.Interfaces.Graphs.GroupedGraphFrames;

namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

// The steps Schöning's algorithm records, like WalkSAT's. Each try starts with a Try from its random assignment ("Restart."
// for every try after the first; an Accept when it already makes every clause true), then one step per walk step: the
// clause it picked and the variable it flips are the focus, and the partial is the whole assignment after the flip, a
// candidate (not incremental). A failed try ends with a Reject after 3n flips; the flip that makes every clause true is the
// Accept before the Done. RestartingSearchSteps records the first try and the successful one in full and the tries in
// between as one step, within the recorder's cap. A run that gives up never says the formula has no answer.
partial class Schoning {
    // One step of a walk: the clause it picked (0-based, no true literal) and the variable of the literal it flips.
    private readonly record struct Flip(int Clause, int Variable);

    // How Schöning's tries read: each move flips the variable of a random literal of a clause with no true literal.
    private sealed class Steps(SatFormula formula, bool recording, int maxFlips) : RestartingSearchSteps<Flip>(formula, recording) {
        public void Flipped(int clause, int variable) => Add(new Flip(clause, variable));

        protected override string SolverLabel => "Schöning's algorithm";
        protected override string MoveNoun => "flip";
        protected override string EachTry => $"ends after {Count(maxFlips, "flip")}";

        protected override void Apply(Flip f, bool?[] values) => values[f.Variable] = !values[f.Variable];

        protected override string[] MoveFocus(Flip f) => [ClauseId(f.Clause), VariableId(Formula.Variables[f.Variable])];

        // "Flip 3: C2 (x1 | !x2 | x3) has no true literal. Pick one of its literals at random and flip its variable: x2 to
        // False. Now C1 has no true literal."
        protected override string MoveCaption(SatTry<Flip> t, Flip f, bool?[] after, int number) {
            var still = FalseClauses(after);
            string now = still.Count == 0 ? "Every clause now has a true literal." : $"Now {Falsified(still, lower: true)}";
            return $"Flip {number}: {ClauseLabel(f.Clause)} has no true literal. Pick one of its literals at random and flip its variable: "
                + $"{Formula.Variables[f.Variable]} to {after[f.Variable]}. {now}";
        }
    }
}
