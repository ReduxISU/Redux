using System.Globalization;
using System.Runtime.CompilerServices;

namespace API.Interfaces.Steps;

/// <summary>
/// Collects <see cref="SolverStep{TPartial}"/>s while a solver runs.
/// <para>
/// Guidance for solver authors: record decisions (choose, reject, back out), not loop iterations. A step's
/// caption is one plain sentence. Say the event first, then the partial answer as a lambda, the caption as an interpolated
/// string and the ids the step is about: <c>rec.Reject(() =&gt; partial, $"{x} = {v} fails: ...", id)</c>. The caption is a
/// <see cref="ShownText"/> (an Accept's a <see cref="KeptText"/>), so nothing in its braces is evaluated, and the lambda is not
/// invoked, when recording is off or the cap has been reached. The older forms, the whole step as one lambda,
/// <c>rec.Try(() =&gt; new(partial, caption) { Focus = [id] })</c> (see <see cref="Step{TPartial}"/>), or the partial answer and
/// caption as separate lambdas, do the same. A new solver records through one of the two set-once forms on top of this
/// recorder, which say once how a state shows and leave each step the keyword, the state and why: a solver that does not branch
/// through a <see cref="RunSteps{TState, TPartial}"/>, and one that branches through a <see cref="SearchSteps{TState, TChoice, TPartial}"/>,
/// which also builds the search-tree nodes.
/// </para>
/// <para>
/// Only the first <c>cap</c> non-<see cref="StepEvent.Done"/> steps are kept; the rest are counted in
/// <see cref="Hidden"/>. Past the cap the most recent <see cref="StepEvent.Accept"/> is also kept, and is placed
/// just before <see cref="Done(TPartial, bool, string)"/>, so a brute-force run that spends the cap on failed tries still shows the step
/// that found the answer. <see cref="Done(TPartial, bool, string)"/> is always kept, so a run holds at most <c>cap + 2</c> steps.
/// Past the cap the lambdas of an Accept are evaluated (the recorder cannot know it is the last Accept until
/// Done arrives, and a stored lambda would read the solver's state as it is at Done, not as it was at the
/// Accept); the lambdas of every other step past the cap are never evaluated.
/// </para>
/// <para>
/// A recorder made with a time limit checks the clock on every recorded step, kept or not, and on
/// <see cref="Done(TPartial, bool, string)"/>, and throws <see cref="StepBudgetExceededException"/> once the limit has passed.
/// <see cref="Off"/> never checks the clock.
/// A <see cref="DeadlineOnly"/> recorder keeps no steps and invokes no lambda, like Off, but checks the clock on every
/// <see cref="ClockCheckInterval"/>th step (the first included), so a solve that records nothing still stops (#935).
/// </para>
/// </summary>
sealed class StepRecorder<TPartial> : IStepGate {
    public const int DefaultCap = 150;

    /// <summary>A shared recorder that is disabled: every method does nothing and no lambda is invoked.</summary>
    public static readonly StepRecorder<TPartial> Off = new(enabled: false, cap: 0, timeLimit: null);

    /// <summary>
    /// A recorder that keeps no steps but stops the solve once <paramref name="timeLimit"/> has passed: the solve gets
    /// <see cref="StepBudgetExceededException"/> at its next checked step. Used for plain solves (no picture), where
    /// recording would be wasted work. Make a new one for each run; it counts calls, so it must not be shared.
    /// </summary>
    public static StepRecorder<TPartial> DeadlineOnly(TimeSpan timeLimit) => new(enabled: false, cap: 0, timeLimit, deadlineOnly: true);

    /// <summary>A <see cref="DeadlineOnly"/> recorder that reads the time from <paramref name="clock"/> (milliseconds), for tests.</summary>
    internal static StepRecorder<TPartial> DeadlineOnlyForTest(TimeSpan timeLimit, Func<long> clock) =>
        new(enabled: false, cap: 0, timeLimit, clock, deadlineOnly: true);

    /// <summary>
    /// How often a <see cref="DeadlineOnly"/> recorder reads the clock: every this many steps (a power of two). Reading
    /// Environment.TickCount64 costs a few nanoseconds, so this keeps the check far below the cost of the closures the
    /// solver already allocates for each step, while a solver that reports a step every few microseconds still stops
    /// within a millisecond of the limit.
    /// </summary>
    public const int ClockCheckInterval = 64;

