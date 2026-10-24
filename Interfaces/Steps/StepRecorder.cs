using System.Globalization;

namespace API.Interfaces.Steps;

/// <summary>
/// Collects <see cref="SolverStep{TPartial}"/>s while a solver runs.
/// <para>
/// Guidance for solver authors: record decisions (choose, reject, back out), not loop iterations. A step's
/// caption is one plain sentence. Pass lambdas for the partial answer and the caption so that nothing is
/// built when recording is off or the cap has been reached.
/// </para>
/// <para>
/// Only the first <c>cap</c> non-<see cref="StepEvent.Done"/> steps are kept; the rest are counted in
/// <see cref="Hidden"/>. Past the cap the most recent <see cref="StepEvent.Accept"/> is also kept, and is placed
/// just before <see cref="Done"/>, so a brute-force run that spends the cap on failed tries still shows the step
/// that found the answer. <see cref="Done"/> is always kept, so a run holds at most <c>cap + 2</c> steps.
/// Past the cap the lambdas of an Accept are evaluated (the recorder cannot know it is the last Accept until
/// Done arrives, and a stored lambda would read the solver's state as it is at Done, not as it was at the
/// Accept); the lambdas of every other step past the cap are never evaluated.
/// </para>
/// <para>
/// A recorder made with a time limit checks the clock on every recorded step, kept or not, and throws
/// <see cref="StepBudgetExceededException"/> once the limit has passed. <see cref="Off"/> never checks the clock.
/// A <see cref="DeadlineOnly"/> recorder keeps no steps and invokes no lambda, like Off, but checks the clock on every
/// <see cref="ClockCheckInterval"/>th step (the first included), so a solve that records nothing still stops (#935).
/// </para>
/// </summary>
sealed class StepRecorder<TPartial> {
    public const int DefaultCap = 150;

    /// <summary>A shared recorder that is disabled: every method does nothing and no lambda is invoked.</summary>
    public static readonly StepRecorder<TPartial> Off = new(enabled: false, cap: 0, timeLimit: null);

    /// <summary>
    /// A recorder that keeps no steps but stops the solve once <paramref name="timeLimit"/> has passed: the solve gets
    /// <see cref="StepBudgetExceededException"/> at its next checked step. Used for plain solves (no picture), where
    /// recording would be wasted work. Make a new one for each run; it counts calls, so it must not be shared.
    /// </summary>
    public static StepRecorder<TPartial> DeadlineOnly(TimeSpan timeLimit) => new(enabled: false, cap: 0, timeLimit, deadlineOnly: true);

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

    /// <summary>The kept steps, in order.</summary>
    public IReadOnlyList<SolverStep<TPartial>> Steps => _steps;
    /// <summary>Every step the solver reported, kept or not (including Done).</summary>
    public int Total => _total;
    /// <summary>How many reported steps were not kept.</summary>
    public int Hidden => _total - _steps.Count;

    public void Try(Func<TPartial> partial, Func<string> caption, params string[] focus) => Add(StepEvent.Try, partial, caption, focus);
    public void Accept(Func<TPartial> partial, Func<string> caption, params string[] focus) => Add(StepEvent.Accept, partial, caption, focus);
    public void Reject(Func<TPartial> partial, Func<string> caption, params string[] focus) => Add(StepEvent.Reject, partial, caption, focus);
    public void Backtrack(Func<TPartial> partial, Func<string> caption, params string[] focus) => Add(StepEvent.Backtrack, partial, caption, focus);

    private void Add(StepEvent kind, Func<TPartial> partial, Func<string> caption, string[] focus) {
        if (!_enabled) {
            if (_deadlineOnly && (_calls++ & (ClockCheckInterval - 1)) == 0 && Now() >= _deadline)
                throw new StepBudgetExceededException(this);
            return;
        }
        _total++;
        if (_shownDecisions < _cap) {
            _shownDecisions++;
            _steps.Add(new SolverStep<TPartial>(partial(), kind, focus, caption()));
        } else if (kind == StepEvent.Accept) {
            _lastAcceptPastCap = new SolverStep<TPartial>(partial(), kind, focus, caption());
        }
        // Checked after the step is kept, so a stopped run always has at least one kept step (when cap > 0).
        if (Now() >= _deadline)
            throw new StepBudgetExceededException(this);
    }

    /// <summary>
    /// Records the final step. Always kept, after the last Accept past the cap if there was one; its caption gets
    /// " (n not shown)" when steps were hidden.
    /// </summary>
    public void Done(TPartial answer, bool ok, string caption) {
        if (!_enabled) return;
        if (_lastAcceptPastCap != null) {
            _steps.Add(_lastAcceptPastCap);
            _lastAcceptPastCap = null;
        }
        int hidden = Hidden;
        _total++;
        if (hidden > 0) caption += StepCaptions.NotShownSuffix(hidden);
        _steps.Add(new SolverStep<TPartial>(answer, StepEvent.Done, Array.Empty<string>(), caption) { Ok = ok });
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
        Done(last, false, $"{StepCaptions.StoppedPrefix}{seconds} s and {steps} {(reported == 1 ? "step" : "steps")}; the solver had not finished.");
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
    private static readonly System.Text.RegularExpressions.Regex NotShownPattern = new(@" \((\d+) not shown\)$");

    /// <summary>How the caption of a Done step for a run stopped by its time limit begins.</summary>
    public const string StoppedPrefix = "Stopped after ";

    /// <summary>Whether a Done caption is that of a run stopped by its time limit.</summary>
    public static bool IsStopped(string caption) => caption.StartsWith(StoppedPrefix, StringComparison.Ordinal);

    /// <summary>The note added to a Done caption when <paramref name="hidden"/> steps were not kept.</summary>
    public static string NotShownSuffix(int hidden) => $" ({hidden} not shown)";

    /// <summary>How many steps a Done caption says were not kept; 0 when it says nothing.</summary>
    public static int NotShown(string caption) {
        var match = NotShownPattern.Match(caption);
        return match.Success ? int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
    }
}
