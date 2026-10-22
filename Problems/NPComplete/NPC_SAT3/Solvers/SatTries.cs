using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System;
using System.Collections.Generic;
using System.Linq;

namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

/// <summary>
/// A 3SAT formula as a randomized solver works on it (#932): its variables in order of first appearance (never a HashSet, so a
/// seed gives the same run in every process), and each clause as (variable index, negated) pairs, in the order written.
/// </summary>
sealed class SatFormula {
    public List<string> Variables { get; }
    /// <summary>The clauses as the instance writes them, for captions.</summary>
    public List<List<string>> Written { get; }
    public List<(int Variable, bool Negated)[]> Clauses { get; }

    private SatFormula(List<string> variables, List<List<string>> written, List<(int Variable, bool Negated)[]> clauses) {
        Variables = variables;
        Written = written;
        Clauses = clauses;
    }

    public static SatFormula Of(SAT3 sat3) {
        List<string> variables = sat3.literals.Where(l => l.Length > 0).Select(l => l.TrimStart('!')).Distinct().ToList();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int v = 0; v < variables.Count; v++) index[variables[v]] = v;
        var clauses = sat3.clauses
            .Select(c => c.Where(l => l.Length > 0).Select(l => (Variable: index[l.TrimStart('!')], Negated: l.StartsWith('!'))).ToArray())
            .ToList();
        return new SatFormula(variables, sat3.clauses, clauses);
    }

    /// <summary>Whether the clause has a true literal under a whole assignment.</summary>
    public static bool Holds((int Variable, bool Negated)[] clause, bool[] values) {
        foreach (var (variable, negated) in clause)
            if (values[variable] != negated) return true;
        return false;
    }

    /// <summary>Whether the clause has a true literal under a partial assignment (null: not set yet).</summary>
    public static bool Holds((int Variable, bool Negated)[] clause, bool?[] values) {
        foreach (var (variable, negated) in clause)
            if (values[variable] is bool value && value != negated) return true;
        return false;
    }

    /// <summary>The certificate of a whole assignment: "(x1:True,x2:False)".</summary>
    public string Certificate(bool[] values) =>
        "(" + string.Join(",", Variables.Select((name, v) => name + ":" + values[v])) + ")";
}

/// <summary>
/// One try of a randomized SAT solver, kept only while recording (#932): its number (from 1), the values it started from (null:
/// not set), its moves in order, whether it ended on an answer, and the values it ended on.
/// </summary>
sealed class SatTry<TMove>(int number, bool?[] start) {
    public int Number { get; } = number;
    public bool?[] Start { get; } = start;
    public List<TMove> Moves { get; } = [];
    public bool Solved { get; set; }
    public bool?[] End { get; set; } = start;
}

/// <summary>
/// Records the steps of a randomized SAT solver that makes tries (WalkSAT, Schöning, PPZ; #932). The solver cannot know which
/// try will succeed, so it reports each try here as it runs (<c>Begin</c>, <see cref="Add"/>, <c>End</c>), and
/// this keeps only the first try, the current one and the last failed one; once the run is over, <see cref="Record"/> records
/// the first try and the successful one in full and the tries in between as one Reject ("Tries 2–37: ..."), all within the
/// recorder's cap. A try too long for its share keeps its first and last moves and one step for those skipped, and the
/// successful try gets at least half the room. The Done names the try that found the answer; when every try fails it says the
/// solver gave up, and never that the formula has no answer.
/// <para>
/// A subclass says how its tries read: <see cref="Apply"/> plays a move on the values, <see cref="MoveCaption"/> and
/// <see cref="MoveFocus"/> describe it. By default a try reads like a local search: its start is a whole random assignment (a
/// Try, or an Accept when it already makes every clause true), each move is a Try (the move that makes every clause true is the
/// Accept), and a failed try ends with a Reject. A solver that only judges the assignment once its moves are done (PPZ) sets
/// <see cref="ChecksAtEnd"/> and overrides <see cref="StartCaption"/> and <see cref="Close"/>.
/// </para>
/// <para>
/// Reporting draws nothing from the solver's generator and does nothing while recording is off (the values are copied only when
/// it is on), so a run with steps and one without give the same answer.
/// </para>
/// </summary>
abstract class SatTries<TMove> {
    protected SatFormula Formula { get; }
    private readonly bool _recording;
    private SatTry<TMove>? _first, _current;
    // The last failed try after the first, for the step that sums up the tries in between.
    private SatTry<TMove>? _lastFailed;

