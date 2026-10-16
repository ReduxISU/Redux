using System.Text.Json;
using System.Text.Json.Nodes;
using API.Interfaces;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Traveling Salesperson Branch and Bound's steps as the Search Tree picture reads them.
// Endpoints/Snapshots/searchtree_tsp_steps.tsv holds (solver, instance, step count, then a hash of each step: the first
// 16 hex digits of the SHA-256 of its compact JSON, so a changed step is reported with its new JSON) captured BEFORE
// the solver reported its search tree: every branch and bound row of tour_solver_answers.tsv, the mockup's "Greedy
// isn't optimal" example, a square whose best tour is over K, and four seeded random complete graphs of 7 to 11 cities
// (two past the 150-step cap). With each step's "tree" removed, the steps must hash the same, so no other picture
// changes. The one change since is the Done caption of a run that never beats the greedy tour, which now says so (#897).
// The other tests check the tree links themselves.
public class SearchTreeTsp_Tests {
    internal static IEnumerable<(string Solver, string Instance, int Count, string[] Hashes)> Rows() =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", "searchtree_tsp_steps.tsv")).Where(l => l.Length > 0).Select(l => {
            var parts = l.Split('\t');
            return (parts[0], parts[1], int.Parse(parts[2]), parts[3].Split(' '));
        });

    public static TheoryData<string, string> Snapshot {
        get {
            var data = new TheoryData<string, string>();
            foreach (var (solver, instance, _, _) in Rows()) data.Add(solver, instance);
            return data;
        }
    }

    private static ISolver Create(string name) => (ISolver)Activator.CreateInstance(ProblemProvider.Solvers[name])!;

    private static JsonArray StepsJson(SolveRun run) =>
        JsonNode.Parse(JsonSerializer.Serialize(run.Steps, ProblemProvider.VisualizeJsonOptions()))!.AsArray();

    private static string Hash(JsonNode step) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(step.ToJsonString())))[..16].ToLowerInvariant();

    [Theory]
    [MemberData(nameof(Snapshot))]
    public void Steps_WithoutTheirTree_AreWhatTheyWereBeforeTheSearchTree(string solver, string instance) {
        var (_, _, count, hashes) = Rows().Single(r => r.Solver == solver && r.Instance == instance);
        var steps = StepsJson(Create(solver).Run(instance, withSteps: true));
        foreach (var step in steps) step!.AsObject().Remove("tree");
        Assert.Equal(count, steps.Count);
        for (int i = 0; i < count; i++)
            if (Hash(steps[i]!) != hashes[i]) Assert.Fail($"Step {i} changed. It is now {steps[i]!.ToJsonString()}");
    }

    private static List<SolverStep<Tour>> Steps(string solver, string instance) =>
        Create(solver).Run(instance, withSteps: true).Steps.Cast<SolverStep<Tour>>().ToList();

    [Theory]
    [MemberData(nameof(Snapshot))]
    public void EveryStep_NamesItsNode_PathsAndTheirExtensions(string solver, string instance) {
        var steps = Steps(solver, instance);
        var made = new Dictionary<int, SearchNode>();
        var path = new Dictionary<int, List<string>>();   // each node's cities, read off the tree
        int expanding = -1;
        for (int i = 0; i < steps.Count && i < StepRecorder<Tour>.DefaultCap; i++) {
            var step = steps[i];
            if (step.Event == StepEvent.Done) continue;
            var cities = step.Partial.Order;
            if (i == 0) {
                // The root: the start city, queued with the initial lower bound (the first expansion checks its label). With
                // it, when the greedy solver found a tour, that tour's own leaf under the root: the solution so far (#897).
                var root = step.Tree![0];
                Assert.Equal((0, (int?)null, SearchNodeState.Queued), (root.NodeId, root.ParentId, root.State));
                Assert.Matches(@"^lb [\d,.]+$", root.Bound);
                made[0] = root;
                path[0] = [root.Label];
                if (step.Event == StepEvent.Accept) {
                    Assert.StartsWith("Start from the greedy tour", step.Caption);
                    var greedy = step.Tree[1];
                    Assert.Equal((1, (int?)0, "greedy tour", SearchNodeState.Solution), (greedy.NodeId, greedy.ParentId, greedy.Label, greedy.State));
                    Assert.Matches(@"^tour [\d,.]+$", greedy.Bound);
                    Assert.Equal(2, step.Tree.Count);
                    made[1] = greedy;
                } else {
                    Assert.Single(step.Tree);
                }
                continue;
            }
            var node = Assert.Single(step.Tree!);
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

    [Theory]
    [MemberData(nameof(Snapshot))]
    public void TheDoneStep_NamesTheBestToursPath_WhenTheSearchFoundIt(string solver, string instance) {
        var steps = Steps(solver, instance);
        var done = steps[^1];
        var accepts = steps.Where(s => s.Event == StepEvent.Accept && s.Caption.StartsWith("Close the tour", StringComparison.Ordinal)).ToList();
        if (done.Ok != true) {
            // No answer: nothing to name.
            Assert.Null(done.Tree);
            return;
        }
        var path = done.Tree!;
        if (accepts.Count == 0 && steps.Count <= StepRecorder<Tour>.DefaultCap + 1) Assert.Equal("greedy tour", path[^1].Label);
        if (path is [{ Label: "greedy tour" } greedy]) {
            // The greedy tour was never beaten (past the cap it may still be the best; then no kept step closed a better one
            // either): the answer is its leaf under the root, and the caption says so (#897).
            Assert.Empty(accepts);
            Assert.Equal((1, (int?)0, SearchNodeState.Solution), (greedy.NodeId, greedy.ParentId, greedy.State));
            Assert.Equal(steps[0].Tree![1], greedy);
            Assert.StartsWith("The cheapest tour is the greedy tour ", done.Caption);
            Assert.EndsWith(": the search found nothing cheaper.", done.Caption);
            return;
        }
        Assert.Equal((0, (int?)null), (path[0].NodeId, path[0].ParentId));
        for (int i = 1; i < path.Count; i++) Assert.Equal(path[i - 1].NodeId, path[i].ParentId);
        Assert.All(path.SkipLast(1), n => Assert.Equal(SearchNodeState.Explored, n.State));
        Assert.Equal(SearchNodeState.Solution, path[^1].State);
        Assert.Matches(@"^tour [\d,.]+$", path[^1].Bound);
        // It spells the answer, a tour that starts where the search starts.
        Assert.Equal(done.Partial.Order.SkipLast(1), path.Select(n => n.Label));
        // The last tour the search kept closing is this one, when it was kept.
        if (accepts.Count > 0 && accepts[^1].Tree![0].NodeId == path[^1].NodeId) Assert.Equal(path[^1].Bound, accepts[^1].Tree![0].Bound);
    }
}
