namespace API.Interfaces;

/// <summary>
/// Thrown when a solver cannot produce an answer because something outside Redux that it depends on failed: for the
/// quantum solvers, the quantum service is not configured, cannot be reached, times out, or sends a response with no
/// answer in it. A solver throws this instead of returning a made-up answer such as <c>{"error": "..."}</c>, which the
/// caller would otherwise take for a certificate and hand to a verifier, a visualization or a reduction (#625).
/// <see cref="SolverUnavailableFilter"/> turns it into HTTP 503 with a structured body on every endpoint.
/// </summary>
public partial class SolverUnavailableException : Exception {
    /// <summary>The solver's class name, the key the endpoints take (e.g. <c>ShorsQuantumSolver</c>).</summary>
    public string Solver { get; }
    /// <summary>The solver's friendly name (e.g. <c>Shor's Algorithm</c>), for <c>message</c>.</summary>
    public string SolverName { get; }
    /// <summary>The friendly name of the problem the solver was solving.</summary>
    public string ProblemName { get; }
    /// <summary>
    /// Creates an exception when a solver cannot produce an answer.
    /// </summary>
    /// <param name="solver">The solver's class name.</param>
    /// <param name="solverName">The solver's display name.</param>
    /// <param name="problemName">The problem being solved.</param>
    /// <param name="detail">Details explaining why the solver is unavailable.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    public SolverUnavailableException(string solver, string solverName, string problemName, string detail, Exception? innerException = null)
        : base(detail, innerException) {
        Solver = solver;
        SolverName = solverName;
        ProblemName = problemName;
    }

    /// <summary>The <see cref="SolverUnavailableException"/> inside <paramref name="ex"/>, if any, looking through the
    /// wrappers reflection and <c>Task.Result</c> add.</summary>
    public static SolverUnavailableException? Find(Exception? ex) {
        while (ex != null) {
            if (ex is SolverUnavailableException found) return found;
            ex = ex switch {
                System.Reflection.TargetInvocationException tie => tie.InnerException,
                AggregateException { InnerExceptions.Count: 1 } agg => agg.InnerException,
                _ => null,
            };
        }
        return null;
    }
}