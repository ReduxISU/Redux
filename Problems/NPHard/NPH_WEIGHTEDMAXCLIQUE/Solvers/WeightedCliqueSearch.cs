using System.Diagnostics;
using System.Globalization;
using API.Interfaces;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Solvers;

// Staged alongside the unregistered Phase 1 model. Adopt ISolver<WEIGHTEDMAXCLIQUE>
// when the real verifier and visualization make problem registration appropriate.
internal abstract class WeightedCliqueSolver {
    public abstract string solverName { get; }
    public abstract string solverDefinition { get; }
    public abstract string source { get; }
    public abstract string sourceFile { get; }
    public abstract string sourceLink { get; }
    public string[] contributors { get; } = { "Corbin Hay" };
    public abstract SolverType solverType { get; }
    public abstract MathematicalFunction timeComplexity { get; }
    public MathematicalFunction spaceComplexity { get; } = new("n^2", ", n = vertices; adjacency matrix and search storage");
    public abstract SolverComplexityBucket complexityBucket { get; }

    private volatile bool _timerHasExpired;
    public bool timerHasExpired { get => _timerHasExpired; set => _timerHasExpired = value; }
    public void TimerExpired() => timerHasExpired = true;
    public void ResetTimer() => timerHasExpired = false;

    public string solve(string input, TimeSpan? timeLimit = null, CancellationToken cancellationToken = default) =>
        solve(new WEIGHTEDMAXCLIQUE(input), timeLimit, cancellationToken);

    public string solve(WEIGHTEDMAXCLIQUE problem, TimeSpan? timeLimit = null, CancellationToken cancellationToken = default) {
        long started = timeLimit.HasValue ? Stopwatch.GetTimestamp() : 0;
        void Check() {
            cancellationToken.ThrowIfCancellationRequested();
            if (timerHasExpired || (timeLimit is { } limit && Stopwatch.GetElapsedTime(started) >= limit))
                throw new WeightedCliqueSearchStoppedException();
        }
        try {
            WeightedCliqueResult result = Optimize(problem, Check);
            string certificate = WeightedCliqueGraph.FormatCertificate(result);
            Check();
            return certificate;
        } catch (WeightedCliqueSearchStoppedException) {
            return SolveRun.TimedOutAnswer;
        }
    }

    // A stopped internal search throws; it never hands an incomplete incumbent to
    // a later verifier or benchmark as though optimization had finished.
    internal abstract WeightedCliqueResult Optimize(WEIGHTEDMAXCLIQUE problem, Action check);
}

internal sealed class WeightedCliqueSearchStoppedException : Exception;

internal sealed record WeightedCliqueResult(IReadOnlyList<string> Vertices, long Weight);

internal static class WeightedCliqueExhaustiveSearchLimit {
    internal const int MaxVertices = 20;
    internal static void ThrowIfExceeded(int count) {
        if (count > MaxVertices)
            throw new ExactSearchLimitExceededException("Weighted Maximum Clique", count, MaxVertices);
    }
}

internal sealed class WeightedCliqueGraph {
    internal string[] Names { get; }
    internal long[] Weights { get; }
    internal bool[][] Adjacent { get; }
    internal int[] Priority { get; }
    internal int Count => Names.Length;

    internal WeightedCliqueGraph(WEIGHTEDMAXCLIQUE problem, Action check) {
        check();
        Names = problem.nodes.Order(StringComparer.Ordinal).ToArray();
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        Weights = new long[Count];
        Adjacent = new bool[Count][];
        var degrees = new int[Count];
        for (int i = 0; i < Count; i++) {
            check();
            indices.Add(Names[i], i);
            Weights[i] = problem.weights[Names[i]];
            Adjacent[i] = new bool[Count];
        }
        foreach (var edge in problem.edges) {
            check();
            int a = indices[edge.Key], b = indices[edge.Value];
            Adjacent[a][b] = Adjacent[b][a] = true;
            degrees[a]++;
            degrees[b]++;
        }
        Priority = Enumerable.Range(0, Count).OrderByDescending(v => Weights[v])
            .ThenByDescending(v => degrees[v]).ThenBy(v => v).ToArray();
        check();
    }

    internal bool CanAdd(int vertex, IReadOnlyList<int> clique, Action check) {
        foreach (int member in clique) {
            check();
            if (!Adjacent[vertex][member]) return false;
        }
        return true;
    }

    internal bool IsClique(IReadOnlyList<int> vertices, Action check) {
        for (int i = 0; i < vertices.Count; i++) {
            for (int j = 0; j < i; j++) {
                check();
                if (!Adjacent[vertices[i]][vertices[j]]) return false;
            }
        }
        return true;
    }

    internal long Weight(IReadOnlyList<int> vertices, Action check) {
        long value = 0;
        foreach (int vertex in vertices) {
            check();
            value = checked(value + Weights[vertex]);
        }
        return value;
    }

    // Positive additions only: zero-weight graphs keep the empty clique.
    internal void GreedyExtend(List<int> clique, Action check) {
        foreach (int vertex in Priority) {
            check();
            if (Weights[vertex] > 0 && CanAdd(vertex, clique, check)) clique.Add(vertex);
        }
    }

    internal WeightedCliqueResult Result(IReadOnlyList<int> clique, long weight) =>
        new(Array.AsReadOnly(clique.Order().Select(v => Names[v]).ToArray()), weight);

    internal static string FormatCertificate(WeightedCliqueResult result) =>
        "({" + string.Join(",", result.Vertices.Order(StringComparer.Ordinal)) + "},"
        + result.Weight.ToString(CultureInfo.InvariantCulture) + ")";
}
