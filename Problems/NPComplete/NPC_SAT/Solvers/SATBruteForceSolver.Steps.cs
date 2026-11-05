using API.Interfaces.Logic;
using API.Interfaces.Steps;
using static API.Interfaces.Graphs.GroupedGraphFrames;

namespace API.Problems.NPComplete.NPC_SAT.Solvers;

// The steps SAT Brute Force records: one per candidate assignment, in the order the counter tries them. A candidate is a
// Reject naming the first clause with no true literal, or the Accept that ends the search; then the Done. The recorder's
// cap hides the rest of a long run.
public partial class SATBruteForceSolver {
    private sealed class Steps(CnfFormula formula, StepRecorder<Assignment> rec) {
        private readonly List<List<string>> _clauses = formula.ClauseStrings();

        // Its own timer stopped it: there is no answer, so the run ends on a Done that says so (never on a candidate).
        public void TimedOut() => rec.Done(new Assignment([]), false, SolverTimeoutCaption);

        public void Satisfied(Dictionary<string, bool> assignment) {
            rec.Accept(() => new(ToAssignment(assignment), $"Try {Assigned(assignment)}: every clause has a true literal."));
            rec.Done(ToAssignment(assignment), true, $"The formula is satisfied by {Assigned(assignment)}.");
        }

        public void Falsified(Dictionary<string, bool> assignment, int clause) =>
            rec.Reject(() => new(ToAssignment(assignment),
                $"Try {Assigned(assignment)}: clause C{clause + 1} {ClauseText(_clauses[clause])} has no true literal.") {
                Focus = [ClauseId(clause)],
            });

        public void NoneSatisfies() =>
            rec.Done(new Assignment([]), false, "No assignment makes every clause true, so no solution exists.");

        private static Assignment ToAssignment(Dictionary<string, bool> values) =>
            new(values.Select(p => new KeyValuePair<string, string>(p.Key, p.Value.ToString())));
    }
}
