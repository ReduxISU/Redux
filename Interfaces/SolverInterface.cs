
namespace API.Interfaces;

interface ISolver {
    string solverName { get; }
    string solverDefinition { get; }
    string source { get; }
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
}

interface ISolver<T> : ISolver where T : IProblem {
    string ISolver.solve(string problem) {
        // Should there be some sort of contraint that assures there is a constructor
        // that matches the signature of a single `string` argument?
        // Perhaps a static `FromInstance(string instance)` method for `IProblem` will work.
        object? instance = Activator.CreateInstance(typeof(T), problem);
        if (instance == null)
            throw new ArgumentException($"Could not create problem instance for {problem}.");

        return solve((T)instance);
    }

    string solve(T problem);

    List<Object> ISolver.GetSteps(string instance) {
        object? problemInstance = Activator.CreateInstance(typeof(T), instance);
        if (problemInstance == null)
            throw new ArgumentException($"Could not create problem instance for {instance}.");

        return GetSteps((T)problemInstance);
    }

    List<Object> GetSteps(T problem) {
        return new List<Object>();
    }
}
