
using API.Interfaces.Steps;

namespace API.Interfaces;

interface ISolver {
    string solverName { get; }
    string solverDefinition { get; }
    string source { get; }
    // Repo-relative path of the file declaring this class; see SourceFile.
    string sourceFile { get; }
    string[] contributors { get; }

    bool timerHasExpired { get; set; }

    /// <summary>
    /// The algorithmic style this solver uses. Declared, not derived — see
    /// <see cref="API.Interfaces.SolverType"/>. Defaults to
    /// <see cref="API.Interfaces.SolverType.Unclassified"/> until a concrete solver
    /// overrides it.
    /// </summary>
    SolverType solverType { get => SolverType.Unclassified; }

    /// <summary>
    /// Free-text runtime-complexity commentary (e.g. "O(n * W)"). Pre-existing ad-hoc
    /// field promoted to the interface under its original name — see the header of
    /// <see cref="API.Interfaces.ReductionCost"/> for why this differs from that type's
    /// naming choice. Defaults to empty string; only populate with a confidently-known
    /// Big-O string, never a guess.
    /// </summary>
    string complexity { get => ""; }

    /// <summary>
    /// Coarse WORST-CASE growth class of this solver. Declared, not derived — see
    /// <see cref="API.Interfaces.SolverComplexityBucket"/>. Defaults to
    /// <see cref="API.Interfaces.SolverComplexityBucket.Unclassified"/> until a concrete
    /// solver overrides it.
    /// </summary>
    SolverComplexityBucket complexityBucket { get => SolverComplexityBucket.Unclassified; }

    /// <summary>
    /// Called when the run time timer for this solver has run out. The solver is
    /// expected to check the "timerHasExpired" periodically and abandon the solution
    /// if the flag is found to be true.
    /// </summary>
    public void TimerExpired() {
        timerHasExpired = true;
    }
    public void ResetTimer() {
        timerHasExpired = false;
    }
    string solve(string problem);

    List<Object> GetSteps(string instance) {
        return new List<Object>();
    }

    /// <summary>
    /// Solves an instance, optionally recording steps, in one call. The default is the legacy behaviour:
    /// <see cref="GetSteps(string)"/> and then <see cref="solve(string)"/>, untyped steps. That is one run only while
    /// GetSteps does no solving of its own, so a solver that records steps implements <see cref="ISolver{T, TPartial}"/>
    /// instead, which overrides this to run once with typed steps (SolveOnce_Tests checks both).
    /// </summary>
    /// <param name="instance">The instance to solve.</param>
    /// <param name="withSteps">Whether to record steps.</param>
    /// <param name="timeLimit">
    /// Optional time budget for a run that records steps; null means no limit. A typed solver that runs past it is
    /// stopped at its next recorded step and returns a run with <see cref="SolveRun.TimedOut"/> set. It has no effect
    /// when <paramref name="withSteps"/> is false, and none on this legacy default: an uninstrumented solver never
    /// calls a recorder, so it cannot be stopped this way (the general solve timeout is issue #523).
    /// </param>
    SolveRun Run(string instance, bool withSteps, TimeSpan? timeLimit = null) {
        var steps = withSteps ? GetSteps(instance).ToList() : new List<Object>();
        return new SolveRun(solve(instance), steps, null);
    }
}

/// <summary>The result of <see cref="ISolver.Run"/>: the answer, the recorded steps, and the shape of their partial answers.</summary>
/// <param name="Answer">The same string <c>solve</c> returns.</param>
/// <param name="Steps">The steps, as objects. <see cref="SolverStep{TPartial}"/> for typed solvers, solver-specific objects for legacy ones.</param>
/// <param name="StepShape">The <c>TPartial</c> of the typed steps, or null for legacy untyped steps.</param>
sealed record SolveRun(string Answer, IReadOnlyList<object> Steps, Type? StepShape) {
    /// <summary>
    /// The <see cref="Answer"/> of a run stopped by its time limit. It is deliberately not a certificate, and not
    /// "{}" (which means that no answer exists): the solver had not finished, so there is no answer to show or verify.
    /// Callers check <see cref="TimedOut"/>, not this text.
    /// </summary>
    public const string TimedOutAnswer = "Stopped: the solver did not finish within the time limit.";

