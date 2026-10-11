using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Interfaces.Table;

/// <summary>
/// The trace table of an automaton run (DFA, NFA): the Table picture's second view of the steps the Automaton picture
/// draws (<see cref="ActiveStates"/>). One row per move, added as the run makes it: the step number, the symbol read (ε
/// for an ε move), the state or states before and after, and whether that is an accepting place to be. Nothing here
/// changes a solver; the table is read off the same recorded steps.
/// <list type="bullet">
/// <item>One run at a time (DFA Simulation, NFA Subset Simulation): every Try adds a row. A DFA's missing move is a row
/// into <c>Garbage</c>, and every branch of an NFA dying is a row into <c>∅</c>; both are <c>Rejected</c>.</item>
/// <item>A depth-first search (NFA Backtracking, recognised by its Backtrack steps): a Try adds a row and a Backtrack takes
/// it away, so the table is always the branch being explored. An Accept marks it <c>Solution</c> and a dead end
/// <c>Rejected</c>. The Done frame shows the first branch that accepted, or the last one that died.</item>
/// <item>The row just added, or gone back to, is the focus (<c>ElementHighlight</c>), and a new row's To cell is marked
/// changed. An Accept marks its row <c>Solution</c> and a Reject <c>Rejected</c>. In Done the last row is <c>Solution</c>
/// when the input is accepted and <c>Rejected</c> when not.</item>
/// <item>A step past the recorder's cap may not follow on from the one before: then a row of <c>…</c> stands for the
/// steps not shown, and the step's own row has <c>…</c> where it is not known. A DFA keeps the rows before it (they are
/// the start of its one run); an NFA keeps only its start row, since a search's backtracking may have been hidden.</item>
/// </list>
/// </summary>
static class AutomatonTraceTable {
    public const string Gap = "…";
    public const string Garbage = "Garbage";
    public const string Dead = "∅";

    /// <summary>The columns: Step, Read, From, To, Accepting?.</summary>
    public static TableSpec Spec(string? title = null) =>
        new(TableShape.Trace, [new("Step", "step"), new("Read", "read"), new("From", "from"), new("To", "to"), new("Accepting?", "accepting")]) {
            Title = title,
        };

    // A row: what is written, plus where the run is after it (null for Garbage and ∅, or a row of "…").
    private sealed record Row(string Step, string Read, string From, string To, string Accepting, string[]? States, int Position);

