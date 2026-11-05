using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SAT3.Steps;
using static API.Interfaces.Graphs.GroupedGraphFrames;

namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

// The steps PPZ records. Each try starts with a Try that names the random order of the variables (nothing set yet;
// "Restart." for every try after the first). Then one Try per variable, in that order: the partial holds the variables set
// so far (the rest are left out, so a picture draws them as Background), the focus is the variable, and the clause that
// forces its value when one does. PPZ builds its assignment variable by variable, so it is incremental. Once every variable
// is set the try is checked: an Accept when every clause has a true literal (then the Done), else a Reject whose focus is
// the clauses with no true literal. RestartingSearchSteps records the first try and the successful one in full and the
// tries in between as one step, within the recorder's cap. A run that gives up never says the formula has no answer.
partial class PPZ {
    // One move of a try: the variable set (an index into the variables), its value, and the clause that forced it (0-based), or
    // -1 when the value was random.
    private readonly record struct Set(int Variable, bool Value, int Clause);

    // How PPZ's tries read: each move sets the next variable of the try's order, forced by a clause or at random.
    private sealed class Steps(SatFormula formula, bool recording) : RestartingSearchSteps<Set>(formula, recording) {
        public void SetVariable(int variable, bool value, int clause) => Add(new Set(variable, value, clause));

        protected override string SolverLabel => "PPZ";
        protected override string MoveNoun => "step";
        protected override string EachTry => "sets every variable";
        protected override bool ChecksAtEnd => true;

        protected override void Apply(Set s, bool?[] values) => values[s.Variable] = s.Value;

        protected override string[] MoveFocus(Set s) => s.Clause < 0
            ? [VariableId(Formula.Variables[s.Variable])]
            : [ClauseId(s.Clause), VariableId(Formula.Variables[s.Variable])];

        // "Try 1: take the variables in a random order: x3, x1, x2." (the order is that of its moves)
        protected override string StartCaption(SatTry<Set> t, bool?[] start) {
            string intro = t.Number == 1 ? "Try 1: take the" : $"Restart. Try {t.Number}: take the";
            string random = t.Number == 1 ? "a random order" : "a new random order";
            return Formula.Variables.Count <= 8
                ? $"{intro} variables in {random}: {string.Join(", ", t.Moves.Select(m => Formula.Variables[m.Variable]))}."
                : $"{intro} {Formula.Variables.Count} variables in {random}.";
        }

        // "Step 2: set x1. Every other literal of C3 (x1 | !x2 | x3) is false, so C3 forces x1 = True."
        protected override string MoveCaption(SatTry<Set> t, Set s, bool?[] after, int number) {
            string name = Formula.Variables[s.Variable];
            return s.Clause < 0
                ? $"Step {number}: set {name}. No clause forces it, so it takes a random value: {name} = {s.Value}."
                : $"Step {number}: set {name}. Every other literal of {ClauseLabel(s.Clause)} is false, so C{s.Clause + 1} forces {name} = {s.Value}.";
        }

        protected override string AfterSkipped(bool?[] values) {
            int set = values.Count(v => v.HasValue);
            return $"{set} of the {Formula.Variables.Count} variables {(set == 1 ? "is" : "are")} set.";
        }

        protected override string FoundBy(SatTry<Set> t) => $"try {t.Number}";

        // Every variable is set: the check that ends the try.
        protected override void Close(StepRecorder<Assignment> rec, SatTry<Set> t, bool?[] end) {
            var falseClauses = FalseClauses(end);
            if (t.Solved)
                rec.Accept(() => new(ToAssignment(end), $"Try {t.Number} has set every variable: every clause has a true literal."));
            else
                rec.Reject(() => new(ToAssignment(end), $"Try {t.Number} has set every variable, but {Falsified(falseClauses, lower: true)}") {
                    Focus = falseClauses.Select(ClauseId).ToArray(),
                });
        }
    }
}
