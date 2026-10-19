
using System.Reflection;
using API.Interfaces;

namespace Redux.Library;

public class ReduxClient {
    private readonly Dictionary<string, Type> _solvers;
    private readonly Dictionary<string, Type> _verifiers;
    private readonly Dictionary<string, Type> _problems;

    public ReduxClient() {
        var types = typeof(ReduxClient).Assembly.GetTypes();

        _solvers = Discover<ISolver>(types);
        _verifiers = Discover<IVerifier>(types);
        _problems = Discover<IProblem>(types);
    }

    private static Dictionary<string, Type> Discover<T>(Type[] types) {
        return types
            .Where(t => t.IsClass && !t.IsAbstract &&
                        typeof(T).IsAssignableFrom(t))
            .ToDictionary(
                t => t.Name,
                t => t,
                StringComparer.OrdinalIgnoreCase
            );
    }

    public string[] GetProblems() {
        return _problems.Keys.OrderBy(x => x).ToArray();
    }

    public string[] GetSolvers() {
        return _solvers.Keys.OrderBy(x => x).ToArray();
    }

    public string[] GetVerifiers() {
        return _verifiers.Keys.OrderBy(x => x).ToArray();
    }

    public string Solve(string solverName, string instance) {
        if (!_solvers.TryGetValue(solverName, out var type))
            throw new ArgumentException($"Unknown solver: {solverName}");

        var solver = (ISolver)Activator.CreateInstance(type)!;

        return solver.solve(instance);
    }

    public bool Verify(
        string verifierName,
        string instance,
        string certificate) {
        if (!_verifiers.TryGetValue(verifierName, out var type))
            throw new ArgumentException($"Unknown verifier: {verifierName}");

        var verifier = (IVerifier)Activator.CreateInstance(type)!;

        return verifier.verify(instance, certificate);
    }
}