    private readonly bool _enabled;
    private readonly int _cap;
    private readonly List<SolverStep<TPartial>> _steps = new();
    private int _total;
    private int _shownDecisions;
    // The most recent Accept reported past the cap, built when it was reported; added just before Done.
    private SolverStep<TPartial>? _lastAcceptPastCap;
    private readonly TimeSpan? _timeLimit;
    // Environment.TickCount64 at which the time limit runs out; long.MaxValue when there is no limit.
    private readonly long _deadline;
    // A test's clock in milliseconds, read instead of Environment.TickCount64; null outside tests.
    private readonly Func<long>? _clock;
    // A DeadlineOnly recorder: disabled, but checks the clock every ClockCheckInterval calls, counted in _calls.
    private readonly bool _deadlineOnly;
    private int _calls;

    public StepRecorder(int cap = DefaultCap, TimeSpan? timeLimit = null) : this(enabled: true, cap, timeLimit) { }

    /// <summary>A recorder that reads the time from <paramref name="clock"/> (milliseconds), so a test decides when the limit passes.</summary>
    internal StepRecorder(int cap, TimeSpan timeLimit, Func<long> clock) : this(enabled: true, cap, timeLimit, clock) { }

    private StepRecorder(bool enabled, int cap, TimeSpan? timeLimit, Func<long>? clock = null, bool deadlineOnly = false) {
        _enabled = enabled;
        _deadlineOnly = deadlineOnly;
        _cap = cap;
        _timeLimit = timeLimit;
        _clock = clock;
        _deadline = timeLimit is { } limit ? Now() + (long)Math.Ceiling(limit.TotalMilliseconds) : long.MaxValue;
    }

    private long Now() => _clock == null ? Environment.TickCount64 : _clock();

    /// <summary>
    /// False for <see cref="Off"/>. Callers that must prepare something to record (a list of ids, a
    /// per-pair bucket) check this first, so that turning recording off costs nothing at all.
    /// </summary>
    public bool Enabled => _enabled;

    /// <summary>
    /// How many non-Done steps this recorder keeps (0 for <see cref="Off"/>). A solver that chooses what to show itself (a
    /// randomized solver that keeps its first and its successful try, #932) reads it to fit what it records within the cap.
    /// </summary>
    public int Cap => _enabled ? _cap : 0;

    /// <summary>
    /// Throws <see cref="StepBudgetExceededException"/> once the time limit has passed, like recording a step would, without
    /// recording one. For a solver that holds its steps back while it runs (a randomized solver records its tries only once it
    /// knows which one succeeds, #932), so that a long run is still stopped by the limit. A <see cref="DeadlineOnly"/> recorder
    /// counts it as a step, checking the clock every <see cref="ClockCheckInterval"/>th call (#935). Does nothing for
    /// <see cref="Off"/>.
    /// </summary>
    public void CheckTime() {
        if (!_enabled) { StepWhileOff(); return; }
        if (Now() >= _deadline)
            throw new StepBudgetExceededException(this);
    }

    /// <summary>The kept steps, in order.</summary>
    public IReadOnlyList<SolverStep<TPartial>> Steps => _steps;
    /// <summary>Every step the solver reported, kept or not (including Done).</summary>
    public int Total => _total;
    /// <summary>How many reported steps were not kept.</summary>
    public int Hidden => _total - _steps.Count;

    /// <summary>
    /// Whether the time limit has passed, for a recording or <see cref="DeadlineOnly"/> recorder. Always false for
    /// <see cref="Off"/> and for a recorder with no limit, which never read the clock here.
    /// </summary>
    internal bool Expired => _timeLimit != null && Now() >= _deadline;

    public void Try(Func<TPartial> partial, Func<string> caption, params string[] focus) => Add(StepEvent.Try, partial, caption, focus);
    public void Accept(Func<TPartial> partial, Func<string> caption, params string[] focus) => Add(StepEvent.Accept, partial, caption, focus);
    public void Reject(Func<TPartial> partial, Func<string> caption, params string[] focus) => Add(StepEvent.Reject, partial, caption, focus);
    public void Backtrack(Func<TPartial> partial, Func<string> caption, params string[] focus) => Add(StepEvent.Backtrack, partial, caption, focus);

    // The same, with the focus built lazily too, for focus that is costly to build on every step of a hot loop.
    public void Try(Func<TPartial> partial, Func<string> caption, Func<string[]> focus) => Add(StepEvent.Try, partial, caption, focus);
    public void Accept(Func<TPartial> partial, Func<string> caption, Func<string[]> focus) => Add(StepEvent.Accept, partial, caption, focus);
    public void Reject(Func<TPartial> partial, Func<string> caption, Func<string[]> focus) => Add(StepEvent.Reject, partial, caption, focus);
    public void Backtrack(Func<TPartial> partial, Func<string> caption, Func<string[]> focus) => Add(StepEvent.Backtrack, partial, caption, focus);

