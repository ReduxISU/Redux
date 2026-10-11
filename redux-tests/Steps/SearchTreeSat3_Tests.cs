using System.Text.Json;
using System.Text.Json.Nodes;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// 3SAT Backtracking's steps as the Search Tree picture reads them. Endpoints/Snapshots/searchtree_sat3_steps.tsv holds
// (solver, instance, the run's steps as compact JSON) captured BEFORE the solver reported its search tree: every 3SAT row of
// sat_solver_answers.tsv, the mockup's examples, and five seeded random formulas (one past the 150-step cap, two
// unsatisfiable). With each step's "tree" removed, the steps must be exactly those, so no other picture changes.
public class SearchTreeSat3_Tests {
    public static TheoryData<string, string, string> Snapshot {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var line in File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", "searchtree_sat3_steps.tsv")).Where(l => l.Length > 0)) {
                var parts = line.Split('\t');
                data.Add(parts[0], parts[1], parts[2]);
            }
            return data;
        }
    }

    private static ISolver Create(string name) => (ISolver)Activator.CreateInstance(ProblemProvider.Solvers[name])!;

    private static JsonArray StepsJson(SolveRun run) =>
        JsonNode.Parse(JsonSerializer.Serialize(run.Steps, ProblemProvider.VisualizeJsonOptions()))!.AsArray();

    [Theory]
    [MemberData(nameof(Snapshot))]
    public void Steps_WithoutTheirTree_AreWhatTheyWereBeforeTheSearchTree(string solver, string instance, string expected) {
        var steps = StepsJson(Create(solver).Run(instance, withSteps: true));
        foreach (var step in steps) step!.AsObject().Remove("tree");
        Assert.Equal(expected, steps.ToJsonString());
    }
}
