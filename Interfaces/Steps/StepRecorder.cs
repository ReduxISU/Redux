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
/// A recorder made with a time limit checks the clock on every recorded step, kept or not, and on
/// <see cref="Done"/>, and throws <see cref="StepBudgetExceededException"/> once the limit has passed.
/// <see cref="Off"/> never checks the clock.
/// </para>
/// </summary>
sealed class StepRecorder<TPartial> {
    public const int DefaultCap = 150;

    /// <summary>A shared recorder that is disabled: every method does nothing and no lambda is invoked.</summary>
    public static readonly StepRecorder<TPartial> Off = new(enabled: false, cap: 0, timeLimit: null);

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

    public StepRecorder(int cap = DefaultCap, TimeSpan? timeLimit = null) : this(enabled: true, cap, timeLimit) { }

    /// <summary>A recorder that reads the time from <paramref name="clock"/> (milliseconds), so a test decides when the limit passes.</summary>
    internal StepRecorder(int cap, TimeSpan timeLimit, Func<long> clock) : this(enabled: true, cap, timeLimit, clock) { }

    private StepRecorder(bool enabled, int cap, TimeSpan? timeLimit, Func<long>? clock = null) {
        _enabled = enabled;
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

    /// <summary>Whether the time limit has passed. Always false for <see cref="Off"/> and for a recorder with no limit.</summary>
    internal bool Expired => _enabled && Now() >= _deadline;

    public void Try(Func<TPartial> partial, Func<string> caption, params string[] focus) => Add(StepEvent.Try, partial, caption, focus);
    public void Accept(Func<TPartial> partial, Func<string> caption, params string[] focus) => Add(StepEvent.Accept, partial, caption, focus);
    public void Reject(Func<TPartial> partial, Func<string> caption, params string[] focus) => Add(StepEvent.Reject, partial, caption, focus);
    public void Backtrack(Func<TPartial> partial, Func<string> caption, params string[] focus) => Add(StepEvent.Backtrack, partial, caption, focus);

    private void Add(StepEvent kind, Func<TPartial> partial, Func<string> caption, string[] focus) {
        if (!_enabled) return;
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
    /// " (n not shown)" when steps were hidden. Past the time limit it throws instead and records nothing, so a run
    /// that ran out of time while working out its answer is stopped rather than reported as finished.
    /// </summary>
    public void Done(TPartial answer, bool ok, string caption) {
        if (!_enabled) return;
        if (Expired)
            throw new StepBudgetExceededException(this);
        AddDone(answer, ok, caption);
    }

    // Done without the clock check, so DoneStopped can close a run whose time has already run out.
    private void AddDone(TPartial answer, bool ok, string caption) {
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
    public StepBudgetExceededException(object recorder) : base("The solver ran past the time limit for recording steps.") {
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