    // The same, with the search-tree nodes the step creates or updates (SolverStep.Tree; the last is the node the step is
    // about), for a solver that branches. Built lazily like the rest, and only for steps that are kept.
    public void Try(Func<TPartial> partial, Func<string> caption, Func<string[]> focus, Func<SearchNode[]> tree) => Add(StepEvent.Try, partial, caption, focus, tree);
    public void Accept(Func<TPartial> partial, Func<string> caption, Func<string[]> focus, Func<SearchNode[]> tree) => Add(StepEvent.Accept, partial, caption, focus, tree);
    public void Reject(Func<TPartial> partial, Func<string> caption, Func<string[]> focus, Func<SearchNode[]> tree) => Add(StepEvent.Reject, partial, caption, focus, tree);
    public void Backtrack(Func<TPartial> partial, Func<string> caption, Func<string[]> focus, Func<SearchNode[]> tree) => Add(StepEvent.Backtrack, partial, caption, focus, tree);

    // The same, with the whole step built by one lambda: rec.Reject(() => new(partial, caption) { Focus = [id] }). It is
    // invoked at most once, only for a step that is kept, and records exactly what the overloads above record.
    public void Try(Func<Step<TPartial>> step) => Add(StepEvent.Try, step);
    public void Accept(Func<Step<TPartial>> step) => Add(StepEvent.Accept, step);
    public void Reject(Func<Step<TPartial>> step) => Add(StepEvent.Reject, step);
    public void Backtrack(Func<Step<TPartial>> step) => Add(StepEvent.Backtrack, step);

    // Keyword-first: the event, the partial answer (a lambda), the caption (an interpolated string, formatted only for a step
    // that is kept) and the ids the step is about. The ids are evaluated at the call, but no array is made unless the step is
    // kept; pass a lambda instead for ids that cost something to build.
    public void Try(Func<TPartial> partial, [InterpolatedStringHandlerArgument("")] ShownText caption, params ReadOnlySpan<string> focus) => Add(StepEvent.Try, partial, caption.ToStringAndClear(), focus);
    public void Accept(Func<TPartial> partial, [InterpolatedStringHandlerArgument("")] KeptText caption, params ReadOnlySpan<string> focus) => Add(StepEvent.Accept, partial, caption.ToStringAndClear(), focus);
    public void Reject(Func<TPartial> partial, [InterpolatedStringHandlerArgument("")] ShownText caption, params ReadOnlySpan<string> focus) => Add(StepEvent.Reject, partial, caption.ToStringAndClear(), focus);
    public void Backtrack(Func<TPartial> partial, [InterpolatedStringHandlerArgument("")] ShownText caption, params ReadOnlySpan<string> focus) => Add(StepEvent.Backtrack, partial, caption.ToStringAndClear(), focus);

    public void Try(Func<TPartial> partial, [InterpolatedStringHandlerArgument("")] ShownText caption, Func<string[]> focus) => Add(StepEvent.Try, partial, caption.ToStringAndClear(), focus);
    public void Accept(Func<TPartial> partial, [InterpolatedStringHandlerArgument("")] KeptText caption, Func<string[]> focus) => Add(StepEvent.Accept, partial, caption.ToStringAndClear(), focus);
    public void Reject(Func<TPartial> partial, [InterpolatedStringHandlerArgument("")] ShownText caption, Func<string[]> focus) => Add(StepEvent.Reject, partial, caption.ToStringAndClear(), focus);
    public void Backtrack(Func<TPartial> partial, [InterpolatedStringHandlerArgument("")] ShownText caption, Func<string[]> focus) => Add(StepEvent.Backtrack, partial, caption.ToStringAndClear(), focus);

    bool IStepGate.Recording => _enabled;
    bool IStepGate.ShowsNextStep => _enabled && _shownDecisions < _cap;

    /// <summary>Whether a step reported now does anything: recording, or a <see cref="DeadlineOnly"/> recorder counting it.</summary>
    internal bool CountsSteps => _enabled || _deadlineOnly;

    /// <summary>Whether a step of <paramref name="kind"/> reported now will be built (see <see cref="Count"/>).</summary>
    internal bool Builds(StepEvent kind) => _enabled && (_shownDecisions < _cap || kind == StepEvent.Accept);

