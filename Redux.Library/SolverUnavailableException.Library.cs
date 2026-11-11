namespace API.Interfaces;

internal class SolverUnavailableException : Exception {
    public string Solver { get; }
    public string SolverName { get; }
    public string ProblemName { get; }

    public SolverUnavailableException(
        string solver,
        string solverName,
        string problemName,
        string detail,
        Exception? innerException = null)
        : base(detail, innerException) {
        Solver = solver;
        SolverName = solverName;
        ProblemName = problemName;
    }

    public static SolverUnavailableException? Find(Exception? ex) {
        while (ex != null) {
            if (ex is SolverUnavailableException found)
                return found;

            ex = ex switch {
                System.Reflection.TargetInvocationException tie => tie.InnerException,
                AggregateException { InnerExceptions.Count: 1 } agg => agg.InnerException,
                _ => null,
            };
        }

        return null;
    }
}