namespace API.Interfaces.Steps;

/// <summary>
/// One try of a search that restarts, kept only while recording: its number (from 1), the values it started from, its moves
/// in order, whether it ended on an answer, and the values it ended on.
/// </summary>
sealed class RestartTry<TValues, TMove>(int number, TValues start) {
    public int Number { get; } = number;
    public TValues Start { get; } = start;
    public List<TMove> Moves { get; } = [];
    public bool Solved { get; set; }
    public TValues End { get; set; } = start;
}

/// <summary>
/// Records the steps of a randomized search that restarts from a new random start until a try succeeds (WalkSAT, Schöning,
/// PPZ). The search cannot know which try will succeed, so it reports each try as it runs (<see cref="Begin"/>,
/// <see cref="Add"/>, <see cref="End"/>), and this keeps only the first try, the current one and the last failed one. Once the
/// run is over, <see cref="Record"/> records the first try and the successful one in full and the tries in between as one
/// Reject, all within the recorder's cap. A try too long for its share keeps its first and last moves and one step for those
/// skipped, and the successful try gets at least half the room.
/// <para>
/// The solver says once, in the constructor, how its tries show: how to copy and change its values (<typeparamref name="TValues"/>,
/// such as a truth assignment), the partial answer and the focus of a move, and the words of every step. The words are
/// called only while recording, for the steps that are recorded.
/// </para>
/// <para>
/// A try's start is a Try (an Accept when it already ends the run), each move is a Try (the move that ends the run is the
/// Accept), and a failed try ends with a Reject. A search that judges its values only once its moves are done (PPZ) sets
/// <c>checksAtEnd</c>: every try then ends with that check, an Accept when it succeeds and a Reject when it fails.
/// </para>
/// <para>
/// Reporting does nothing while recording is off (the values are copied only when it is on) and draws nothing from the solver's
/// generator, so a run with steps and one without give the same answer. A solver that is not recording uses <see cref="Off"/>,
/// so a plain solve makes none of the converters.
/// </para>
/// </summary>
sealed class RestartSteps<TValues, TMove, TPartial> {
    private readonly StepRecorder<TPartial> _rec;
    private readonly Func<TValues, TValues> _copy;
    private readonly Action<TMove, TValues> _apply;
    private readonly Func<TValues, TPartial> _partial;
    private readonly Func<TMove, string[]> _moveFocus;
    private readonly Func<RestartTry<TValues, TMove>, TValues, string> _startWhy;
    private readonly Func<RestartTry<TValues, TMove>, TMove, TValues, int, string> _moveWhy;
    private readonly Func<RestartTry<TValues, TMove>, int, int, TValues, string> _skippedWhy;
    private readonly Func<RestartTry<TValues, TMove>, TValues, string> _closeWhy;
    private readonly Func<RestartTry<TValues, TMove>, TValues, string[]>? _closeFocus;
    private readonly Func<int, int, string> _betweenWhy;
    private readonly Func<RestartTry<TValues, TMove>, TValues, string> _solvedWhy;
    private readonly Func<int, string> _gaveUpWhy;
    private readonly bool _checksAtEnd;
    private RestartTry<TValues, TMove>? _first, _current;
    // The last failed try after the first, for the step that sums up the tries in between.
    private RestartTry<TValues, TMove>? _lastFailed;

    /// <summary>Steps for a run that is not recording: every call does nothing.</summary>
    public static RestartSteps<TValues, TMove, TPartial> Off { get; } = new();

    private RestartSteps() {
        _rec = StepRecorder<TPartial>.Off;
        _copy = null!;
        _apply = null!;
        _partial = null!;
        _moveFocus = null!;
        _startWhy = null!;
        _moveWhy = null!;
        _skippedWhy = null!;
        _closeWhy = null!;
        _betweenWhy = null!;
        _solvedWhy = null!;
        _gaveUpWhy = null!;
    }

    /// <param name="rec">The recorder the steps go to, once the run is over.</param>
    /// <param name="copy">A copy of the values, so a try keeps them as they were.</param>
    /// <param name="apply">Plays a move on the values.</param>
    /// <param name="partial">The partial answer the values show.</param>
    /// <param name="moveFocus">The ids a move is about (a clause, a variable).</param>
    /// <param name="startWhy">The caption of a try's start, from its start values.</param>
    /// <param name="moveWhy">The caption of a move, with the values after it and its number within the try (from 1).</param>
    /// <param name="skippedWhy">The caption of the step for the moves from..to (numbered from 1) not shown, with the values after them.</param>
    /// <param name="closeWhy">The caption of the step that ends a try (a failed one; every one with <paramref name="checksAtEnd"/>).</param>
    /// <param name="closeFocus">The ids that step is about; none by default.</param>
    /// <param name="betweenWhy">The caption of the one step for the failed tries from..to between the first and the last.</param>
    /// <param name="solvedWhy">The Done's caption when a try found an answer, with the values it ended on.</param>
    /// <param name="gaveUpWhy">The Done's caption when every try failed, with how many began.</param>
    /// <param name="checksAtEnd">Whether every try ends with a step that judges its values, even one that succeeds.</param>
    public RestartSteps(StepRecorder<TPartial> rec,
                        Func<TValues, TValues> copy,
                        Action<TMove, TValues> apply,
                        Func<TValues, TPartial> partial,
                        Func<TMove, string[]> moveFocus,
                        Func<RestartTry<TValues, TMove>, TValues, string> startWhy,
                        Func<RestartTry<TValues, TMove>, TMove, TValues, int, string> moveWhy,
                        Func<RestartTry<TValues, TMove>, int, int, TValues, string> skippedWhy,
                        Func<RestartTry<TValues, TMove>, TValues, string> closeWhy,
                        Func<int, int, string> betweenWhy,
                        Func<RestartTry<TValues, TMove>, TValues, string> solvedWhy,
                        Func<int, string> gaveUpWhy,
                        Func<RestartTry<TValues, TMove>, TValues, string[]>? closeFocus = null,
                        bool checksAtEnd = false) {
        _rec = rec;
        _copy = copy;
        _apply = apply;
        _partial = partial;
        _moveFocus = moveFocus;
        _startWhy = startWhy;
        _moveWhy = moveWhy;
        _skippedWhy = skippedWhy;
        _closeWhy = closeWhy;
        _closeFocus = closeFocus;
        _betweenWhy = betweenWhy;
        _solvedWhy = solvedWhy;
        _gaveUpWhy = gaveUpWhy;
        _checksAtEnd = checksAtEnd;
    }

