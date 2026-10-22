using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API.Interfaces;

/// <summary>
/// Thrown when a solver cannot produce an answer because something outside Redux that it depends on failed: for the
/// quantum solvers, the quantum service is not configured, cannot be reached, times out, or sends a response with no
/// answer in it. A solver throws this instead of returning a made-up answer such as <c>{"error": "..."}</c>, which the
/// caller would otherwise take for a certificate and hand to a verifier, a visualization or a reduction (#625).
/// <see cref="SolverUnavailableFilter"/> turns it into HTTP 503 with a structured body on every endpoint.
/// </summary>
internal class SolverUnavailableException : Exception {
    /// <summary>The solver's class name, the key the endpoints take (e.g. <c>ShorsQuantumSolver</c>).</summary>
    public string Solver { get; }
    /// <summary>The friendly name of the problem the solver was solving.</summary>
    public string ProblemName { get; }

    public SolverUnavailableException(string solver, string problemName, string detail, Exception? innerException = null)
        : base(detail, innerException) {
        Solver = solver;
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

    /// <summary>The response body: the same shape as the parse errors (an <c>error</c> code, what was asked, a
    /// <c>detail</c>), so clients can read every structured error the same way.</summary>
    public object Body() => new {
        error = "solver_unavailable",
        solver = Solver,
        problem = ProblemName,
        detail = Message,
    };
}

/// <summary>
/// Registered for every controller (Program.cs), so any endpoint that runs a solver (solve, visualize, and any later
/// endpoint) answers a <see cref="SolverUnavailableException"/> with HTTP 503 Service Unavailable and
/// <see cref="SolverUnavailableException.Body"/>, never with a 200 carrying an error as the answer, and never with a 500.
/// 503, not 502: Redux is not a gateway for the quantum service; the chosen solver is what is unavailable for now, and
/// another solver (or the same one later) can still answer.
/// </summary>
internal sealed class SolverUnavailableFilter : IExceptionFilter {
    public void OnException(ExceptionContext context) {
        if (SolverUnavailableException.Find(context.Exception) is not { } ex) return;
        context.Result = new ObjectResult(ex.Body()) { StatusCode = StatusCodes.Status503ServiceUnavailable };
        context.ExceptionHandled = true;
    }
}
