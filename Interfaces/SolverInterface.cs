
namespace API.Interfaces;

interface ISolver {
    string solverName{get;}
    string solverDefinition{get;}
    string source {get;}
    string[] contributors { get; }

    bool timerHasExpired { get; set; }

    /// <summary>
    /// Called when the run time timer for this solver has run out. The solver is
    /// expected to check the "timerHasExpired" periodically and abandon the solution
    /// if the flag is found to be true.
    /// </summary>
    public void TimerExpired()
    {
        timerHasExpired = true;
    }
    public void ResetTimer()
    {
        timerHasExpired = false;
    }
    string solve(string problem);

    List<Object> GetSteps(string instance)
    {
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

    List<Object> ISolver.GetSteps(string instance)
    {
        object? problemInstance = Activator.CreateInstance(typeof(T), instance);
        if (problemInstance == null)
            throw new ArgumentException($"Could not create problem instance for {instance}.");

        return GetSteps((T)problemInstance);
    }

    List<Object> GetSteps(T problem)
    {
        return new List<Object>();
    }
}
