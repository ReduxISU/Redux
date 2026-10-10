
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
