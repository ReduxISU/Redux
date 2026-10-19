
using System.Reflection;
using System.Text.Json;
using API.Interfaces;

namespace Redux.Library;

public class ReduxClient {
    private readonly Dictionary<string, Type> _solvers;
    private readonly Dictionary<string, Type> _verifiers;
    private readonly Dictionary<string, Type> _problems;
    private readonly Dictionary<string, Type> _reductions;
    private readonly Dictionary<string, Type> _visualizations;

    public ReduxClient() {
        var types = typeof(ReduxClient).Assembly.GetTypes();

        _solvers = Discover<ISolver>(types);
        _verifiers = Discover<IVerifier>(types);
        _problems = Discover<IProblem>(types);
        _reductions = Discover<IReduction>(types);
        _visualizations = Discover<IVisualization>(types);
    }

    private static Dictionary<string, Type> Discover<T>(Type[] types) {
        return types
            .Where(t => t.IsClass && !t.IsAbstract &&
                        !t.ContainsGenericParameters &&
                        typeof(T).IsAssignableFrom(t))
            .ToDictionary(
                t => t.Name,
                t => t,
                StringComparer.OrdinalIgnoreCase
            );
    }

    public string[] GetProblems() =>
        _problems.Keys.OrderBy(x => x).ToArray();

    public string[] GetSolvers() =>
        _solvers.Keys.OrderBy(x => x).ToArray();

    public string[] GetVerifiers() =>
        _verifiers.Keys.OrderBy(x => x).ToArray();

    public string[] GetReductions() =>
        _reductions.Keys.OrderBy(x => x).ToArray();

    public string[] GetVisualizations() =>
        _visualizations.Keys.OrderBy(x => x).ToArray();

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


    public string Reduce(string reductionName, string instance) {
        if (!_reductions.TryGetValue(reductionName, out var type))
            throw new ArgumentException(
                $"Unknown reduction: {reductionName}");

        var constructor = type.GetConstructor(
            new[] { typeof(string) });

        if (constructor == null)
            throw new NotSupportedException(
                $"Reduction {reductionName} does not support a string constructor.");

        var reduction = (IReduction)constructor.Invoke(
            new object[] { instance });

        IProblem result = reduction.reduce();

        return result.instance;
    }


    public string Visualize(
        string visualizationName,
        string instance) {

        if (!_visualizations.TryGetValue(visualizationName, out var type))
            throw new ArgumentException(
                $"Unknown visualization: {visualizationName}");

        var visualization =
            (IVisualization)Activator.CreateInstance(type)!;

        var result = visualization.visualize(instance);

        return JsonSerializer.Serialize(
            result,
            result.GetType()
        );
    }

    public string VisualizeSolved(
        string visualizationName,
        string instance,
        string solution) {

        if (!_visualizations.TryGetValue(visualizationName, out var type))
            throw new ArgumentException(
                $"Unknown visualization: {visualizationName}");

        var visualization =
            (IVisualization)Activator.CreateInstance(type)!;

        var result = visualization.SolvedVisualization(
            instance,
            solution
        );

        return JsonSerializer.Serialize(
            result,
            result.GetType()
        );
    }

    public string[] VisualizeSteps(
        string visualizationName,
        string instance,
        List<object> steps) {

        if (!_visualizations.TryGetValue(
            visualizationName, out var type))
            throw new ArgumentException(
                $"Unknown visualization: {visualizationName}");

        var visualization =
            (IVisualization)Activator.CreateInstance(type)!;

        var results = visualization.StepsVisualization(
            instance, steps);

        return results.Select(result =>
            JsonSerializer.Serialize(
                result,
                result.GetType()
            )
        ).ToArray();
    }

}
