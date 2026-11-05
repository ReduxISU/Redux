// Contributors: Prashant Pant and Bektur Akkabakov. Tests for the LLM implementation.
using System.Globalization;
using API.Interfaces;
using API.Problems.NPComplete.NPC_KERNEL;
using API.Problems.NPComplete.NPC_KERNEL.Solvers;
using API.Problems.NPComplete.NPC_KERNEL.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class KernelSolver_Tests {
    private static ISolver<KERNEL> CreateSolver(string name) => name switch {
        "BruteForce" => new KernelBruteForce(),
        "Backtracking" => new KernelBacktracking(),
        "DPLL" => new KernelDPLL(),
        _ => throw new ArgumentException("Unknown solver", nameof(name))
    };

    [Theory]
    [InlineData("BruteForce")]
    [InlineData("Backtracking")]
    [InlineData("DPLL")]
    public void Solves_known_graphs_and_can_be_reused(string name) {
        var solver = CreateSolver(name);
        (string Instance, bool Exists)[] cases = {
            ("({},{})", true),
            ("({7},{})", true),
            ("({2,9,42},{})", true),
            ("({2,9},{(2,9)})", true),
            ("({0,1},{(0,1),(1,0)})", true),
            ("({0,1,2,3},{(0,1),(1,2),(2,3),(3,0)})", true),
            ("({0,1,2},{(0,1),(1,2),(2,0)})", false),
            ("({0},{(0,0)})", false),
            ("({0,1},{(0,0),(0,1)})", true),
            ("({0,1,2,9},{(0,1),(1,2),(2,0)})", false),
            ("({0,1,2,9},{(0,1),(1,2),(2,0),(0,9),(1,9),(2,9)})", true),
            ("({0,1},{(0,1),(0,1)})", true),
            ("({0,2147483647},{(0,2147483647)})", true)
        };
        foreach (var test in cases) {
            var problem = new KERNEL(test.Instance);
            int[] verticesBefore = problem.vertices.ToArray();
            var edgesBefore = problem.edges.ToArray();
            string answer = solver.solve(problem);
            if (test.Exists) {
                Assert.True(KernelTestCases.IsKernel(problem, KernelTestCases.ParseAnswer(answer)),
                    $"{name} returned {answer} for {test.Instance}");
                Assert.True(new KernelVerifier().verify(problem, answer));
            } else {
                Assert.Equal("{}", answer);
                Assert.False(new KernelVerifier().verify(problem, answer));
            }
            Assert.Equal(verticesBefore, problem.vertices.ToArray());
            Assert.Equal(edgesBefore, problem.edges.ToArray());
        }
    }

    [Theory]
    [InlineData("BruteForce")]
    [InlineData("Backtracking")]
    [InlineData("DPLL")]
    public void Matches_independent_oracle_on_all_graphs_through_three_vertices(string name) {
        var solver = CreateSolver(name);
        foreach (var problem in KernelTestCases.SmallGraphs()) {
            bool exists = KernelTestCases.Subsets(problem).Any(s => KernelTestCases.IsKernel(problem, s));
            string answer = solver.solve(problem);
            if (exists) Assert.True(KernelTestCases.IsKernel(problem, KernelTestCases.ParseAnswer(answer)),
                $"{name} returned {answer} for {problem.instance}");
            else Assert.Equal("{}", answer);
        }
    }

    [Theory]
    [InlineData("BruteForce")]
    [InlineData("Backtracking")]
    [InlineData("DPLL")]
    public void Expired_timer_returns_standard_timeout_and_reset_allows_reuse(string name) {
        var solver = CreateSolver(name);
        foreach (string instance in new[] { "({},{})", "({0,1},{(0,1)})", "({0},{(0,0)})" }) {
            solver.timerHasExpired = true;
            Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(new KERNEL(instance)));
        }
        solver.timerHasExpired = false;
        var problem = new KERNEL("({0,1},{(0,1)})");
        Assert.True(KernelTestCases.IsKernel(problem, KernelTestCases.ParseAnswer(solver.solve(problem))));
    }

    [Theory]
    [InlineData("BruteForce")]
    [InlineData("Backtracking")]
    [InlineData("DPLL")]
    public void Supports_more_than_64_vertices_on_an_easy_instance(string name) {
        // Unique kernel {0}; found early even by brute force. Avoids a timing-dependent test.
        string vertices = string.Join(",", Enumerable.Range(0, 70));
        string edges = string.Join(",", Enumerable.Range(1, 69).Select(v => $"({v},0)"));
        var problem = new KERNEL($"({{{vertices}}},{{{edges}}})");
        Assert.Equal("{0}", CreateSolver(name).solve(problem));
    }
}

// Independent test oracle: pairwise independence and per-vertex absorption,
// rather than the production verifiers' accumulated absorption sets.
internal static class KernelTestCases {
    internal static bool IsKernel(KERNEL problem, HashSet<int> selected) =>
        selected.IsSubsetOf(problem.vertices)
        && selected.All(u => selected.All(v => !problem.edges.Contains((u, v))))
        && problem.vertices.Where(u => !selected.Contains(u))
            .All(u => selected.Any(v => problem.edges.Contains((u, v))));

    internal static HashSet<int> ParseAnswer(string answer) {
        Assert.StartsWith("{", answer);
        Assert.EndsWith("}", answer);
        int[] entries = answer[1..^1].Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(entries.Length, entries.Distinct().Count());
        return entries.ToHashSet();
    }

    internal static IEnumerable<HashSet<int>> Subsets(KERNEL problem) {
        int[] vertices = problem.vertices.ToArray();
        for (int mask = 0; mask < (1 << vertices.Length); mask++)
            yield return vertices.Where((_, i) => (mask & (1 << i)) != 0).ToHashSet();
    }

    internal static IEnumerable<KERNEL> SmallGraphs() {
        for (int n = 0; n <= 3; n++) {
            int[] vertices = Enumerable.Range(0, n).Select(i => 7 * i + 2).ToArray();
            for (int mask = 0; mask < (1 << (n * n)); mask++) {
                var edges = new List<string>();
                for (int u = 0; u < n; u++)
                    for (int v = 0; v < n; v++)
                        if ((mask & (1 << (u * n + v))) != 0)
                            edges.Add($"({vertices[u]},{vertices[v]})");
                yield return new KERNEL($"({{{string.Join(",", vertices)}}},{{{string.Join(",", edges)}}})");
            }
        }
    }
}
