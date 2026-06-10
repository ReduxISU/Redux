
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
        T problemInstance = (T)Activator.CreateInstance(typeof(T), problem);
        if (problemInstance == null)
            throw new ArgumentException($"Could not create problem instance for {problem}.");

        return solve(problemInstance);
    }

    string solve(T problem);

    List<Object> ISolver.GetSteps(string instance)
    {
        return GetSteps((T)Activator.CreateInstance(typeof(T), instance));
    }

    List<Object> GetSteps(T problem)
    {
        return new List<Object>();
    }
}
