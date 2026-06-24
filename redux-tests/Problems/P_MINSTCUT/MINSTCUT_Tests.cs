using Xunit;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.P.P_MINSTCUT;
using API.Problems.P.P_MINSTCUT.Solvers;
using API.Problems.P.P_MINSTCUT.Verifiers;
using API.Problems.P.P_MINSTCUT.Visualizations;

namespace redux_tests;
#pragma warning disable CS1591

public class MINSTCUT_Tests
{
    // ----- Instantiation ----- //

    [Fact]
    public void MINSTCUT_Default_Instantiation()
    {
        MINSTCUT problem = new MINSTCUT();
        Assert.Equal(MINSTCUT._defaultInstance, problem.instance);
        Assert.Equal(MINSTCUT._defaultInstance, problem.defaultInstance);
        Assert.Equal(4, problem.nodes.Count);
        Assert.Equal(4, problem.edges.Count);
        Assert.Equal("1", problem.sourceNode);
        Assert.Equal("4", problem.targetNode);
    }

    [Fact]
    public void MINSTCUT_Custom_Instantiation()
    {
        string instance = "({s,a,t},{((s,a),3),((a,t),5)},s,t)";
        MINSTCUT problem = new MINSTCUT(instance);
        Assert.Equal(instance, problem.instance);
        Assert.Equal(3, problem.nodes.Count);
        Assert.Equal(2, problem.edges.Count);
        Assert.Equal("s", problem.sourceNode);
        Assert.Equal("t", problem.targetNode);
    }

    [Fact]
    public void MINSTCUT_Rejects_Source_Outside_Node_Set()
    {
        string instance = "({1,2,3},{((1,2),5),((2,3),3)},9,3)";
        Assert.Throws<InvalidOperationException>(() => new MINSTCUT(instance));
    }

    [Fact]
    public void MINSTCUT_Rejects_Target_Outside_Node_Set()
    {
        string instance = "({1,2,3},{((1,2),5),((2,3),3)},1,9)";
        Assert.Throws<InvalidOperationException>(() => new MINSTCUT(instance));
    }

    // ----- Solver ----- //

    [Theory]
    // default: min cut = 5 (cut edges (1,3)=2 and (2,4)=3)
    [InlineData("({1,2,3,4},{((1,2),10),((1,3),2),((2,4),3),((3,4),5)},1,4)", 5)]
    // 2-node: min cut = 7
    [InlineData("({1,2},{((1,2),7)},1,2)", 7)]
    // chain: min cut = 2 (bottleneck is (2,3)=2)
    [InlineData("({1,2,3},{((1,2),4),((2,3),2)},1,3)", 2)]
    public void MINSTCUT_Solver_Returns_Correct_Min_Cut_Capacity(string instance, int expectedCapacity)
    {
        MINSTCUT problem = new MINSTCUT(instance);
        MinSTCutSolver solver = new MinSTCutSolver();
        string solution = solver.solve(problem);
        int actualCapacity = ComputeCutCapacity(problem, solution);
        Assert.Equal(expectedCapacity, actualCapacity);
    }

    [Fact]
    public void MINSTCUT_Solver_Default_Instance_Min_Cut_Is_5()
    {
        MINSTCUT problem = new MINSTCUT();
        string solution = new MinSTCutSolver().solve(problem);
        Assert.Equal(5, ComputeCutCapacity(problem, solution));
    }

    [Fact]
    public void MINSTCUT_Solver_Source_In_Result()
    {
        MINSTCUT problem = new MINSTCUT();
        string solution = new MinSTCutSolver().solve(problem);
        HashSet<string> sNodes = ParseNodeSet(solution);
        Assert.Contains(problem.sourceNode, sNodes);
    }

    [Fact]
    public void MINSTCUT_Solver_Target_Not_In_Result()
    {
        MINSTCUT problem = new MINSTCUT();
        string solution = new MinSTCutSolver().solve(problem);
        HashSet<string> sNodes = ParseNodeSet(solution);
        Assert.DoesNotContain(problem.targetNode, sNodes);
    }

