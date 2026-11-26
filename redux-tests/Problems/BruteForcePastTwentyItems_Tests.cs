using Xunit;
using API.Problems.NPComplete.NPC_CUT;
using API.Problems.NPComplete.NPC_INDEPENDENTSET;
using API.Problems.NPComplete.NPC_NODESET;
using API.Problems.NPComplete.NPC_STEINERTREE;
using API.Problems.NPComplete.NPC_WEIGHTEDCUT;

namespace redux_tests;
#pragma warning disable CS1591

// #1024, #1044, #783: these brute forces counted their candidates as n! / (k! (n-k)!) in a long. 21! overflows a long,
// so on 21 or more nodes (edges, for Steiner Tree) the count was garbage and they answered "none" at once. Each
// instance below has an answer among the first few candidates, so it runs fast once the count is right.
public class BruteForcePastTwentyItems_Tests {

    private const int N = 21;

    private static IEnumerable<int> Nodes => Enumerable.Range(1, N);
    private static string NodeList => "{" + string.Join(",", Nodes) + "}";
    private static IEnumerable<(int, int)> PathEdges => Enumerable.Range(1, N - 1).Select(i => (i, i + 1));

    [Fact] // a 21-node path, K = 2: {1,3} is independent
    public void IndependentSet_FindsASet_On21Nodes() {
        string edges = "{" + string.Join(",", PathEdges.Select(e => $"{{{e.Item1},{e.Item2}}}")) + "}";
        var problem = new INDEPENDENTSET($"(({NodeList},{edges}),2)");

        string answer = problem.defaultSolver.solve(problem);

        Assert.NotEqual("{}", answer);
        Assert.True(problem.defaultVerifier.verify(problem, answer));
    }

    [Fact] // a 21-node path, K = 1: the side {1} cuts one edge
    public void Cut_FindsACut_On21Nodes() {
        string edges = "{" + string.Join(",", PathEdges.Select(e => $"{{{e.Item1},{e.Item2}}}")) + "}";
        var problem = new CUT($"(({NodeList},{edges}),1)");

        string answer = problem.defaultSolver.solve(problem);

        Assert.NotEqual("{}", answer);
        Assert.True(problem.defaultVerifier.verify(problem, answer));
    }

    [Fact] // a 21-node path with every weight 1, K = 1: the side {1} cuts weight 1
    public void WeightedCut_FindsACut_On21Nodes() {
        string edges = "{" + string.Join(",", PathEdges.Select(e => $"({{{e.Item1},{e.Item2}}},1)")) + "}";
        var problem = new WEIGHTEDCUT($"(({NodeList},{edges}),1)");

        string answer = problem.defaultSolver.solve(problem);

        Assert.NotEqual("{}", answer);
        Assert.True(problem.defaultVerifier.verify(problem, answer));
    }

    [Fact] // a directed cycle through 21 nodes, K = 1: removing any one node breaks it
    public void NodeSet_FindsASet_On21Nodes() {
        string edges = "{" + string.Join(",", Nodes.Select(i => $"({i},{i % N + 1})")) + "}";
        var problem = new NODESET($"(({NodeList},{edges}),1)");

        string answer = problem.defaultSolver.solve(problem);

        Assert.NotEqual("{}", answer);
        Assert.True(problem.defaultVerifier.verify(problem, answer));
    }

    [Fact] // a 22-node path has 21 edges; terminals 1 and 2, K = 1: the edge {1,2} joins them
    public void SteinerTree_FindsATree_On21Edges() {
        string nodes = "{" + string.Join(",", Enumerable.Range(1, N + 1)) + "}";
        string edges = "{" + string.Join(",", Enumerable.Range(1, N).Select(i => $"{{{i},{i + 1}}}")) + "}";
        var problem = new STEINERTREE($"(({nodes},{edges}),{{1,2}},1)");

        string answer = problem.defaultSolver.solve(problem);

        Assert.NotEqual("{}", answer);
        Assert.True(problem.defaultVerifier.verify(problem, answer));
    }
}
