using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_DFS;
using API.Problems.NPComplete.NPC_DFS.Solvers;
using API.Problems.NPComplete.NPC_DFS.Verifiers;
using API.Problems.NPComplete.NPC_DFS.Visualizations;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class DFS_Tests
{
    [Fact]
    public void DFS_Default_Instantiation()
    {
        DFS problem = new DFS();
        Assert.Equal("(({1,2,3,4,5,6},((1,2),(2,4),(1,3),(3,5),(5,6))),1,6)", problem.instance);
        Assert.Equal("1", problem.sourceNode);
        Assert.Equal("6", problem.targetNode);
        Assert.True(problem.isDirected);
    }

    [Theory]
    [InlineData("(({1,2,3,4,5,6},((1,2),(2,4),(1,3),(3,5),(5,6))),1,6)", "{1,3,5,6}")]
    [InlineData("(({A,B,C,D},({A,B},{A,C},{C,D})),A,D)", "{A,C,D}")]
    [InlineData("(({1,2,3},((1,2))),1,3)", "{}")]
    public void DFS_Solver(string instance, string expectedCertificate)
    {
        DFS problem = new DFS(instance);
        DFSSolver solver = new DFSSolver();
        string solution = solver.solve(problem);
        Assert.Equal(expectedCertificate, solution);
    }

    [Theory]
    [InlineData("(({1,2,3,4,5,6},((1,2),(2,4),(1,3),(3,5),(5,6))),1,6)", "{1,3,5,6}", true)]
    [InlineData("(({1,2,3,4,5,6},((1,2),(2,4),(1,3),(3,5),(5,6))),1,6)", "{1,2,4}", false)]
    [InlineData("(({A,B,C,D},({A,B},{A,C},{C,D})),A,D)", "{A,C,D}", true)]
    [InlineData("(({1,2,3},((1,2))),1,3)", "{}", true)]
    [InlineData("(({1,2,3},((1,2))),1,3)", "{1,2}", false)]
    public void DFS_Verifier(string instance, string certificate, bool expected)
    {
        DFS problem = new DFS(instance);
        DFSVerifier verifier = new DFSVerifier();
        bool result = verifier.verify(problem, certificate);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void DFS_GetSteps_Tracks_Backtracking()
    {
        DFS problem = new DFS("(({1,2,3,4,5,6},((1,2),(2,4),(1,3),(3,5),(5,6))),1,6)");
        DFSSolver solver = new DFSSolver();

        List<object> steps = solver.GetSteps(problem);

        Assert.Equal(new[]
        {
            "{1}",
            "{1,2}",
            "{1,2,4}",
            "{1,2}",
            "{1}",
            "{1,3}",
            "{1,3,5}",
            "{1,3,5,6}"
        }, steps);
    }

    [Fact]
    public void DFS_Visualization_Highlights_Path_And_Endpoints()
    {
        DFS problem = new DFS("(({1,2,3,4,5,6},((1,2),(2,4),(1,3),(3,5),(5,6))),1,6)");
        DFSVisualization visualization = new DFSVisualization();

        API_GraphJSON graph = Assert.IsType<API_GraphJSON>(visualization.SolvedVisualization(problem, "{1,3,5,6}"));

        Assert.Equal("Green", graph.nodes.Single(node => node.name == "1").outline);
        Assert.Equal("Red", graph.nodes.Single(node => node.name == "6").outline);
        Assert.Equal("Solution", graph.nodes.Single(node => node.name == "3").color);
        Assert.Equal("Background", graph.nodes.Single(node => node.name == "2").color);
        Assert.Equal("Solution", graph.links.Single(link => link.source == "1" && link.target == "3").color);
        Assert.Equal("Background", graph.links.Single(link => link.source == "1" && link.target == "2").color);
    }

    [Fact]
    public void DFS_Rejects_Mixed_Edge_Directions()
    {
        string instance = "(({1,2,3},((1,2),{2,3})),1,3)";
        Assert.Throws<InvalidOperationException>(() => new DFS(instance));
    }
}