    /// <summary>
    /// True when the solver was stopped by the time limit given to <see cref="ISolver.Run"/>. Then
    /// <see cref="Answer"/> is <see cref="TimedOutAnswer"/>, and <see cref="Steps"/> holds the steps kept so far
    /// followed by a Done step with ok = false that says the solver had not finished.
    /// </summary>
    public bool TimedOut { get; init; }

    /// <summary>
    /// Runs a typed solve: with recording off when <paramref name="withSteps"/> is false (no clock checks), else with
    /// a recorder that has <paramref name="timeLimit"/>. A run that exceeds the limit is returned as stopped (see
    /// <see cref="TimedOut"/>) instead of throwing.
    /// </summary>
    internal static SolveRun Record<TPartial>(bool withSteps, TimeSpan? timeLimit, Func<StepRecorder<TPartial>, string> solve) {
        if (!withSteps)
            return new SolveRun(solve(StepRecorder<TPartial>.Off), new List<object>(), typeof(TPartial));
        var rec = new StepRecorder<TPartial>(timeLimit: timeLimit);
        try {
            string answer = solve(rec);
            return new SolveRun(answer, rec.Steps.Cast<object>().ToList(), typeof(TPartial));
        } catch (StepBudgetExceededException ex) when (ex.Recorder == rec) {
            rec.DoneStopped();
            return new SolveRun(TimedOutAnswer, rec.Steps.Cast<object>().ToList(), typeof(TPartial)) { TimedOut = true };
        }
    }

    /// <summary>
    /// The steps to hand a visualization that draws <paramref name="visualizationShape"/>. If the shapes differ
    /// (including one typed and one legacy) the steps cannot be drawn, so none are given: the response then
    /// shows the initial and solved pictures instead of failing.
    /// </summary>
    public List<object> StepsFor(Type? visualizationShape) =>
        StepShape == visualizationShape ? Steps.ToList() : new List<object>();
}

interface ISolver<T> : ISolver where T : IProblem {
    string ISolver.solve(string problem) {
        // Should there be some sort of contraint that assures there is a constructor
        // that matches the signature of a single `string` argument?
        // Perhaps a static `FromInstance(string instance)` method for `IProblem` will work.
        // Any constructor failure is reported as a ProblemParseException (-> HTTP 400); only the
        // construction is guarded, not the solve below.
        T instance = ParseGuard.CreateProblem<T>(problem);

        return solve(instance);
    }

    string solve(T problem);

    List<Object> ISolver.GetSteps(string instance) {
        T problemInstance = ParseGuard.CreateProblem<T>(instance);

        return GetSteps(problemInstance);
    }

    List<Object> GetSteps(T problem) {
        return new List<Object>();
    }
}

/// <summary>
/// A solver whose steps are typed by the shape of its answer. Implement <see cref="Solve"/>; the plain
/// <c>solve</c> runs it with recording off.
/// </summary>
interface ISolver<T, TPartial> : ISolver<T> where T : IProblem {
    string Solve(T problem, StepRecorder<TPartial> rec);

    string ISolver<T>.solve(T problem) => Solve(problem, StepRecorder<TPartial>.Off);

    SolveRun ISolver.Run(string instance, bool withSteps, TimeSpan? timeLimit) {
        T parsed = ParseGuard.CreateProblem<T>(instance);
        return SolveRun.Record<TPartial>(withSteps, timeLimit, rec => Solve(parsed, rec));
    }

    List<Object> ISolver<T>.GetSteps(T problem) {
        var rec = new StepRecorder<TPartial>();
        Solve(problem, rec);
        return rec.Steps.Cast<object>().ToList();
    }
}
