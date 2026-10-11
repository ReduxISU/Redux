using System.Text.Json;
using System.Text.Json.Nodes;
using API.Interfaces;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Traveling Salesperson Branch and Bound's steps as the Search Tree picture reads them. Endpoints/Snapshots/searchtree_tsp_steps.tsv
// holds (solver, instance, the run's steps as compact JSON) captured BEFORE the solver reported its search tree: every branch
// and bound row of tour_solver_answers.tsv, the mockup's "Greedy isn't optimal" example, a square whose best tour is over K,
// and four seeded random complete graphs of 7 to 11 cities (two past the 150-step cap). With each step's "tree" removed, the
// steps must be exactly those, so no other picture changes. The other tests check the tree links themselves.
public class SearchTreeTsp_Tests {
    public static TheoryData<string, string, string> Snapshot {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var line in File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", "searchtree_tsp_steps.tsv")).Where(l => l.Length > 0)) {
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

    private static List<SolverStep<Tour>> Steps(string solver, string instance) =>
        Create(solver).Run(instance, withSteps: true).Steps.Cast<SolverStep<Tour>>().ToList();

    [Theory]
    [MemberData(nameof(Snapshot))]
    public void EveryStep_NamesItsNode_PathsAndTheirExtensions(string solver, string instance, string _) {
        var steps = Steps(solver, instance);
        var made = new Dictionary<int, SearchNode>();
        var path = new Dictionary<int, List<string>>();   // each node's cities, read off the tree
        int expanding = -1;
        for (int i = 0; i < steps.Count && i < StepRecorder<Tour>.DefaultCap; i++) {
            var step = steps[i];
            if (step.Event == StepEvent.Done) {
                Assert.Null(step.Tree);
                continue;
            }
            var node = Assert.Single(step.Tree!);
            var cities = step.Partial.Order;
            if (i == 0) {
                // The root: the start city, queued with the initial lower bound (the first expansion checks its label).
                Assert.Equal((0, (int?)null, SearchNodeState.Queued), (node.NodeId, node.ParentId, node.State));
                Assert.Matches(@"^lb [\d,.]+$", node.Bound);
                made[0] = node;
                path[0] = [node.Label];
                continue;
            }
            bool isNew = !made.ContainsKey(node.NodeId);
            if (isNew) {
                // An extension of the path being expanded, numbered in order.
                Assert.Equal(made.Count, node.NodeId);
                Assert.Equal(expanding, node.ParentId);
                Assert.Equal(path[expanding].Append(node.Label), cities);
                made[node.NodeId] = node;
                path[node.NodeId] = [.. cities];
                if (step.Event == StepEvent.Accept) {
                    Assert.EndsWith("queued.", step.Caption);
                    Assert.Equal(SearchNodeState.Queued, node.State);
                    Assert.Matches(@"^lb [\d,.]+$", node.Bound);
                } else {
                    Assert.Equal(StepEvent.Reject, step.Event);
                    Assert.Equal(SearchNodeState.Pruned, node.State);
                    if (step.Caption.StartsWith("There is no edge", StringComparison.Ordinal)) Assert.Equal("no edge", node.Bound);
                    else Assert.Matches(@"^lb [\d,.]+ ≥ [\d,.]+$", node.Bound);
                }
                continue;
            }
            // A queued path, taken off the queue.
            Assert.Equal(SearchNodeState.Queued, made[node.NodeId].State);
            Assert.Equal((made[node.NodeId].ParentId, made[node.NodeId].Label), (node.ParentId, node.Label));
            Assert.Equal(path[node.NodeId], cities.Take(path[node.NodeId].Count));
            switch (step.Event) {
                case StepEvent.Try:
                    Assert.StartsWith("Expand the path", step.Caption);
                    Assert.Equal((SearchNodeState.Explored, (string?)null), (node.State, node.Bound));
                    expanding = node.NodeId;
                    break;
                case StepEvent.Reject when step.Caption.StartsWith("Drop the path", StringComparison.Ordinal):
                    Assert.Equal(SearchNodeState.Pruned, node.State);
                    Assert.Matches(@"^lb [\d,.]+ ≥ [\d,.]+$", node.Bound);
                    break;
                case StepEvent.Reject:
                    Assert.StartsWith("Close the tour", step.Caption);
                    Assert.Equal(SearchNodeState.Failed, node.State);
                    break;
                default:
                    Assert.Equal(StepEvent.Accept, step.Event);
                    Assert.StartsWith("Close the tour", step.Caption);
                    Assert.Equal(SearchNodeState.Solution, node.State);
                    Assert.Matches(@"^tour [\d,.]+$", node.Bound);
                    break;
            }
            made[node.NodeId] = node;
        }
    }
}