    [Fact]
    public void MINSTCUT_Solver_Output_Passes_Verifier()
    {
        string[] instances = {
            MINSTCUT._defaultInstance,
            "({1,2},{((1,2),7)},1,2)",
            "({1,2,3},{((1,2),4),((2,3),2)},1,3)",
            "({s,a,b,t},{((s,a),4),((s,b),3),((a,t),3),((b,t),4)},s,t)"
        };
        foreach (string inst in instances)
        {
            MINSTCUT problem = new MINSTCUT(inst);
            string solution = new MinSTCutSolver().solve(problem);
            Assert.True(new MinSTCutVerifier().verify(problem, solution), $"Solver output failed verifier for: {inst}");
        }
    }

    // ----- Verifier ----- //

    [Theory]
    // default: min cut = 5, S={1,2} → cut edges (1,3)+(2,4)=2+3=5
    [InlineData("({1,2,3,4},{((1,2),10),((1,3),2),((2,4),3),((3,4),5)},1,4)", "{1,2}", true)]
    // 2-node: min cut = 7, S={1}
    [InlineData("({1,2},{((1,2),7)},1,2)", "{1}", true)]
    // chain: min cut = 2, S={1,2}
    [InlineData("({1,2,3},{((1,2),4),((2,3),2)},1,3)", "{1,2}", true)]
    public void MINSTCUT_Verifier_Accepts_Valid_Minimum_Cut(string instance, string certificate, bool expected)
    {
        MINSTCUT problem = new MINSTCUT(instance);
        MinSTCutVerifier verifier = new MinSTCutVerifier();
        Assert.Equal(expected, verifier.verify(problem, certificate));
    }

    [Theory]
    // S={1}: (1,2)+(1,3)=10+2=12, not min cut (5)
    [InlineData("({1,2,3,4},{((1,2),10),((1,3),2),((2,4),3),((3,4),5)},1,4)", "{1}")]
    // S={1,2,3}: (2,4)+(3,4)=3+5=8, not min cut (5)
    [InlineData("({1,2,3,4},{((1,2),10),((1,3),2),((2,4),3),((3,4),5)},1,4)", "{1,2,3}")]
    public void MINSTCUT_Verifier_Rejects_Non_Minimum_Cut(string instance, string certificate)
    {
        MINSTCUT problem = new MINSTCUT(instance);
        MinSTCutVerifier verifier = new MinSTCutVerifier();
        Assert.False(verifier.verify(problem, certificate));
    }

    [Fact]
    public void MINSTCUT_Verifier_Rejects_Missing_Source()
    {
        // S={2} does not contain source=1
        MINSTCUT problem = new MINSTCUT("({1,2,3},{((1,2),4),((2,3),2)},1,3)");
        MinSTCutVerifier verifier = new MinSTCutVerifier();
        Assert.False(verifier.verify(problem, "{2}"));
    }

    [Fact]
    public void MINSTCUT_Verifier_Rejects_Target_In_S()
    {
        // S={1,3} contains target=3
        MINSTCUT problem = new MINSTCUT("({1,2,3},{((1,2),4),((2,3),2)},1,3)");
        MinSTCutVerifier verifier = new MinSTCutVerifier();
        Assert.False(verifier.verify(problem, "{1,3}"));
    }

    [Fact]
    public void MINSTCUT_Verifier_Rejects_Node_Not_In_Graph()
    {
        MINSTCUT problem = new MINSTCUT("({1,2,3},{((1,2),4),((2,3),2)},1,3)");
        MinSTCutVerifier verifier = new MinSTCutVerifier();
        Assert.False(verifier.verify(problem, "{1,99}"));
    }

    [Fact]
    public void MINSTCUT_Verifier_Rejects_Empty_Certificate()
    {
        MINSTCUT problem = new MINSTCUT();
        MinSTCutVerifier verifier = new MinSTCutVerifier();
        Assert.False(verifier.verify(problem, "{}"));
    }

