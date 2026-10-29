using System.Text.Json;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Solvers;

class ConstraintSatisfactionBruteForce : ISolver<CONSTRAINTSATISFACTION> {
    public string solverName { get; } = "Constraint Satisfaction Brute Force";
    public string solverDefinition { get; } = "Enumerates the Cartesian product of the variable domains and returns the first assignment satisfying every constraint. Empty domains imply no solution.";
    public string source { get; } = "Exhaustive enumeration of finite-domain assignments.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = Array.Empty<string>();
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public MathematicalFunction timeComplexity { get; } = new("d^n * (n + m + T*a + 1)", ", n = variable count, d = max(2, largest domain size), m = constraint count, T = total allowed tuple count, a = maximum constraint arity; unit-cost string operations");
    public bool timerHasExpired { get; set; }

    public string solve(CONSTRAINTSATISFACTION problem) {
        if (timerHasExpired) return "timeout";
        var variables = problem.domains.Keys.ToArray();
        if (problem.domains.Values.Any(domain => domain.Count == 0)) return "No solution exists";

        // Mixed-radix counting avoids recursion and integer overflow from computing d^n.
        var indices = new int[variables.Length];
        var assignment = new Dictionary<string, string>(StringComparer.Ordinal);
        while (true) {
            if (timerHasExpired) return "timeout";
            for (int i = 0; i < variables.Length; i++)
                assignment[variables[i]] = problem.domains[variables[i]][indices[i]];
            if (problem.SatisfiesConstraints(assignment)) {
                if (timerHasExpired) return "timeout";
                return JsonSerializer.Serialize(assignment);
            }

            int position = variables.Length - 1;
            while (position >= 0 && ++indices[position] == problem.domains[variables[position]].Count) {
                indices[position] = 0;
                position--;
            }
            if (position < 0) return "No solution exists";
        }
    }
}