    /// <summary>
    /// One draft per step. <paramref name="input"/> is the input as symbols (the empty input is ""); a state set is written
    /// by <paramref name="write"/>; <paramref name="accepts"/> says whether a state accepts; <paramref name="noState"/> is
    /// what the run is in once it has no state left (<see cref="Garbage"/> for a DFA, <see cref="Dead"/> for an NFA).
    /// <paramref name="oneRun"/> says the steps can only be one run (a DFA): then the rows kept before steps that were not
    /// shown are a true start of the run, and stay. An NFA's steps may come from a search whose backtracking was not
    /// shown, so past them only the start row is kept.
    /// </summary>
    public static IReadOnlyList<TableDraft> Drafts(IReadOnlyList<SolverStep<ActiveStates>> steps, string input,
        Func<IReadOnlyList<string>, string> write, Func<string, bool> accepts, string noState, bool oneRun) {
        bool branches = steps.Any(s => s.Event == StepEvent.Backtrack);
        var rows = new List<Row>();
        List<Row>? firstAccepted = null, lastDied = null;
        var drafts = new List<TableDraft>();

        string Accepting(IReadOnlyList<string> states) => states.Any(accepts) ? "yes" : "no";
        string Symbol(int at) => at >= 0 && at < input.Length ? input[at].ToString() : Gap;

        Row Start(ActiveStates p) => new("0", "—", "—", write(p.States), Accepting(p.States), p.States, 0);

        // Steps were not shown before this one: a row of "…" stands for them. A search's branch at the cap need not lead to
        // where the run is now, so only its start row is kept; a single run's rows are a true start of it.
        void Unknown() {
            if (!oneRun && rows.Count > 1) rows.RemoveRange(1, rows.Count - 1);
            if (rows[^1].Step != Gap) rows.Add(new Row(Gap, Gap, Gap, Gap, Gap, null, -1));
        }

        // A row for a move to `p`, from the last row; "…" where the move does not follow on from it.
        Row Move(ActiveStates p) {
            var last = rows[^1];
            bool follows = last.States != null && (p.Position == last.Position || p.Position == last.Position + 1);
            if (!follows) {
                Unknown();
                return new Row(Num(p.Position), p.Position > 0 ? Symbol(p.Position - 1) : Gap, Gap, write(p.States), Accepting(p.States), p.States, p.Position);
            }
            string read = p.Position == last.Position ? AutomatonFrames.Epsilon : Symbol(p.Position - 1);
            return new Row(Num(rows.Count), read, last.To, write(p.States), Accepting(p.States), p.States, p.Position);
        }

        // Whether the run is where the last row left it: `p` names states of the last row, at its position.
        bool At(ActiveStates p) => rows.Count > 0 && rows[^1].States is { } s && rows[^1].Position == p.Position && p.States.All(s.Contains);

        // The row the run falls out of every state on: reading the symbol at `p`'s position leads nowhere.
        Row Fall(ActiveStates p) {
            var last = rows[^1];
            bool follows = last.States != null && last.Position == p.Position;
            if (!follows) Unknown();
            return new Row(follows ? Num(rows.Count) : Gap, Symbol(p.Position), follows ? last.To : Gap, noState, "no", null, p.Position);
        }

        StepEvent? previous = null;
        foreach (var step in steps) {
            var p = step.Partial;
            // Which row this step points at, and its state; -1: none.
            int marked = -1;
            PictureState state = PictureState.Background;
            bool added = false;
            var shown = rows;

            switch (step.Event) {
                case StepEvent.Try:
                    if (rows.Count == 0) rows.Add(Start(p));
                    else rows.Add(Move(p));
                    added = true;
                    marked = rows.Count - 1;
                    break;
                case StepEvent.Backtrack:
                    if (rows.Count > 1) rows.RemoveAt(rows.Count - 1);
                    if (rows.Count > 0 && rows[^1].Step == Gap) rows.RemoveAt(rows.Count - 1);
                    marked = rows.Count - 1;
                    break;
                case StepEvent.Accept:
                case StepEvent.Reject:
                    if (rows.Count == 0) {
                        rows.Add(Start(new ActiveStates(p.States.Length > 0 ? p.States : [], p.Position)));
                        if (p.States.Length == 0) rows[^1] = rows[^1] with { To = noState, Accepting = "no", States = null };
                    } else if (p.States.Length == 0) {
                        rows.Add(Fall(p));
                        added = true;
                    } else if (!At(p)) {
                        rows.Add(Move(p));
                        added = true;
                    }
                    marked = rows.Count - 1;
                    state = step.Event == StepEvent.Accept ? PictureState.Solution : PictureState.Rejected;
                    if (step.Event == StepEvent.Accept) firstAccepted ??= rows.ToList();
                    else lastDied = rows.ToList();
                    break;
                case StepEvent.Done:
                    if (branches) {
                        shown = (step.Ok == true ? firstAccepted : lastDied) ?? rows;
                    } else if (previous is StepEvent.Accept or StepEvent.Reject) {
                        // The step before already judged where the run ended.
                    } else if (rows.Count == 0) {
                        if (p.States.Length > 0) rows.Add(Start(p));
                    } else if (p.States.Length == 0) {
                        if (rows[^1].States != null) { rows.Add(Fall(p)); added = true; }
                    } else if (!At(p)) {
                        rows.Add(Move(p));
                        added = true;
                    }
                    marked = shown.Count - 1;
                    state = step.Ok == true ? PictureState.Solution : PictureState.Rejected;
                    break;
            }

            var draft = new TableDraft();
            foreach (var r in shown) draft.AddRow(r.Step, r.Read, r.From, r.To, r.Accepting);
            // A row the run cannot leave (Garbage, ∅) stays Rejected.
            for (int i = 0; i < shown.Count; i++)
                if (shown[i].States == null && shown[i].Step != Gap) draft.MarkRow(i, PictureState.Rejected);
            if (marked >= 0) {
                if (state != PictureState.Background) draft.MarkRow(marked, state);
                // An Accept's row is the answer (Solution), which a highlight would hide.
                if (step.Event is not (StepEvent.Done or StepEvent.Accept)) draft.FocusRow(marked);
                if (added) draft.MarkChanged(marked, 3);
            }
            drafts.Add(draft);
            previous = step.Event;
        }
        return drafts;
    }

    private static string Num(int n) => TableFrames.Num(n);
}