    // The keyword-first forms: the caption was formatted by its handler, so it is null exactly when the step is not built.
    private void Add(StepEvent kind, Func<TPartial> partial, string? caption, ReadOnlySpan<string> focus) {
        if (!_enabled) { StepWhileOff(); return; }
        if (Count(kind))
            Keep(new SolverStep<TPartial>(partial(), kind, focus.ToArray(), caption!));
        CheckDeadline();
    }

    private void Add(StepEvent kind, Func<TPartial> partial, string? caption, Func<string[]> focus) {
        if (!_enabled) { StepWhileOff(); return; }
        if (Count(kind))
            Keep(new SolverStep<TPartial>(partial(), kind, focus(), caption!));
        CheckDeadline();
    }

    /// <summary>
    /// A step its caller has already built, for <see cref="StepsCore{TPartial}"/> (RunSteps and SearchSteps): it asked <see cref="Builds"/>
    /// first, and built the partial, caption, focus and tree only if it said yes (they are default otherwise, and not read).
    /// </summary>
    internal void AddBuilt(StepEvent kind, TPartial partial, string? caption, string[]? focus, SearchNode[]? tree) {
        if (!_enabled) { StepWhileOff(); return; }
        if (Count(kind))
            Keep(new SolverStep<TPartial>(partial, kind, focus!, caption!) { Tree = tree });
        CheckDeadline();
    }

    private void Add(StepEvent kind, Func<TPartial> partial, Func<string> caption, string[] focus) {
        if (!_enabled) { StepWhileOff(); return; }
        Add(kind, partial, caption, () => focus);
    }

    // A disabled recorder keeps nothing; a DeadlineOnly one still checks the clock every ClockCheckInterval steps.
    private void StepWhileOff() {
        if (_deadlineOnly && (_calls++ & (ClockCheckInterval - 1)) == 0 && Now() >= _deadline)
            throw new StepBudgetExceededException(this);
    }

    private void Add(StepEvent kind, Func<TPartial> partial, Func<string> caption, Func<string[]> focus, Func<SearchNode[]>? tree = null) {
        if (!_enabled) { StepWhileOff(); return; }
        if (Count(kind))
            Keep(new SolverStep<TPartial>(partial(), kind, focus(), caption()) { Tree = tree?.Invoke() });
        CheckDeadline();
    }

    private void Add(StepEvent kind, Func<Step<TPartial>> step) {
        if (!_enabled) { StepWhileOff(); return; }
        if (Count(kind)) {
            var s = step();
            Keep(new SolverStep<TPartial>(s.Partial, kind, s.Focus, s.Caption) { Tree = s.Tree });
        }
        CheckDeadline();
    }

    // Counts a reported step, and says whether it must be built: it is within the cap, or an Accept past it.
    private bool Count(StepEvent kind) {
        _total++;
        return _shownDecisions < _cap || kind == StepEvent.Accept;
    }

    // Keeps a built step: within the cap it is added; past it (an Accept) it replaces the last Accept past the cap.
    private void Keep(SolverStep<TPartial> step) {
        if (_shownDecisions < _cap) {
            _shownDecisions++;
            _steps.Add(step);
        } else {
            _lastAcceptPastCap = step;
        }
    }

    // Checked after the step is kept, so a stopped run always has at least one kept step (when cap > 0).
    private void CheckDeadline() {
        if (Now() >= _deadline)
            throw new StepBudgetExceededException(this);
    }

    /// <summary>
    /// Records the final step. Always kept, after the last Accept past the cap if there was one; its caption gets
    /// " (n not shown)" when steps were hidden, before its closing full stop if it has one. Past the time limit it throws
    /// instead and records nothing, so a run that ran out of time while working out its answer is stopped rather than
    /// reported as finished.
    /// </summary>
    public void Done(TPartial answer, bool ok, string caption) => Done(answer, ok, caption, null);

    /// <summary>
    /// <see cref="Done(TPartial, bool, string)"/> with search-tree nodes (<see cref="SolverStep{TPartial}.Tree"/>), for a
    /// solver whose answer's node might not be in the kept steps: past the cap, the last Accept kept need not be the one that
    /// found the answer. A solver that branches names the answer's path here, root first, so the tree ends on it.
    /// </summary>
    public void Done(TPartial answer, bool ok, string caption, Func<SearchNode[]>? tree) {
        if (!_enabled) return;
        if (Expired)
            throw new StepBudgetExceededException(this);
        AddDone(answer, ok, caption, tree);
    }

