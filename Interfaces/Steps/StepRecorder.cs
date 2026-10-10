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
/// </summary>
sealed class StepRecorder<TPartial> {
    public const int DefaultCap = 150;

    /// <summary>A shared recorder that is disabled: every method does nothing and no lambda is invoked.</summary>
    public static readonly StepRecorder<TPartial> Off = new(enabled: false, cap: 0);

    private readonly bool _enabled;
    private readonly int _cap;
    private readonly List<SolverStep<TPartial>> _steps = new();
    private int _total;
    private int _shownDecisions;
    // The most recent Accept reported past the cap, built when it was reported; added just before Done.
    private SolverStep<TPartial>? _lastAcceptPastCap;

    public StepRecorder(int cap = DefaultCap) : this(enabled: true, cap) { }

    private StepRecorder(bool enabled, int cap) {
        _enabled = enabled;
        _cap = cap;
    }

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
        if (!_enabled) return;
        _total++;
        if (_shownDecisions < _cap) {
            _shownDecisions++;
            _steps.Add(new SolverStep<TPartial>(partial(), kind, focus, caption()));
        } else if (kind == StepEvent.Accept) {
            _lastAcceptPastCap = new SolverStep<TPartial>(partial(), kind, focus, caption());
        }
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
        if (hidden > 0) caption += $" ({hidden} not shown)";
        _steps.Add(new SolverStep<TPartial>(answer, StepEvent.Done, Array.Empty<string>(), caption) { Ok = ok });
    }
}
