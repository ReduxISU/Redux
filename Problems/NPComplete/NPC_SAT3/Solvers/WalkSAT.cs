using API.Interfaces;
using API.Interfaces.Steps;
using static API.Interfaces.Graphs.GroupedGraphFrames;
using static API.Interfaces.Steps.Words;

namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

// The steps WalkSAT records. Each try starts with a Try: its random start ("Restart." for every try after the first). Then
// one Try per flip: the clause it picked (no true literal) and the variable it flips are the focus, and the partial is the
// whole assignment after the flip, a candidate (WalkSAT is not incremental). A failed try ends with a Reject; the flip that
// makes every clause true is an Accept, followed by the Done.
//
// The search cannot know which try will succeed, so it reports its tries to RestartSteps as it runs, and RestartSteps
// records them once it is over: the first try and the successful one in full, the tries in between as one Reject
// ("Tries 2–37: ..."); a try too long for the recorder's cap keeps its first and last flips and says how many it skipped.
// Recording draws nothing from the generator, so the answer is the same with steps or without. When every try fails, the
// Done says it gave up: WalkSAT can miss an answer, so that is no proof that there is none.
class WalkSAT : ISolver<SAT3, Assignment>, IRandomizedSolver {

    // --- Fields ---
    public string solverName { get; } = "WalkSAT Algorithm";
    public string solverDefinition { get; } = "Repeats random-restart local search up to 100 times."
 + " In each try, starts with a random truth assignment and performs up to 300n flips (n = number of variables)."
 + " Each step selects a randomly chosen unsatisfied clause. If any literal in the clause can be flipped"
 + " without breaking any satisfied clauses (break-count = 0), it flips it immediately."
 + " Otherwise, with probability p, a random literal in the clause is flipped; with probability 1-p,"
 + " the literal with the minimum break-count is flipped.";

    public string source { get; } = "Bart Selman, Henry A. Kautz, and Bram Cohen. 1994. Noise strategies for improving local search. In AAAI '94.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://cdn.aaai.org/AAAI/1994/AAAI94-051.pdf";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Randomized local search: random choices make it Stochastic, and it has no
    // worst-case guarantee of finding an assignment within its tries, which makes it a Heuristic.
    public SolverType solverType { get; } = SolverType.Heuristic | SolverType.Stochastic;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    public MathematicalFunction timeComplexity { get; } = new("n * m", ", n = number of variables, m = number of clauses");
    // betweeen 0.5 and 0.6, empirically 0.57 proven best
    private readonly double _p = 0.57;
    private readonly int _maxTries = 100;
    // MaxFlips is typically scaled with n (e.g., 100 * n to 300 * n)
    private readonly int _flipsPerVariable = 300;

    // The seed of the next solve; explicit, so it never appears in /info.
    int? IRandomizedSolver.Seed { get; set; }

    public WalkSAT() { }

    // Fewer tries or flips, so tests can reach runs that restart.
    internal WalkSAT(int maxTries, int flipsPerVariable) {
        _maxTries = maxTries;
        _flipsPerVariable = flipsPerVariable;
    }

    // How a flip chose its variable: a free flip (it turns no clause false), a random one, or the one that turns the fewest
    // clauses false.
    private enum FlipKind { Free, Random, Greedy }

    // One flip of a try: the clause it repairs (0-based), the variable it flips (an index into the variables), how it chose
    // it, and how many clauses it turns false.
    private readonly record struct Flip(int Clause, int Variable, FlipKind Kind, int Breaks);