    protected SatTries(SatFormula formula, bool recording) {
        Formula = formula;
        _recording = recording;
    }

    /// <summary>How many tries have begun.</summary>
    public int Begun { get; private set; }

    // ---- What a subclass says ----

    /// <summary>The solver as captions name it: "WalkSAT".</summary>
    protected abstract string SolverLabel { get; }

    /// <summary>What one move is called, lower case: "flip".</summary>
    protected abstract string MoveNoun { get; }

    /// <summary>What every failed try does, for the step that sums up the tries in between: "ends after 600 flips".</summary>
    protected abstract string EachTry { get; }

    /// <summary>Plays a move on the values.</summary>
    protected abstract void Apply(TMove move, bool?[] values);

    /// <summary>The caption of the move, numbered from 1 within its try; <paramref name="after"/> holds the values after it.</summary>
    protected abstract string MoveCaption(SatTry<TMove> t, TMove move, bool?[] after, int number);

    /// <summary>The ids the move is about (a clause, a variable).</summary>
    protected abstract string[] MoveFocus(TMove move);

    /// <summary>
    /// Whether a try ends with a step that judges its values (<see cref="Close"/>, an Accept or a Reject) even when it succeeds.
    /// Otherwise the move (or the start) that makes every clause true is the Accept, and only a failed try is closed.
    /// </summary>
    protected virtual bool ChecksAtEnd => false;

    /// <summary>"Try 1: start from a random assignment, x1 = True, x2 = False. C2 has no true literal."</summary>
    protected virtual string StartCaption(SatTry<TMove> t, bool?[] start) {
        string intro = t.Number == 1
            ? "Try 1: start from a random assignment"
            : $"Restart. Try {t.Number}: start from a new random assignment";
        var startFalse = FalseClauses(start);
        return startFalse.Count == 0
            ? $"{intro}{Listed(start)}: every clause has a true literal."
            : $"{intro}{Listed(start)}. {Falsified(startFalse)}";
    }

    /// <summary>The step that ends a try: by default the Reject of a failed one ("Try 1 ends after 600 flips: ...").</summary>
    protected virtual void Close(StepRecorder<Assignment> rec, SatTry<TMove> t, bool?[] end) =>
        rec.Reject(() => ToAssignment(end),
            () => $"Try {t.Number} ends after {Count(t.Moves.Count, MoveNoun)}: {Falsified(FalseClauses(end), lower: true)}");

    /// <summary>What the step for skipped moves says about the values after them: "C2 has no true literal."</summary>
    protected virtual string AfterSkipped(bool?[] values) => Falsified(FalseClauses(values), lower: true);

    /// <summary>How the Done names the try that found the answer: "try 3 after 12 flips".</summary>
    protected virtual string FoundBy(SatTry<TMove> t) => t.Moves.Count == 0
        ? $"the random start of try {t.Number}"
        : $"try {t.Number} after {Count(t.Moves.Count, MoveNoun)}";

    // ---- What the solver reports ----

    /// <summary>A try begins from these values (copied when recording).</summary>
    public void Begin(bool[] start) {
        Begun++;
        if (_recording) Track(Array.ConvertAll(start, v => (bool?)v));
    }

    /// <summary>A try begins from these values (null: not set; copied when recording).</summary>
    public void Begin(bool?[] start) {
        Begun++;
        if (_recording) Track((bool?[])start.Clone());
    }

    private void Track(bool?[] start) {
        _current = new SatTry<TMove>(Begun, start);
        _first ??= _current;
    }

    /// <summary>The current try makes a move.</summary>
    public void Add(TMove move) => _current?.Moves.Add(move);

    /// <summary>The current try ends on these values, with an answer or not.</summary>
    public void End(bool[] end, bool solved) {
        if (_current != null) Finish(Array.ConvertAll(end, v => (bool?)v), solved);
    }

