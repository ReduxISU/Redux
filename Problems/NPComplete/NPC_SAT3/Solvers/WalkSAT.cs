using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System;
using System.Collections.Generic;
using System.Linq;

namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

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
    public SolverType solverType { get; } = SolverType.Stochastic;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    public MathematicalFunction timeComplexity { get; } = new("n * m", ", n = number of variables, m = number of clauses");
    // betweeen 0.5 and 0.6, empirically 0.57 proven best
    private readonly double _p = 0.57;
    private readonly int _maxTries = 100;
    // MaxFlips is typically scaled with n (e.g., 100 * n to 300 * n)
    private readonly int _flipsPerVariable = 300;

    // The seed of the next solve (#932); explicit, so it never appears in /info.
    int? IRandomizedSolver.Seed { get; set; }

    public WalkSAT() { }

    // Fewer tries or flips, so tests can reach runs that restart (#932).
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

    // One try, kept only while recording: where it started, its flips, and whether it ended on an answer.
    private sealed class TryTrace(int number, bool[] start) {
        public int Number { get; } = number;
        public bool[] Start { get; } = start;
        public List<Flip> Flips { get; } = [];
        public bool Solved { get; set; }
        // The assignment it ended on.
        public bool[] End { get; set; } = start;
    }

    // Steps: each try starts with a Try (its random start; "Restart." for every try after the first), then one step per flip: the
    // clause it picked (no true literal) and the variable it flips are the focus, and the partial is the whole assignment after
    // the flip, a candidate (WalkSAT is not incremental). A failed try ends with a Reject; the flip that makes every clause true
    // is an Accept, followed by the Done. Steps are held back while the search runs (it cannot know which try will succeed), then
    // recorded: the first try and the successful one in full, the tries in between as one Reject ("Tries 2–37: ..."); a try too
    // long for the recorder's cap keeps its first and last flips and says how many it skipped. Recording draws nothing from the
    // generator, so the answer is the same with steps or without. When every try fails, the Done says it gave up: WalkSAT can
    // miss an answer, so that is no proof that there is none.
    public string Solve(SAT3 sat3, StepRecorder<Assignment> rec) {
        // The order of first appearance, so the same seed gives the same run in every process (a HashSet's order would not).
        List<string> variables = sat3.literals.Where(l => l.Length > 0).Select(l => l.TrimStart('!')).Distinct().ToList();
        int n = variables.Count;
        if (n == 0) {
            rec.Done(new Assignment([]), false, "The formula has no variables to set: no answer found.");
            return "{}";
        }
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int v = 0; v < n; v++) index[variables[v]] = v;
        // Each clause as (variable index, negated) pairs, in the order written.
        var clauses = sat3.clauses
            .Select(c => c.Where(l => l.Length > 0).Select(l => (Variable: index[l.TrimStart('!')], Negated: l.StartsWith('!'))).ToArray())
            .ToList();

        int maxFlips = _flipsPerVariable * n;
        Random rnd = RandomizedSolvers.Generator(((IRandomizedSolver)this).Seed);

        TryTrace? first = null, current = null;
        TryTrace? lastFailed = null;   // the last failed try after the first, for the summary of the tries in between
        bool[]? solution = null;
        int tries = 0;

        for (int tryIdx = 0; tryIdx < _maxTries && solution == null; tryIdx++) {
            tries++;
            // Generate random assignment
            bool[] assignments = new bool[n];
            for (int v = 0; v < n; v++) assignments[v] = rnd.Next(2) == 0;
            if (rec.Enabled) {
                current = new TryTrace(tries, (bool[])assignments.Clone());
                first ??= current;
            }

            for (int flip = 0; flip <= maxFlips; flip++) {
                // Identify unsatisfied clauses
                var unsatisfied = new List<int>();
                for (int c = 0; c < clauses.Count; c++)
                    if (!IsClauseSatisfied(clauses[c], assignments)) unsatisfied.Add(c);

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
                current?.Flips.Add(new Flip(clause, varToFlip, kind, breaks));
            }

            if (current != null) {
                current.End = (bool[])assignments.Clone();
                current.Solved = solution != null;
                if (!current.Solved && current != first) lastFailed = current;
            }
        }

        if (rec.Enabled)
            Record(rec, variables, sat3.clauses, clauses, first!, current!, lastFailed, tries, maxFlips);

        if (solution == null)
            return "{}"; // Failure to satisfy within caps

        string certificate = string.Join(",", variables.Select((name, v) => name + ":" + solution[v]));
        return "(" + certificate + ")";
    }

    private static bool IsClauseSatisfied((int Variable, bool Negated)[] clause, bool[] assignments) {
        foreach (var (variable, negated) in clause)
            if (assignments[variable] != negated) return true;
        return false;
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

    // ---- Recording ----

    // Records the run once it is over: the first try, the tries in between as one step, the last try, and the Done; all within
    // the recorder's cap (a try too long for its share keeps its first and last flips and one step for those skipped).
    private static void Record(StepRecorder<Assignment> rec, List<string> variables, List<List<string>> written,
        List<(int Variable, bool Negated)[]> clauses, TryTrace first, TryTrace last, TryTrace? lastFailed, int tries, int maxFlips) {
        bool solved = last.Solved;
        // Tries 2..k-1 when the k-th succeeds (k > 2), or 2..N when all N failed (N > 1): one summary step.
        int betweenFrom = 2, betweenTo = solved ? last.Number - 1 : tries;
        bool summary = betweenTo >= betweenFrom;
        int available = Math.Max(1, rec.Cap - (summary ? 1 : 0));
        if (solved && last != first) {
            // Both tries in full when they fit; else the successful try gets at least half the room.
            int len1 = Length(first), lenK = Length(last);
            int room1 = Math.Min(len1, available / 2);
            int roomK = Math.Min(lenK, available - room1);
            room1 = Math.Min(len1, available - roomK);
            RecordTry(rec, variables, written, clauses, first, room1);
            if (summary) RecordBetween(rec, variables, betweenFrom, betweenTo, lastFailed!, maxFlips);
            RecordTry(rec, variables, written, clauses, last, roomK);
        } else {
            RecordTry(rec, variables, written, clauses, first, available);
            if (summary) RecordBetween(rec, variables, betweenFrom, betweenTo, lastFailed!, maxFlips);
        }

        if (solved) {
            var answer = Values(variables, last.End);
            string how = last.Flips.Count == 0
                ? $"the random start of try {last.Number}"
                : $"try {last.Number} after {Count(last.Flips.Count, "flip")}";
            rec.Done(ToAssignment(answer), true, $"Every clause has a true literal under {GroupedGraphFrames.Assigned(answer)}: found by {how}.");
        } else {
            rec.Done(new Assignment([]), false,
                $"Gave up after {Count(tries, "try", "tries")}: no answer found. WalkSAT can miss an answer, so the formula may still have one.");
        }
    }

    // The steps a try takes in full: its start, its flips, and the Reject that ends a failed try.
    private static int Length(TryTrace t) => 1 + t.Flips.Count + (t.Solved ? 0 : 1);

    // Records one try in at most `room` steps (all of them when they fit, or when the room is too small to skip anything).
    private static void RecordTry(StepRecorder<Assignment> rec, List<string> variables, List<List<string>> written,
        List<(int Variable, bool Negated)[]> clauses, TryTrace t, int room) {
        int flips = t.Flips.Count;
        // Flip steps that fit besides the start, the skip step and a failed try's Reject.
        int shown = room - 2 - (t.Solved ? 0 : 1);
        int head = flips, tail = 0;
        if (Length(t) > room && shown >= 2) {
            head = (shown + 1) / 2;
            tail = shown - head;
        }
        int skipFrom = head, skipTo = flips - tail;   // flips [skipFrom, skipTo) are skipped (0-based)

        bool[] cur = (bool[])t.Start.Clone();
        // The start: an Accept when it already makes every clause true (it ends the run), else a Try.
        var startFalse = FalseClauses(clauses, cur);
        string intro = t.Number == 1
            ? "Try 1: start from a random assignment"
            : $"Restart. Try {t.Number}: start from a new random assignment";
        if (startFalse.Count == 0 && t.Solved)
            rec.Accept(() => ToAssignment(Values(variables, cur)), () => $"{intro}{Listed(variables, cur)}: every clause has a true literal.");
        else
            rec.Try(() => ToAssignment(Values(variables, cur)), () => $"{intro}{Listed(variables, cur)}. {Falsified(startFalse)}");

        for (int i = 0; i < flips; i++) {
            var f = t.Flips[i];
            cur[f.Variable] = !cur[f.Variable];
            if (i >= skipFrom && i < skipTo) {
                if (i == skipTo - 1) {
                    int from = skipFrom + 1, to = skipTo;
                    rec.Try(() => ToAssignment(Values(variables, cur)),
                        () => $"{Range("Flips", "Flip", from, to)} of try {t.Number} {(from == to ? "is" : "are")} not shown one by one. After them, {Falsified(FalseClauses(clauses, cur), lower: true)}");
                }
                continue;
            }
            bool ends = t.Solved && i == flips - 1;
            int number = i + 1;
            Func<Assignment> partial = () => ToAssignment(Values(variables, cur));
            Func<string> caption = () => FlipCaption(variables, written, clauses, f, cur, number);
            string[] focus = [GroupedGraphFrames.ClauseId(f.Clause), GroupedGraphFrames.VariableId(variables[f.Variable])];
            if (ends) rec.Accept(partial, caption, focus);
            else rec.Try(partial, caption, focus);
        }

        if (!t.Solved)
            rec.Reject(() => ToAssignment(Values(variables, cur)),
                () => $"Try {t.Number} ends after {Count(flips, "flip")}: {Falsified(FalseClauses(clauses, cur), lower: true)}");
    }

    // One step for the failed tries from..to, drawn with the assignment the last of them ended on.
    private static void RecordBetween(StepRecorder<Assignment> rec, List<string> variables, int from, int to, TryTrace lastFailed, int maxFlips) =>
        rec.Reject(() => ToAssignment(Values(variables, lastFailed.End)),
            () => from == to
                ? $"Try {from} ends after {Count(maxFlips, "flip")}: no satisfying assignment."
                : $"Tries {from}–{to}: no satisfying assignment (each ends after {Count(maxFlips, "flip")}).");

    // "C2 (x1 | !x2 | x3) has no true literal. Flip x2 to False: a free flip, no clause turns false. Now ..."
    private static string FlipCaption(List<string> variables, List<List<string>> written, List<(int Variable, bool Negated)[]> clauses,
        Flip f, bool[] after, int number) {
        string clause = $"C{f.Clause + 1} {GroupedGraphFrames.ClauseText(written[f.Clause])}";
        string flip = $"{variables[f.Variable]} to {after[f.Variable]}";
        string turns = f.Breaks == 0 ? "no clause turns false" : $"{Count(f.Breaks, "clause")} {(f.Breaks == 1 ? "turns" : "turn")} false";
        string how = f.Kind switch {
            FlipKind.Free => $"Flip {flip}: a free flip, {turns}.",
            FlipKind.Random => $"No variable of C{f.Clause + 1} flips free, so flip one at random: {flip}, and {turns}.",
            _ => $"No variable of C{f.Clause + 1} flips free, so flip the one that turns the fewest clauses false: {flip}, and {turns}.",
        };
        var still = FalseClauses(clauses, after);
        string now = still.Count == 0 ? "Every clause now has a true literal." : $"Now {Falsified(still, lower: true)}";
        return $"Flip {number}: {clause} has no true literal. {how} {now}";
    }

    // The 0-based indexes of the clauses with no true literal under `values`.
    private static List<int> FalseClauses(List<(int Variable, bool Negated)[]> clauses, bool[] values) {
        var list = new List<int>();
        for (int c = 0; c < clauses.Count; c++)
            if (clauses[c].Length > 0 && !IsClauseSatisfied(clauses[c], values)) list.Add(c);
        return list;
    }

    // "C2 and C3 have no true literal.", "5 clauses have no true literal." (more than three), "every clause has a true literal."
    private static string Falsified(List<int> clauses, bool lower = false) {
        string text = clauses.Count switch {
            0 => "every clause has a true literal.",
            1 => $"C{clauses[0] + 1} has no true literal.",
            <= 3 => $"{string.Join(", ", clauses.SkipLast(1).Select(c => $"C{c + 1}"))} and C{clauses[^1] + 1} have no true literal.",
            _ => $"{clauses.Count} clauses have no true literal.",
        };
        return lower || text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
    }

    // ", x1 = True, x2 = False" for up to eight variables; " of the 12 variables" for more.
    private static string Listed(List<string> variables, bool[] values) =>
        variables.Count <= 8 ? $", {GroupedGraphFrames.Assigned(Values(variables, values))}" : $" of the {variables.Count} variables";

    private static string Range(string plural, string singular, int from, int to) => from == to ? $"{singular} {from}" : $"{plural} {from}–{to}";

    private static string Count(int count, string singular, string? plural = null) => $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";

    private static List<KeyValuePair<string, bool>> Values(List<string> variables, bool[] values) =>
        variables.Select((name, v) => new KeyValuePair<string, bool>(name, values[v])).ToList();

    private static Assignment ToAssignment(IEnumerable<KeyValuePair<string, bool>> values) =>
        new(values.Select(p => new KeyValuePair<string, string>(p.Key, p.Value.ToString())));
}
