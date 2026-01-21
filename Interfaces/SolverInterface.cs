
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

    List<string> GetSteps(string instance)
    {
        return new List<string>();
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

        string result = "no solution found";
        Thread thread = new Thread(() => result = solve(problemInstance));

        // start the thread
        ResetTimer();
        thread.Start();

        // after 5 seconds w/out finishing, tell the thread it's time
        // is up and wait for it to finish up.
        // XXX make the solution time configurable
        if (thread.Join(new TimeSpan(0, 0, 5)) == false)
        {
            TimerExpired();
            thread.Join();
        }
        return result;
    }

    string solve(T problem);

    List<string> ISolver.GetSteps(string instance)
    {
        return GetSteps((T)Activator.CreateInstance(typeof(T), instance));
    }

    List<string> GetSteps(T problem)
    {
        return new List<string>();
    }
}
