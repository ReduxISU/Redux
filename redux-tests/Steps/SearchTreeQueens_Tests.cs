using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// N-Queens Backtracking's steps as the Search Tree picture reads them. Endpoints/Snapshots/searchtree_queens_steps.tsv holds
// (solver, instance as a JSON string, step count, then a hash of each step: the first 16 hex digits of the SHA-256 of its
// compact JSON) captured BEFORE the solver reported its search tree, on every N-Queens Backtracking row of
// board_solver_answers.tsv. With each step's "tree" removed, the steps must hash the same, so no other picture changes.
public class SearchTreeQueens_Tests {
    internal static IEnumerable<(string Solver, string Instance, int Count, string[] Hashes)> Rows() =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", "searchtree_queens_steps.tsv")).Where(l => l.Length > 0).Select(l => {
            var parts = l.Split('\t');
            return (parts[0], JsonSerializer.Deserialize<string>(parts[1])!, int.Parse(parts[2]), parts[3].Split(' '));
        });

    public static TheoryData<string, string> Snapshot {
        get {
            var data = new TheoryData<string, string>();
            foreach (var (solver, instance, _, _) in Rows()) data.Add(solver, instance);
            return data;
        }
    }

    private static ISolver Create(string name) => (ISolver)Activator.CreateInstance(ProblemProvider.Solvers[name])!;

    private static string Hash(JsonNode step) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(step.ToJsonString())))[..16].ToLowerInvariant();

    [Theory]
    [MemberData(nameof(Snapshot))]
    public void Steps_WithoutTheirTree_AreWhatTheyWereBeforeTheSearchTree(string solver, string instance) {
        var (_, _, count, hashes) = Rows().Single(r => r.Solver == solver && r.Instance == instance);
        var steps = JsonNode.Parse(JsonSerializer.Serialize(Create(solver).Run(instance, withSteps: true).Steps, ProblemProvider.VisualizeJsonOptions()))!.AsArray();
        foreach (var step in steps) step!.AsObject().Remove("tree");
        Assert.Equal(count, steps.Count);
        for (int i = 0; i < count; i++)
            if (Hash(steps[i]!) != hashes[i]) Assert.Fail($"Step {i} changed. It is now {steps[i]!.ToJsonString()}");
    }
}