    // How the steps show a try. A run that is not recording gets RestartSteps.Off and makes none of these; RestartSteps calls
    // them only while recording, for the steps it records.
    private static RestartSteps<bool[], Flip, Assignment> NewSteps(SatFormula formula, int maxFlips, StepRecorder<Assignment> rec) => !rec.Enabled ? RestartSteps<bool[], Flip, Assignment>.Off : new(rec,
        // What every picture shows: the whole assignment, and for a flip the clause it picked and the variable it flips.
        copy: values => (bool[])values.Clone(),
        apply: (f, values) => values[f.Variable] = !values[f.Variable],
        partial: values => ToAssignment(formula.Values(values)),
        moveFocus: f => [ClauseId(f.Clause), VariableId(formula.Variables[f.Variable])],
        // What each step says. "Try 1: start from a random assignment, x1 = True, x2 = False. C2 has no true literal."
        startWhy: (t, start) => {
            string intro = t.Number == 1 ? "Try 1: start from a random assignment" : $"Restart. Try {t.Number}: start from a new random assignment";
            string listed = formula.Variables.Count <= 8 ? $", {Assigned(formula.Values(start))}" : $" of the {formula.Variables.Count} variables";
            var falseClauses = formula.FalseClauses(start);
            return falseClauses.Count == 0
                ? $"{intro}{listed}: every clause has a true literal."
                : $"{intro}{listed}. {ClauseNames(falseClauses)} {(falseClauses.Count == 1 ? "has" : "have")} no true literal.";
        },
        // "Flip 3: C2 (x1 | !x2 | x3) has no true literal. Flip x2 to False: a free flip, no clause turns false. Now ..."
        moveWhy: (t, f, after, number) => {
            string flip = $"{formula.Variables[f.Variable]} to {after[f.Variable]}";
            string turns = f.Breaks == 0 ? "no clause turns false" : $"{Count(f.Breaks, "clause")} {(f.Breaks == 1 ? "turns" : "turn")} false";
            string how = f.Kind switch {
                FlipKind.Free => $"Flip {flip}: a free flip, {turns}.",
                FlipKind.Random => $"No variable of C{f.Clause + 1} flips free, so flip one at random: {flip}, and {turns}.",
                _ => $"No variable of C{f.Clause + 1} flips free, so flip the one that turns the fewest clauses false: {flip}, and {turns}.",
            };
            var still = formula.FalseClauses(after);
            string now = still.Count == 0 ? "Every clause now has a true literal." : $"Now {ClauseNames(still)} {(still.Count == 1 ? "has" : "have")} no true literal.";
            return $"Flip {number}: C{f.Clause + 1} {ClauseText(formula.Written[f.Clause])} has no true literal. {how} {now}";
        },
        skippedWhy: (t, from, to, after) => {
            var still = formula.FalseClauses(after);
            return $"{(from == to ? $"Flip {from} of try {t.Number} is" : $"Flips {from}–{to} of try {t.Number} are")} not shown one by one. After them, "
                + (still.Count == 0 ? "every clause has a true literal." : $"{ClauseNames(still)} {(still.Count == 1 ? "has" : "have")} no true literal.");
        },
        closeWhy: (t, end) => {
            var still = formula.FalseClauses(end);
            return $"Try {t.Number} ends after {Count(t.Moves.Count, "flip")}: "
                + (still.Count == 0 ? "every clause has a true literal." : $"{ClauseNames(still)} {(still.Count == 1 ? "has" : "have")} no true literal.");
        },
        betweenWhy: (from, to) => from == to
            ? $"Try {from} ends after {Count(maxFlips, "flip")}: no satisfying assignment."
            : $"Tries {from}–{to}: no satisfying assignment (each ends after {Count(maxFlips, "flip")}).",
        solvedWhy: (t, end) => $"Every clause has a true literal under {Assigned(formula.Values(end))}: found by "
            + (t.Moves.Count == 0 ? $"the random start of try {t.Number}." : $"try {t.Number} after {Count(t.Moves.Count, "flip")}."),
        gaveUpWhy: begun => $"Gave up after {Count(begun, "try", "tries")}: no answer found. WalkSAT can miss an answer, so the formula may still have one.");

