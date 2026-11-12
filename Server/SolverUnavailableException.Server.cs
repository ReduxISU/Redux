namespace API.Interfaces;

public partial class SolverUnavailableException {
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