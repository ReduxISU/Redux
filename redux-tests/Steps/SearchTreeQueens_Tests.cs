using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using API.Interfaces;
using API.Interfaces.Board;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// N-Queens Backtracking's steps as the Search Tree picture reads them. Endpoints/Snapshots/searchtree_queens_steps.tsv holds
// (solver, instance as a JSON string, step count, then a hash of each step: the first 16 hex digits of the SHA-256 of its
// compact JSON) captured BEFORE the solver reported its search tree, on every N-Queens Backtracking row of
// board_solver_answers.tsv. With each step's "tree" removed, the steps must hash the same, so no other picture changes. The
// other tests check the tree links themselves.
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

    private static List<SolverStep<BoardCells>> Steps(string solver, string instance) =>
        Create(solver).Run(instance, withSteps: true).Steps.Cast<SolverStep<BoardCells>>().ToList();

    [Theory]
    [MemberData(nameof(Snapshot))]
    public void EveryStep_NamesTheSquaresItConsidered_UnderTheQueenAbove(string solver, string instance) {
        var steps = Steps(solver, instance);
        var made = new Dictionary<int, SearchNode>();
        for (int i = 0; i < steps.Count - 1 && i < StepRecorder<BoardCells>.DefaultCap; i++) {
            var step = steps[i];
            var tree = step.Tree!.ToList();
            if (i == 0) {
                // The very first step brings the root.
                Assert.Equal(new SearchNode(0, null, "start", SearchNodeState.Explored), tree[0]);
                made[0] = tree[0];
                tree.RemoveAt(0);
            }
            var node = tree[^1];
            // The squares skipped since the last queen in this row: new nodes under the same parent, cut, with the queen
            // that attacks them. Their number is the step's conflicts.
            var cuts = tree.SkipLast(1).ToList();
            Assert.Equal(step.Partial.Conflicts.Length, cuts.Count);
            foreach (var cut in cuts) {
                Assert.Equal(made.Count, cut.NodeId);
                Assert.Equal(SearchNodeState.Pruned, cut.State);
                Assert.Matches(@"^hit by \(\d+,\d+\)$", cut.Bound);
                made[cut.NodeId] = cut;
            }
            if (step.Event == StepEvent.Try) {
                // The queen placed: a new node under the queen of the row above (or the root), labelled with its square.
                Assert.Equal(made.Count, node.NodeId);
                Assert.True(node.ParentId is int p && made.ContainsKey(p));
                Assert.All(cuts, c => Assert.Equal(node.ParentId, c.ParentId));
                Assert.Matches(@"^\(\d+,\d+\)$", node.Label);
                int row = int.Parse(node.Label[1..node.Label.IndexOf(',')]);
                Assert.Equal(row == int.Parse(instance.Trim()) - 1 ? SearchNodeState.Solution : SearchNodeState.Explored, node.State);
                Assert.Equal(row, step.Partial.Values.Count - 1);
                made[node.NodeId] = node;
            } else {
                // A dead row: the queen above (or the root) is taken back.
                Assert.Equal(StepEvent.Reject, step.Event);
                Assert.Equal(SearchNodeState.Failed, node.State);
                Assert.Equal((made[node.NodeId].ParentId, made[node.NodeId].Label), (node.ParentId, node.Label));
                Assert.All(cuts, c => Assert.Equal(node.NodeId, c.ParentId));
                made[node.NodeId] = node;
            }
        }
    }

    [Theory]
    [MemberData(nameof(Snapshot))]
    public void TheDoneStep_NamesTheAnswersPath_RowByRow(string solver, string instance) {
        var steps = Steps(solver, instance);
        var done = steps[^1];
        int n = int.Parse(instance.Trim());
        if (done.Ok != true || n == 0) {
            Assert.Null(done.Tree);
            return;
        }
        var path = done.Tree!;
        Assert.Equal(new SearchNode(0, null, "start", SearchNodeState.Explored), path[0]);
        Assert.Equal(n + 1, path.Count);
        for (int i = 1; i < path.Count; i++) Assert.Equal(path[i - 1].NodeId, path[i].ParentId);
        Assert.Equal(SearchNodeState.Solution, path[^1].State);
        string answer = Create(solver).solve(instance);
        Assert.Equal(answer, "{" + string.Join(",", path.Skip(1).Select(q => q.Label)) + "}");
        // The last queen placed is the solution node.
        if (StepCaptions.NotShown(done.Caption) == 0) Assert.Equal(path[^1], steps[^2].Tree![^1]);
    }
}