    public string Solve(SAT3 sat3, StepRecorder<Assignment> rec) {
        // The order of first appearance, so the same seed gives the same run in every process (a HashSet's order would not).
        var formula = SatFormula.Of(sat3);
        int n = formula.Variables.Count;
        if (n == 0) {
            rec.Done(new Assignment([]), false, "The formula has no variables to set: no answer found.");
            return "{}";
        }
        var clauses = formula.Clauses;

        int maxFlips = _flipsPerVariable * n;
        Random rnd = RandomizedSolvers.Generator(((IRandomizedSolver)this).Seed);
        var steps = NewSteps(formula, maxFlips, rec);
        bool[]? solution = null;

        for (int tryIdx = 0; tryIdx < _maxTries && solution == null; tryIdx++) {
            if (timerHasExpired) break; // stop at the time limit
            rec.CheckTime();   // every try, so a run whose first try succeeds is still stopped at the limit
            // Generate random assignment
            bool[] assignments = new bool[n];
            for (int v = 0; v < n; v++) assignments[v] = rnd.Next(2) == 0;
            steps.Begin(assignments);

            for (int flip = 0; flip <= maxFlips; flip++) {
                if (timerHasExpired) break; // stop at the time limit (the try ends here; see below)
                // Identify unsatisfied clauses
                var unsatisfied = new List<int>();
                for (int c = 0; c < clauses.Count; c++)
                    if (!SatFormula.Holds(clauses[c], assignments)) unsatisfied.Add(c);

                // If all clauses are satisfied, we found a solution
                if (unsatisfied.Count == 0) {
                    solution = assignments;
                    break;
                }
                if (flip == maxFlips) break;   // out of flips: the try failed
                rec.CheckTime();

                // Pick an unsatisfied clause uniformly at random
                int clause = unsatisfied[rnd.Next(unsatisfied.Count)];

                // Select variable to flip using standard WalkSAT heuristic
                var (varToFlip, kind, breaks) = PickWalkSATVariable(clauses[clause], assignments, clauses, rnd);

                // Flip assignment
                assignments[varToFlip] = !assignments[varToFlip];
                steps.Add(new Flip(clause, varToFlip, kind, breaks));
            }

            steps.End(assignments, solution != null);
        }

        steps.Record(new Assignment([]));
        // Stopped by the time limit, not out of tries: that is no verdict at all.
        if (solution == null && timerHasExpired) return SolveRun.TimedOutAnswer;
        return solution == null ? "{}" : formula.Certificate(solution); // "{}": failure to satisfy within caps
    }

    private (int Variable, FlipKind Kind, int Breaks) PickWalkSATVariable(
        (int Variable, bool Negated)[] clause,
        bool[] assignments,
        List<(int Variable, bool Negated)[]> clauses,
        Random rnd) {

        List<int> candidates = clause.Select(lit => lit.Variable).Distinct().ToList();

        // Compute break-count for each variable candidate in the clause
        var breakCounts = new List<int>(candidates.Count);
        List<int> zeroBreakVars = new List<int>();

        foreach (int var in candidates) {
            int breaks = CountBreaks(var, assignments, clauses);
            breakCounts.Add(breaks);
            if (breaks == 0) {
                zeroBreakVars.Add(var);
            }
        }

        if (zeroBreakVars.Count > 0) {
            return (zeroBreakVars[rnd.Next(zeroBreakVars.Count)], FlipKind.Free, 0);
        }

        if (rnd.NextDouble() < _p) {
            int pick = rnd.Next(candidates.Count);
            return (candidates[pick], FlipKind.Random, breakCounts[pick]);
        } else {
            int minBreaks = breakCounts.Min();
            List<int> bestVars = candidates.Where((_, i) => breakCounts[i] == minBreaks).ToList();
            return (bestVars[rnd.Next(bestVars.Count)], FlipKind.Greedy, minBreaks);
        }
    }

    private static int CountBreaks(int varToFlip, bool[] assignments, List<(int Variable, bool Negated)[]> clauses) {
        int breaks = 0;

        foreach (var clause in clauses) {
            // Fast check: is varToFlip even in this clause?
            bool containsVar = false;
            foreach (var lit in clause) if (lit.Variable == varToFlip) { containsVar = true; break; }
            if (!containsVar) continue;

            bool currentlySatisfied = false;
            bool satisfiedAfterFlip = false;

            foreach (var (variable, negated) in clause) {
                bool currentVal = assignments[variable];
                if (currentVal != negated) {
                    currentlySatisfied = true;
                }

                bool nextVal = variable == varToFlip ? !currentVal : currentVal;
                if (nextVal != negated) {
                    satisfiedAfterFlip = true;
                }
            }

            // A break occurs when a previously satisfied clause becomes unsatisfied
            if (currentlySatisfied && !satisfiedAfterFlip) {
                breaks++;
            }
        }

        return breaks;
    }
}
