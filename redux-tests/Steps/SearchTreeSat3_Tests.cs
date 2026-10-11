using System.Text.Json;
using System.Text.Json.Nodes;
using API.Interfaces;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// 3SAT Backtracking's steps as the Search Tree picture reads them. Endpoints/Snapshots/searchtree_sat3_steps.tsv holds
// (solver, instance, the run's steps as compact JSON) captured BEFORE the solver reported its search tree: every 3SAT row of
// sat_solver_answers.tsv, the mockup's examples, and five seeded random formulas (one past the 150-step cap, two
// unsatisfiable). With each step's "tree" removed, the steps must be exactly those, so no other picture changes. The other
// tests check the tree links themselves.
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

    private static List<SolverStep<Assignment>> Steps(string solver, string instance) =>
        Create(solver).Run(instance, withSteps: true).Steps.Cast<SolverStep<Assignment>>().ToList();

    [Theory]
    [MemberData(nameof(Snapshot))]
    public void EveryDecision_NamesItsNode_UnderTheStateItDecides(string solver, string instance, string _) {
        var steps = Steps(solver, instance);
        var made = new Dictionary<int, SearchNode>();
        bool pastCap = steps.Count > StepRecorder<Assignment>.DefaultCap + 1;
        for (int i = 0; i < steps.Count; i++) {
            var step = steps[i];
            if (step.Event == StepEvent.Done) continue;
            var tree = Assert.IsAssignableFrom<IReadOnlyList<SearchNode>>(step.Tree);
            var node = tree[^1];
            if (i == 0) {
                // The very first step brings the root, queued, then the first value tried under it.
                Assert.Equal(2, tree.Count);
                Assert.Equal(new SearchNode(0, null, "start", SearchNodeState.Queued), tree[0]);
                made[0] = tree[0];
            } else {
                Assert.Single(tree);
            }
            var values = step.Partial.Values;
            switch (step.Event) {
                case StepEvent.Try:
                    // A new node, numbered in order, under a node already made, labelled with the value being tried.
                    Assert.False(made.ContainsKey(node.NodeId));
                    Assert.Equal(made.Count, node.NodeId);
                    Assert.True(node.ParentId is int parent && made.ContainsKey(parent));
                    Assert.Equal(SearchNodeState.Explored, node.State);
                    var tried = Assert.Single(values, v => step.Caption.StartsWith($"Try {v.Key} = {v.Value}", StringComparison.Ordinal));
                    Assert.Equal($"{tried.Key}={(tried.Value == "True" ? "T" : "F")}", node.Label);
                    made[node.NodeId] = node;
                    break;
                case StepEvent.Accept or StepEvent.Reject:
                    // What became of the value just tried (or, past the cap, of a value whose Try was not kept).
                    if (i < StepRecorder<Assignment>.DefaultCap) {
                        Assert.Equal(StepEvent.Try, steps[i - 1].Event);
                        Assert.Equal(made[node.NodeId] with { State = node.State, Bound = node.Bound }, node);
                    } else {
                        Assert.True(pastCap);
                    }
                    if (step.Event == StepEvent.Reject) {
                        Assert.Equal(SearchNodeState.Pruned, node.State);
                        Assert.Matches(@"^C\d+ false$", node.Bound);
                        Assert.Contains($"clause {node.Bound![..^6]} ", step.Caption);
                    } else if (step.Caption.EndsWith("so this branch is queued.", StringComparison.Ordinal)) {
                        Assert.Equal(SearchNodeState.Queued, node.State);
                        Assert.Matches(@"^priority -?\d+$", node.Bound);
                    } else {
                        Assert.Equal(SearchNodeState.Solution, node.State);
                        Assert.Equal(StepEvent.Done, steps[i + 1].Event);
                    }
                    made[node.NodeId] = node;
                    break;
                case StepEvent.Backtrack:
                    // Back to a queued node: it is taken off the queue.
                    Assert.Equal(SearchNodeState.Queued, made[node.NodeId].State);
                    Assert.Equal(made[node.NodeId] with { State = SearchNodeState.Explored, Bound = null }, node);
                    made[node.NodeId] = node;
                    break;
            }
        }
    }

    [Theory]
    [MemberData(nameof(Snapshot))]
    public void TheDoneStep_NamesTheSolutionsPath_WhichSpellsTheAssignmentItFound(string solver, string instance, string _) {
        var steps = Steps(solver, instance);
        var done = steps[^1];
        if (done.Ok != true) {
            Assert.Null(done.Tree);
            return;
        }
        var path = done.Tree!;
        Assert.Equal(new SearchNode(0, null, "start", SearchNodeState.Explored), path[0]);
        for (int i = 1; i < path.Count; i++) Assert.Equal(path[i - 1].NodeId, path[i].ParentId);
        Assert.All(path.Skip(1).SkipLast(1), n => Assert.Equal(SearchNodeState.Explored, n.State));
        Assert.Equal(SearchNodeState.Solution, path[^1].State);
        // The solution's node is the one the last Accept named, and the path spells the assignment it found.
        var answer = steps.Last(s => s.Event == StepEvent.Accept);
        Assert.Equal(answer.Tree![^1], path[^1]);
        Assert.Equal(answer.Partial.Values.Select(v => $"{v.Key}={(v.Value == "True" ? "T" : "F")}").Order(), path.Skip(1).Select(n => n.Label).Order());
    }
}