    // How many tries have begun (counted only while recording).
    private int Begun { get; set; }

    // ---- What the solver reports ----

    /// <summary>A try begins from these values (copied when recording).</summary>
    public void Begin(TValues start) {
        if (!_rec.Enabled) return;
        Begun++;
        _current = new RestartTry<TValues, TMove>(Begun, _copy(start));
        _first ??= _current;
    }

    /// <summary>The current try makes a move.</summary>
    public void Add(TMove move) => _current?.Moves.Add(move);

    /// <summary>The current try ends on these values (copied when recording), with an answer or not.</summary>
    public void End(TValues end, bool solved) {
        if (_current == null) return;
        _current.End = _copy(end);
        _current.Solved = solved;
        if (!solved && _current != _first) _lastFailed = _current;
    }

    // ---- Recording ----

    /// <summary>
    /// Records the run once it is over: the first try, the tries in between as one step, the last try, and the Done; all within
    /// the recorder's cap. A run stopped before its first try ends on a Done that says so. Does nothing while recording is off.
    /// </summary>
    /// <param name="noAnswer">The answer of a Done with no answer (an empty one).</param>
    public void Record(TPartial noAnswer) {
        if (!_rec.Enabled) return;
        if (_first == null || _current == null) {
            _rec.Done(noAnswer, false, "Stopped before the first try: no answer found.");
            return;
        }
        RestartTry<TValues, TMove> first = _first, last = _current;
        bool solved = last.Solved;
        // Tries 2..k-1 when the k-th succeeds (k > 2), or 2..N when all N failed (N > 1): one summary step.
        int betweenFrom = 2, betweenTo = solved ? last.Number - 1 : Begun;
        bool summary = betweenTo >= betweenFrom;
        int available = Math.Max(1, _rec.Cap - (summary ? 1 : 0));
        if (solved && last != first) {
            // Both tries in full when they fit; else the successful try gets at least half the room.
            int len1 = Length(first), lenK = Length(last);
            int room1 = Math.Min(len1, available / 2);
            int roomK = Math.Min(lenK, available - room1);
            room1 = Math.Min(len1, available - roomK);
            RecordTry(first, room1);
            if (summary) RecordBetween(betweenFrom, betweenTo);
            RecordTry(last, roomK);
        } else {
            RecordTry(first, available);
            if (summary) RecordBetween(betweenFrom, betweenTo);
        }

        if (solved)
            _rec.Done(_partial(last.End), true, _solvedWhy(last, last.End));
        else
            _rec.Done(noAnswer, false, _gaveUpWhy(Begun));
    }

    // Whether the try ends with a closing step: always for a search that checks at the end, else only when it failed.
    private bool Closes(RestartTry<TValues, TMove> t) => _checksAtEnd || !t.Solved;

    // The steps a try takes in full: its start, its moves, and its closing step.
    private int Length(RestartTry<TValues, TMove> t) => 1 + t.Moves.Count + (Closes(t) ? 1 : 0);

    // Records one try in at most `room` steps (all of them when they fit, or when the room is too small to skip anything).
    private void RecordTry(RestartTry<TValues, TMove> t, int room) {
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

        TValues cur = _copy(t.Start);
        // The start: an Accept when it ends the run (it already is an answer), else a Try.
        if (t.Solved && moves == 0 && !closes)
            _rec.Accept(() => _partial(cur), () => _startWhy(t, cur));
        else
            _rec.Try(() => _partial(cur), () => _startWhy(t, cur));

        for (int i = 0; i < moves; i++) {
            var move = t.Moves[i];
            _apply(move, cur);
            if (i >= skipFrom && i < skipTo) {
                if (i == skipTo - 1) {
                    int from = skipFrom + 1, to = skipTo;
                    _rec.Try(() => _partial(cur), () => _skippedWhy(t, from, to, cur));
                }
                continue;
            }
            bool ends = t.Solved && !closes && i == moves - 1;
            int number = i + 1;
            Func<Step<TPartial>> step = () => new(_partial(cur), _moveWhy(t, move, cur, number)) { Focus = _moveFocus(move) };
            if (ends) _rec.Accept(step);
            else _rec.Try(step);
        }

        if (closes) {
            Func<Step<TPartial>> close = () => new(_partial(cur), _closeWhy(t, cur)) { Focus = _closeFocus?.Invoke(t, cur) ?? [] };
            if (t.Solved) _rec.Accept(close);
            else _rec.Reject(close);
        }
    }

    // One step for the failed tries from..to, drawn with the values the last of them ended on.
    private void RecordBetween(int from, int to) {
        var lastFailed = _lastFailed!;
        _rec.Reject(() => _partial(lastFailed.End), () => _betweenWhy(from, to));
    }
}