    /// <summary>The current try ends on these values (null: not set), with an answer or not.</summary>
    public void End(bool?[] end, bool solved) {
        if (_current != null) Finish((bool?[])end.Clone(), solved);
    }

    private void Finish(bool?[] end, bool solved) {
        _current!.End = end;
        _current.Solved = solved;
        if (!solved && _current != _first) _lastFailed = _current;
    }

    /// <summary>The Done of a formula with no variables: there is nothing to try.</summary>
    public static void NoVariables(StepRecorder<Assignment> rec) =>
        rec.Done(new Assignment([]), false, "The formula has no variables to set: no answer found.");

    // ---- Recording ----

    /// <summary>
    /// Records the run once it is over: the first try, the tries in between as one step, the last try, and the Done; all within
    /// the recorder's cap. Does nothing while recording is off.
    /// </summary>
    public void Record(StepRecorder<Assignment> rec) {
        if (!rec.Enabled) return;
        if (_first == null || _current == null) {
            rec.Done(new Assignment([]), false, "Stopped before the first try: no answer found.");
            return;
        }
        SatTry<TMove> first = _first, last = _current;
        bool solved = last.Solved;
        // Tries 2..k-1 when the k-th succeeds (k > 2), or 2..N when all N failed (N > 1): one summary step.
        int betweenFrom = 2, betweenTo = solved ? last.Number - 1 : Begun;
        bool summary = betweenTo >= betweenFrom;
        int available = Math.Max(1, rec.Cap - (summary ? 1 : 0));
        if (solved && last != first) {
            // Both tries in full when they fit; else the successful try gets at least half the room.
            int len1 = Length(first), lenK = Length(last);
            int room1 = Math.Min(len1, available / 2);
            int roomK = Math.Min(lenK, available - room1);
            room1 = Math.Min(len1, available - roomK);
            RecordTry(rec, first, room1);
            if (summary) RecordBetween(rec, betweenFrom, betweenTo);
            RecordTry(rec, last, roomK);
        } else {
            RecordTry(rec, first, available);
            if (summary) RecordBetween(rec, betweenFrom, betweenTo);
        }

        if (solved) {
            var answer = Values(last.End);
            rec.Done(ToAssignment(answer), true, $"Every clause has a true literal under {GroupedGraphFrames.Assigned(answer)}: found by {FoundBy(last)}.");
        } else {
            rec.Done(new Assignment([]), false,
                $"Gave up after {Count(Begun, "try", "tries")}: no answer found. {SolverLabel} can miss an answer, so the formula may still have one.");
        }
    }

    // Whether the try ends with a closing step: always for a solver that checks at the end, else only when it failed.
    private bool Closes(SatTry<TMove> t) => ChecksAtEnd || !t.Solved;

    // The steps a try takes in full: its start, its moves, and its closing step.
    private int Length(SatTry<TMove> t) => 1 + t.Moves.Count + (Closes(t) ? 1 : 0);

    // Records one try in at most `room` steps (all of them when they fit, or when the room is too small to skip anything).
    private void RecordTry(StepRecorder<Assignment> rec, SatTry<TMove> t, int room) {
        int moves = t.Moves.Count;
        bool closes = Closes(t);
        // Move steps that fit besides the start, the skip step and the closing step.
        int shown = room - 2 - (closes ? 1 : 0);
        int head = moves, tail = 0;
        if (Length(t) > room && shown >= 2) {
            head = (shown + 1) / 2;
            tail = shown - head;
        }
        int skipFrom = head, skipTo = moves - tail;   // moves [skipFrom, skipTo) are skipped (0-based)

        bool?[] cur = (bool?[])t.Start.Clone();
        // The start: an Accept when it ends the run (it already makes every clause true), else a Try.
        if (t.Solved && moves == 0 && !closes)
            rec.Accept(() => ToAssignment(cur), () => StartCaption(t, cur));
        else
            rec.Try(() => ToAssignment(cur), () => StartCaption(t, cur));

        string plural = Capitalized(MoveNoun) + "s", singular = Capitalized(MoveNoun);
        for (int i = 0; i < moves; i++) {
            var move = t.Moves[i];
            Apply(move, cur);
            if (i >= skipFrom && i < skipTo) {
                if (i == skipTo - 1) {
                    int from = skipFrom + 1, to = skipTo;
                    rec.Try(() => ToAssignment(cur),
                        () => $"{Range(plural, singular, from, to)} of try {t.Number} {(from == to ? "is" : "are")} not shown one by one. After them, {AfterSkipped(cur)}");
                }
                continue;
            }
            bool ends = t.Solved && !closes && i == moves - 1;
            int number = i + 1;
            Func<Assignment> partial = () => ToAssignment(cur);
            Func<string> caption = () => MoveCaption(t, move, cur, number);
            string[] focus = MoveFocus(move);
            if (ends) rec.Accept(partial, caption, focus);
            else rec.Try(partial, caption, focus);
        }

        if (closes) Close(rec, t, cur);
    }

