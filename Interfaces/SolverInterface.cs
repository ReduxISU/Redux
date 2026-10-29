
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
    /// Worst-case TIME growth, as an evaluable Big-O function of instance-size
    /// variables (e.g. <c>new("n * W", ", n = |items|, W = capacity")</c> displays as
    /// "O(n * W), n = |items|, W = capacity" and can also be evaluated for a concrete
    /// n/W). Pre-existing ad-hoc field promoted to the interface under its original
    /// name — see the header of <see cref="API.Interfaces.ReductionCost"/> for why this
    /// differs from that type's naming choice. Defaults to <c>null</c> (undeclared);
    /// only populate with a confidently-known Big-O function, never a guess.
    /// </summary>
    MathematicalFunction? timeComplexity { get => null; }

    /// <summary>
    /// Worst-case SPACE growth, as an evaluable Big-O function of instance-size
    /// variables — same shape and same "declare only when confidently known" rule as
    /// <see cref="timeComplexity"/>, just for auxiliary memory instead of running time.
    /// Defaults to <c>null</c> (undeclared).
    /// </summary>
    MathematicalFunction? spaceComplexity { get => null; }

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
    SolveRun Run(string instance, bool withSteps) {
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
    /// True when the solver builds its answer piece by piece (it implements <see cref="IIncrementalSolver"/>), so every
    /// step's partial is what it has committed so far and the focus names the piece under test. A picture then draws the
    /// partial as the answer so far in every event, and only the focus as tried or failed. False (the default) means each
    /// Try and Reject judges a whole candidate, which a picture highlights as a candidate and never draws as the answer.
    /// Set by <see cref="ISolver{T, TPartial}"/>'s <c>Run</c>; a run built by hand says it itself.
    /// </summary>
    public bool Incremental { get; init; }

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

    SolveRun ISolver.Run(string instance, bool withSteps) {
        T parsed = ParseGuard.CreateProblem<T>(instance);
        var rec = withSteps ? new StepRecorder<TPartial>() : StepRecorder<TPartial>.Off;
        string answer = Solve(parsed, rec);
        return new SolveRun(answer, rec.Steps.Cast<object>().ToList(), typeof(TPartial)) { Incremental = this is IIncrementalSolver };
    }

    List<Object> ISolver<T>.GetSteps(T problem) {
        var rec = new StepRecorder<TPartial>();
        Solve(problem, rec);
        return rec.Steps.Cast<object>().ToList();
    }
}

/// <summary>
/// Declares that a typed solver builds its answer piece by piece (a greedy, a backtracking search, a branch and bound),
/// rather than judging whole candidates one after another (a brute force). It is declared, never inferred: the frames
/// builders read it from <see cref="SolveRun.Incremental"/> to decide how a step's partial is drawn.
/// <para>
/// The contract for the solver's steps:
/// </para>
/// <list type="bullet">
/// <item>Accept and Backtrack: the partial is what the solver has committed after the move.</item>
/// <item>Try and Reject: the partial is what the solver has committed, and may also hold the piece under test (the next
/// node of a path, the value being tried for a variable).</item>
/// <item>The focus names the piece under test, and only that: never the whole partial. A step that judges everything
/// built so far (a closing check, out of budget) names the piece that failed, or nothing.</item>
/// </list>
/// <para>
/// A picture then draws the partial as the answer so far (Solution) in every event, the focus as
/// <c>ElementHighlight</c> in a Try and <c>Rejected</c> in a Reject. A solver without this marker gets the whole-candidate
/// reading: its Try and Reject partials are highlighted as a candidate, never drawn as the answer.
/// </para>
/// </summary>
interface IIncrementalSolver : ISolver { }