    // ----- Visualization ----- //

    [Fact]
    public void MINSTCUT_Visualization_Returns_Graph()
    {
        MINSTCUT problem = new MINSTCUT();
        MinSTCutVisualization viz = new MinSTCutVisualization();
        var graph = (API_GraphJSON)viz.visualize(problem);
        Assert.Equal(4, graph.nodes.Count);
        Assert.Equal(4, graph.links.Count);
    }

    [Fact]
    public void MINSTCUT_SolvedVisualization_Colors_Cut_Edges()
    {
        // S={1,2}: cut edges are (1,3) and (2,4)
        MINSTCUT problem = new MINSTCUT();
        MinSTCutVisualization viz = new MinSTCutVisualization();
        var graph = (API_GraphJSON)viz.SolvedVisualization(problem, "{1,2}");

        var link13 = graph.links.FirstOrDefault(l => l.source == "1" && l.target == "3");
        Assert.NotNull(link13);
        Assert.Equal("Solution", link13!.color);

        var link24 = graph.links.FirstOrDefault(l => l.source == "2" && l.target == "4");
        Assert.NotNull(link24);
        Assert.Equal("Solution", link24!.color);
    }

    [Fact]
    public void MINSTCUT_SolvedVisualization_Does_Not_Color_Internal_Edges()
    {
        // S={1,2}: (1,2) goes from S to S, not a cut edge
        MINSTCUT problem = new MINSTCUT();
        MinSTCutVisualization viz = new MinSTCutVisualization();
        var graph = (API_GraphJSON)viz.SolvedVisualization(problem, "{1,2}");

        var link12 = graph.links.FirstOrDefault(l => l.source == "1" && l.target == "2");
        Assert.NotNull(link12);
        Assert.NotEqual("Solution", link12!.color);
    }

    [Fact]
    public void MINSTCUT_SolvedVisualization_Colors_S_Nodes()
    {
        MINSTCUT problem = new MINSTCUT();
        MinSTCutVisualization viz = new MinSTCutVisualization();
        var graph = (API_GraphJSON)viz.SolvedVisualization(problem, "{1,2}");

        Assert.Equal("Solution", graph.nodes.First(n => n.name == "1").color);
        Assert.Equal("Solution", graph.nodes.First(n => n.name == "2").color);
    }

    [Fact]
    public void MINSTCUT_SolvedVisualization_Colors_T_Nodes_As_Background()
    {
        MINSTCUT problem = new MINSTCUT();
        MinSTCutVisualization viz = new MinSTCutVisualization();
        var graph = (API_GraphJSON)viz.SolvedVisualization(problem, "{1,2}");

        Assert.Equal("Background", graph.nodes.First(n => n.name == "3").color);
        Assert.Equal("Background", graph.nodes.First(n => n.name == "4").color);
    }

    [Fact]
    public void MINSTCUT_SolvedVisualization_Empty_Solution_Returns_Plain_Graph()
    {
        MINSTCUT problem = new MINSTCUT();
        MinSTCutVisualization viz = new MinSTCutVisualization();
        var graph = (API_GraphJSON)viz.SolvedVisualization(problem, "{}");
        Assert.All(graph.nodes, n => Assert.NotEqual("Solution", n.color));
        Assert.All(graph.links, l => Assert.NotEqual("Solution", l.color));
    }

    // ----- Helpers ----- //

    private static int ComputeCutCapacity(MINSTCUT problem, string certificate)
    {
        if (string.IsNullOrWhiteSpace(certificate) || certificate.Trim() == "{}") return 0;
        HashSet<string> sNodes = ParseNodeSet(certificate);
        return MinSTCutSolver.CutCapacity(problem.edges, sNodes);
    }

    private static HashSet<string> ParseNodeSet(string certificate)
    {
        return certificate.Trim()
            .TrimStart('{').TrimEnd('}')
            .Split(',')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToHashSet();
    }
}