    // One step for the failed tries from..to, drawn with the values the last of them ended on.
    private void RecordBetween(StepRecorder<Assignment> rec, int from, int to) {
        var lastFailed = _lastFailed!;
        rec.Reject(() => ToAssignment(lastFailed.End),
            () => from == to
                ? $"Try {from} {EachTry}: no satisfying assignment."
                : $"Tries {from}–{to}: no satisfying assignment (each {EachTry}).");
    }

    // ---- Caption helpers ----

    /// <summary>The 0-based indexes of the clauses with no true literal under <paramref name="values"/>.</summary>
    protected List<int> FalseClauses(bool?[] values) {
        var list = new List<int>();
        for (int c = 0; c < Formula.Clauses.Count; c++)
            if (Formula.Clauses[c].Length > 0 && !SatFormula.Holds(Formula.Clauses[c], values)) list.Add(c);
        return list;
    }

    /// <summary>"C2 and C3 have no true literal.", "5 clauses have no true literal." (more than three), "every clause has a true literal."</summary>
    protected static string Falsified(List<int> clauses, bool lower = false) {
        string text = clauses.Count switch {
            0 => "every clause has a true literal.",
            1 => $"C{clauses[0] + 1} has no true literal.",
            <= 3 => $"{string.Join(", ", clauses.SkipLast(1).Select(c => $"C{c + 1}"))} and C{clauses[^1] + 1} have no true literal.",
            _ => $"{clauses.Count} clauses have no true literal.",
        };
        return lower || text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
    }

    /// <summary>", x1 = True, x2 = False" for up to eight variables; " of the 12 variables" for more.</summary>
    protected string Listed(bool?[] values) =>
        Formula.Variables.Count <= 8 ? $", {GroupedGraphFrames.Assigned(Values(values))}" : $" of the {Formula.Variables.Count} variables";

    /// <summary>"C2 (x1 | !x2 | x3)".</summary>
    protected string ClauseLabel(int clause) => $"C{clause + 1} {GroupedGraphFrames.ClauseText(Formula.Written[clause])}";

    protected static string Range(string plural, string singular, int from, int to) => from == to ? $"{singular} {from}" : $"{plural} {from}–{to}";

    protected static string Count(int count, string singular, string? plural = null) => $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";

    private static string Capitalized(string word) => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];

    /// <summary>The variables that are set, with their values, in variable order.</summary>
    protected List<KeyValuePair<string, bool>> Values(bool?[] values) {
        var list = new List<KeyValuePair<string, bool>>();
        for (int v = 0; v < Formula.Variables.Count; v++)
            if (values[v] is bool value) list.Add(new KeyValuePair<string, bool>(Formula.Variables[v], value));
        return list;
    }

    /// <summary>The partial of a step: the variables that are set (an unset one is left out, so a picture draws it as Background).</summary>
    protected Assignment ToAssignment(bool?[] values) => ToAssignment(Values(values));

    protected static Assignment ToAssignment(IEnumerable<KeyValuePair<string, bool>> values) =>
        new(values.Select(p => new KeyValuePair<string, string>(p.Key, p.Value.ToString())));
}
