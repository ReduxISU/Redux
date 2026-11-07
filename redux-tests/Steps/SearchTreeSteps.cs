using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The checks every test of a solver's search-tree nodes makes on its recorded steps, whatever the problem: every step
// before the Done carries nodes, each node is made once under one parent (the root, or a node made earlier), keeps its
// label, and the Done of a run with an answer names a path from the root that ends on a solution.
internal static class SearchTreeSteps {
    /// <summary>Checks the links of the tree the steps build; returns each node's parent and label.</summary>
    public static (Dictionary<int, int?> ParentOf, Dictionary<int, string> LabelOf) CheckLinks<TPartial>(IReadOnlyList<SolverStep<TPartial>> steps) {
        var parentOf = new Dictionary<int, int?>();
        var labelOf = new Dictionary<int, string>();
        foreach (var step in steps.Where(s => s.Event != StepEvent.Done)) {
            Assert.NotNull(step.Tree);
            Assert.NotEmpty(step.Tree!);
            foreach (var node in step.Tree!) {
                if (parentOf.TryGetValue(node.NodeId, out var parent)) {
                    Assert.Equal(parent, node.ParentId);
                    Assert.Equal(labelOf[node.NodeId], node.Label);
                    continue;
                }
                if (node.ParentId is int p) Assert.True(parentOf.ContainsKey(p), $"node {node.NodeId} names parent {p}, which does not exist yet");
                else Assert.Equal(0, node.NodeId);
                parentOf[node.NodeId] = node.ParentId;
                labelOf[node.NodeId] = node.Label;
            }
        }
        return (parentOf, labelOf);
    }

    /// <summary>
    /// Checks that the Done names the answer's path, root first, each node under the one before it (and under the parent
    /// the steps gave it, when they made it), ending on a solution; returns the path's labels below the root.
    /// </summary>
    public static List<string> AnswerPath<TPartial>(IReadOnlyList<SolverStep<TPartial>> steps, Dictionary<int, int?> parentOf) {
        var done = steps[^1];
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.True(done.Ok);
        // The path may follow nodes the Done marks failed (ones the search gave up after the last step kept).
        var path = done.Tree!.SkipWhile(n => n.ParentId != null).ToList();
        Assert.Equal("start", path[0].Label);
        Assert.Null(path[0].ParentId);
        for (int i = 1; i < path.Count; i++) {
            Assert.Equal(path[i - 1].NodeId, path[i].ParentId);
            if (parentOf.TryGetValue(path[i].NodeId, out var parent)) Assert.Equal(parent, path[i].ParentId);
        }
        Assert.Equal(SearchNodeState.Solution, path[^1].State);
        return path.Skip(1).Select(n => n.Label).ToList();
    }
}
