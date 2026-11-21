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
    /// <summary>The solver's friendly name (e.g. <c>Shor's Algorithm</c>), for <c>message</c>.</summary>
    public string SolverName { get; }
    /// <summary>The friendly name of the problem the solver was solving.</summary>
    public string ProblemName { get; }

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

    /// <summary>The response body, in the error contract proposed in #933 (one shape for every error): a stable
    /// <c>error</c> code, a plain-sentence <c>message</c>, the technical <c>detail</c>, context (<c>solver</c>,
    /// <c>problem</c>), a <c>hint</c> saying what to do next, and the valid <c>options</c> (the problem's other solvers,
    /// by class name, the key the endpoints take). The fields from before #933 keep their names and values.</summary>
    public object Body() {
        var others = OtherSolvers(Solver);
        var local = others.Where(o => !o.quantum).ToList();
        string hint = local.Count > 0
            ? $"{string.Join(" or ", local.Select(o => $"{o.className} ({o.name})"))} "
              + $"{(local.Count == 1 ? "runs" : "run")} without the quantum service; or try again later."
            : "Try again later, or check that the quantum service is running and configured (see Tools/QUANTUM_API_README.md).";
        return new {
            error = "solver_unavailable",
            message = $"{SolverName} couldn't get an answer from the quantum service, so {ProblemName} wasn't solved.",
            solver = Solver,
            problem = ProblemName,
            detail = Message,
            hint,
            options = others.Select(o => o.className).ToArray(),
        };
    }

    /// <summary>The other solvers of the problem <paramref name="solver"/> solves, from the same reflected catalog the
    /// navigation endpoints list (Navigation/Problem_SolversRefactor, Navigation/Batch/allSolvers), sorted by class name,
    /// each with its friendly name and whether it is a quantum solver (and so needs the quantum service too).</summary>
    internal static List<(string className, string name, bool quantum)> OtherSolvers(string solver) {
        string? problem = SolverNavigationData.Entries
            .FirstOrDefault(e => string.Equals(e.className, solver, StringComparison.OrdinalIgnoreCase))?.problemName;
        if (problem == null) return new();
        return SolverNavigationData.FindWithoutExtension(problem, null)
            .Where(c => !string.Equals(c, solver, StringComparison.OrdinalIgnoreCase))
            .Select(c => {
                string name = c;
                bool quantum = false;
                try {
                    if (ProblemProvider.Solvers.TryGetValue(c.ToLower(), out var type)
                        && Activator.CreateInstance(type) is ISolver s) {
                        name = s.solverName;
                        quantum = s.solverType == SolverType.Quantum;
                    }
                } catch {
                    // A solver that can't be default-constructed is still listed, by class name.
                }
                return (c, name, quantum);
            })
            .ToList();
    }
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