    // Done without the clock check, so DoneStopped can close a run whose time has already run out.
    private void AddDone(TPartial answer, bool ok, string caption, Func<SearchNode[]>? tree = null) {
        if (_lastAcceptPastCap != null) {
            _steps.Add(_lastAcceptPastCap);
            _lastAcceptPastCap = null;
        }
        int hidden = Hidden;
        _total++;
        if (hidden > 0) caption = StepCaptions.WithNotShown(caption, hidden);
        _steps.Add(new SolverStep<TPartial>(answer, StepEvent.Done, Array.Empty<string>(), caption) { Ok = ok, Tree = tree?.Invoke() });
    }

    /// <summary>
    /// Closes a run that was stopped by the time limit: a Done step with ok = false whose caption says the solver had
    /// not finished. The solver has no answer, so for an answer shape (see <see cref="Shapes.EmptyAnswer"/>) the partial
    /// is the empty answer: the last candidate it looked at was not an answer, and a picture must not draw it as one.
    /// For <see cref="ActiveStates"/> (and any other shape) it is the last kept partial, default when nothing was kept:
    /// the states the run is in are where it stopped, not a claimed answer.
    /// </summary>
    internal void DoneStopped() {
        if (!_enabled) return;
        int reported = _total;
        TPartial last = Shapes.EmptyAnswer(typeof(TPartial)) is TPartial empty ? empty
            : _lastAcceptPastCap != null ? _lastAcceptPastCap.Partial
            : _steps.Count > 0 ? _steps[^1].Partial
            : default!;
        string seconds = (_timeLimit ?? TimeSpan.Zero).TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        string steps = reported.ToString("N0", CultureInfo.InvariantCulture);
        AddDone(last, false, $"{StepCaptions.StoppedPrefix}{seconds} s and {steps} {(reported == 1 ? "step" : "steps")}; the solver had not finished.");
    }

    /// <summary>
    /// Closes a run whose solver returned after the time limit: its own Done step, if it recorded one, is taken out
    /// and the run ends with the stopped Done of <see cref="DoneStopped"/> instead, so there is only one Done.
    /// </summary>
    internal void ReplaceDoneWithStopped() {
        if (!_enabled) return;
        if (_steps.Count > 0 && _steps[^1].Event == StepEvent.Done) {
            _steps.RemoveAt(_steps.Count - 1);
            _total--;
        }
        DoneStopped();
    }
}

/// <summary>
/// Thrown by a <see cref="StepRecorder{TPartial}"/> made with a time limit when a step is recorded after the limit
/// has passed. <see cref="SolveRun.Record{TPartial}"/> catches it and returns a stopped run. Solvers must not catch it.
/// </summary>
sealed class StepBudgetExceededException : Exception {
    /// <summary>The recorder whose time limit ran out.</summary>
    public object Recorder { get; }
    public StepBudgetExceededException(object recorder) : base("The solver ran past its time limit.") {
        Recorder = recorder;
    }
}

/// <summary>What a Done caption says about steps the recorder did not keep, and about a run stopped by its time limit.</summary>
static class StepCaptions {
    private static readonly System.Text.RegularExpressions.Regex NotShownPattern = new(@" \((\d+) not shown\)\.?$");

    /// <summary>How the caption of a Done step for a run stopped by its time limit begins.</summary>
    public const string StoppedPrefix = "Stopped after ";

    /// <summary>Whether a Done caption is that of a run stopped by its time limit.</summary>
    public static bool IsStopped(string caption) => caption.StartsWith(StoppedPrefix, StringComparison.Ordinal);

    /// <summary>The note added to a Done caption when <paramref name="hidden"/> steps were not kept.</summary>
    public static string NotShownSuffix(int hidden) => $" ({hidden} not shown)";

    /// <summary>
    /// <paramref name="caption"/> with the note for <paramref name="hidden"/> steps not kept: before its closing full stop
    /// if it has one ("found after 191 orders (40 not shown)."), else at the end.
    /// </summary>
    public static string WithNotShown(string caption, int hidden) => caption.EndsWith('.')
        ? caption[..^1] + NotShownSuffix(hidden) + "."
        : caption + NotShownSuffix(hidden);

    /// <summary>How many steps a Done caption says were not kept; 0 when it says nothing.</summary>
    public static int NotShown(string caption) {
        var match = NotShownPattern.Match(caption);
        return match.Success ? int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
    }
}
