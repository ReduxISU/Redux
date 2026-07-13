using Xunit;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.P.P_MINCUT;
using API.Problems.P.P_MINCUT.Solvers;
using API.Problems.P.P_MINCUT.Verifiers;
using API.Problems.P.P_MINCUT.Visualizations;

namespace redux_tests;
#pragma warning disable CS1591

public class MINCUT_Tests
{
    // ----- Instantiation ----- //

    [Fact]
    public void MINCUT_Default_Instantiation()
    {
        MINCUT problem = new MINCUT();
        Assert.Equal(MINCUT._defaultInstance, problem.instance);
        Assert.Equal(MINCUT._defaultInstance, problem.defaultInstance);
    }

    [Fact]
    public void MINCUT_Custom_Instantiation()
    {
        string instance = "({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})";
        MINCUT problem = new MINCUT(instance);
        Assert.Equal(instance, problem.instance);
        Assert.Equal(3, problem.nodes.Count);
        Assert.Equal(3, problem.edges.Count);
    }

    [Fact]
    public void MINCUT_Two_Node_Graph()
    {
        string instance = "({1,2},{({1,2},7)})";
        MINCUT problem = new MINCUT(instance);
        Assert.Equal(2, problem.nodes.Count);
        Assert.Single(problem.edges);
    }

    // ----- Solver ----- //

    [Theory]
    [InlineData("({1,2,3,4,5},{({2,1},5),({1,3},4),({2,3},2),({3,5},1),({2,4},4),({4,5},2)})", 3)]
    [InlineData("({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})", 3)]
    [InlineData("({1,2},{({1,2},7)})", 7)]
    [InlineData("({1,2,3,4},{({1,2},3),({2,3},3),({3,4},3),({4,1},3)})", 6)]
    public void MINCUT_Solver_Returns_Correct_Weight(string instance, int expectedWeight)
    {
        MINCUT problem = new MINCUT(instance);
        MinCutStoerWagner solver = new MinCutStoerWagner();
        string solution = solver.solve(problem);
        int actualWeight = ParseTotalWeight(solution);
        Assert.Equal(expectedWeight, actualWeight);
    }

    [Fact]
    public void MINCUT_Solver_Default_Instance_Weight_Is_3()
    {
        MINCUT problem = new MINCUT();
        string solution = new MinCutStoerWagner().solve(problem);
        Assert.Equal(3, ParseTotalWeight(solution));
    }

    [Fact]
    public void MINCUT_Solver_Single_Node_Returns_Empty()
    {
        // Single-node graph - no cut possible
        string instance = "({1},{})";
        MINCUT problem = new MINCUT(instance);
        string solution = new MinCutStoerWagner().solve(problem);
        Assert.Equal("{}", solution);
    }

    [Fact]
    public void MINCUT_Solver_Certificate_Edges_All_Exist_In_Graph()
    {
        MINCUT problem = new MINCUT();
        string solution = new MinCutStoerWagner().solve(problem);
        Assert.NotEqual("{}", solution);

        // Each edge in the certificate must exist in the problem
        MinCutVerifier verifier = new MinCutVerifier();
        Assert.True(verifier.verify(problem, solution));
    }

    // ----- Verifier ----- //

    [Theory]
    [InlineData("({1,2,3,4,5},{({2,1},5),({1,3},4),({2,3},2),({3,5},1),({2,4},4),({4,5},2)})", "{({3,5},1),({4,5},2)}", true)]
    [InlineData("({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})", "{({1,2},1),({2,3},2)}", true)]
    [InlineData("({1,2},{({1,2},7)})", "{({1,2},7)}", true)]
    public void MINCUT_Verifier_Accepts_Valid_Minimum_Cut(string instance, string certificate, bool expected)
    {
        MINCUT problem = new MINCUT(instance);
        MinCutVerifier verifier = new MinCutVerifier();
        Assert.Equal(expected, verifier.verify(problem, certificate));
    }

    [Theory]
    [InlineData("({1,2,3,4,5},{({2,1},5),({1,3},4),({2,3},2),({3,5},1),({2,4},4),({4,5},2)})", "{({1,3},4),({2,3},2)}")]
    [InlineData("({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})", "{({1,2},1)}")]
    public void MINCUT_Verifier_Rejects_Non_Minimum_Cut(string instance, string certificate)
    {
        MINCUT problem = new MINCUT(instance);
        MinCutVerifier verifier = new MinCutVerifier();
        Assert.False(verifier.verify(problem, certificate));
    }

    [Fact]
    public void MINCUT_Verifier_Rejects_Nonexistent_Edge()
    {
        string instance = "({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})";
        MINCUT problem = new MINCUT(instance);
        MinCutVerifier verifier = new MinCutVerifier();
        Assert.False(verifier.verify(problem, "{({1,2},99)}"));
    }

    [Fact]
    public void MINCUT_Verifier_Rejects_Empty_For_Connected_Graph()
    {
        MINCUT problem = new MINCUT();
        MinCutVerifier verifier = new MinCutVerifier();
        Assert.False(verifier.verify(problem, "{}"));
    }

    [Fact]
    public void MINCUT_Solver_Output_Passes_Verifier()
    {
        string[] instances = {
            MINCUT._defaultInstance,
            "({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})",
            "({1,2},{({1,2},7)})",
            "({A,B,C,D},{({A,B},4),({B,C},3),({C,D},4),({D,A},3),({A,C},1),({B,D},1)})"
        };
        foreach (string inst in instances)
        {
            MINCUT problem = new MINCUT(inst);
            string solution = new MinCutStoerWagner().solve(problem);
            Assert.True(new MinCutVerifier().verify(problem, solution), $"Solver output failed verifier for: {inst}");
        }
    }

    // ----- Visualization ----- //

    [Fact]
    public void MINCUT_Visualization_Returns_Graph()
    {
        MINCUT problem = new MINCUT();
        MinCutVisualization viz = new MinCutVisualization();
        var graph = (API_GraphJSON)viz.visualize(problem);
        Assert.Equal(5, graph.nodes.Count);
    }

    [Fact]
    public void MINCUT_SolvedVisualization_Colors_Cut_Edges()
    {
        string instance = "({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})";
        MINCUT problem = new MINCUT(instance);
        MinCutVisualization viz = new MinCutVisualization();

        // Min cut for this graph: {2} vs {1,3}, edges ({1,2},1) and ({2,3},2), weight=3
        string solution = "{({1,2},1),({2,3},2)}";
        var graph = (API_GraphJSON)viz.SolvedVisualization(problem, solution);

        var link12 = graph.links.FirstOrDefault(l =>
            (l.source == "1" && l.target == "2") || (l.source == "2" && l.target == "1"));
        Assert.NotNull(link12);
        Assert.Equal("Solution", link12!.color);
    }

    [Fact]
    public void MINCUT_SolvedVisualization_Empty_Solution_Returns_Plain_Graph()
    {
        MINCUT problem = new MINCUT();
        MinCutVisualization viz = new MinCutVisualization();
        var graph = (API_GraphJSON)viz.SolvedVisualization(problem, "{}");
        Assert.All(graph.nodes, n => Assert.NotEqual("Solution", n.color));
    }

    // ----- Helper ----- //

    private static int ParseTotalWeight(string certificate)
    {
        if (string.IsNullOrWhiteSpace(certificate) || certificate.Trim() == "{}") return 0;
        var edgeList = new SPADE.UtilCollection(certificate);
        int total = 0;
        foreach (SPADE.UtilCollection item in edgeList)
            total += int.Parse(item[1].ToString());
        return total;
    }
}
