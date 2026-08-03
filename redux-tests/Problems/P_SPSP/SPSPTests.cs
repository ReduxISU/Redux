using System;
using System.Linq;
using Xunit;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.P.P_SPSP;
using API.Problems.P.P_SPSP.Solvers;
using API.Problems.P.P_SPSP.Verifiers;
using API.Problems.P.P_SPSP.Visualizations;

namespace redux_tests;
#pragma warning disable CS1591

public class SPSP_Tests
{
    [Fact]
    public void SPSP_Default_Instantiation()
    {
        SPSP problem = new SPSP();
        Assert.Equal("({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)", problem.instance);
        Assert.Equal(problem.defaultInstance, problem.instance);
    }

    [Fact]
    public void SPSP_Custom_Instantiation()
    {
        string instance = "({1,2,3,4,5,6},{((1,2),8),((1,3),4),((2,3),6),((3,5),5),((2,4),3),((4,5),9),((3,6),1),((4,6),12),((5,6),5)},1,6)";
        var problem = new SPSP(instance);
        Assert.Equal(instance, problem.instance);
    }

    [Theory]
    [InlineData("({1,2}, {((1,2),-1)}),1,2")]
    [InlineData("(({1,2}, {((1,2),-1)}),1,2")]
    public void SPSP_Rejects_Negative_Edge_Weights(string instance)
    {
        // Dijkstra's algorithm correctness depends on non-negative weights
        // The SSSP problem must reject problem instances with negative-weights during parse time
        Assert.Throws<InvalidOperationException>(() => new SPSP(instance));
    }

    [Fact]
    public void SPSP_Rejects_Source_Outside_Node_Set()
    {
        string instance = "(({1,2,3},{((1,2),1),((2,3),1)}),4,3)";
        Assert.Throws<InvalidOperationException>(() => new SPSP(instance));
    }

    // ----- Solver ----- //

    [Theory]
    [InlineData("({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)", "{1,3,5}")]
    [InlineData("(({A,B,C,D},{((A,B),2),((B,D),2),((A,C),1),((C,D),1)}),A,D)", "{A,C,D}")]
    [InlineData("(({1,2,3},((1,2))),1,3)", "{}")]
    public void SPSP_Solver(string instance, string certificate)
    {
        SPSP problem = new SPSP(instance);
        SPSPSolver solver = new SPSPSolver();
        string solution = solver.solve(problem);
        Assert.Equal(certificate, solution);
    }

    [Theory]
    [InlineData("({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)", 9)]
    [InlineData("({1,2,3,4,5,6},{((1,2),8),((1,3),4),((2,3),6),((3,5),5),((2,4),3),((4,5),9),((3,6),1),((4,6),12),((5,6),5)},1,6)", 5)]
    [InlineData("(({1,2,3,4},{((1,2),1),((2,3),1),((3,4),1),((1,4),10)}),2,4)", 2)]
    [InlineData("(({A,B,C,D},{((A,B),2),((B,D),2),((A,C),1),((C,D),1)}),A,D)", 2)]
    public void SPSP_Solver_Returns_Valid_Minimum_Cost_Path(string instance, int expectedMinCost)
    {
        SPSP problem = new SPSP(instance);
        SPSPSolver solver = new SPSPSolver();
        SPSPVerifier verifier = new SPSPVerifier();

        string solution = solver.solve(problem);

        Assert.True(verifier.verify(problem, solution), $"Solver returned an invalid certificate: {solution}");
        Assert.Equal(expectedMinCost, TotalPathWeight(problem, solution));
    }

    [Fact]
    public void SSSP_Solver_Handles_Equal_Weights()
    {
        string instance = "(({1,2,3},{((1,2),1),((2,3),1)}),1,1)";
        SPSP problem = new SPSP(instance);
        SPSPSolver solver = new SPSPSolver();

        string solution = solver.solve(problem);

        Assert.Equal("{1}", solution);
    }

    // ----- Verifier ----- //

    [Theory] //Tests independent set verifier with a few certificates
    [InlineData("({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)", "{1,3,5}", true)]
    [InlineData("({1,2,3,4,5},{(1,2),(1,3),(2,3),(3,5),(2,4),(4,5)},1,5)", "{1,3,5}", true)]
    [InlineData("({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)", "{1,2,3,5}", false)]
    [InlineData("({1,2,3,4,5,6},{((1,2),8),((1,3),4),((2,3),6),((3,5),5),((2,4),3),((4,5),9),((3,6),1),((4,6),12),((5,6),5)},1,6)", "{1,3,6}", true)]
    [InlineData("({1,2,3,4,5,6},{((1,2),8),((1,3),4),((2,3),6),((3,5),5),((2,4),3),((4,5),9),((3,6),1),((4,6),12),((5,6),5)},1,6)", "{1,2,4,5}", false)]
    [InlineData("(({1,2,3,4},{((1,2),1),((2,3),1),((3,4),1),((1,4),10)}),2,4)", "{2,3,4}", true)]
    [InlineData("(({1,2,3,4},{((1,2),1),((2,3),1),((3,4),1),((1,4),10)}),2,4)", "{1,2,3,4}", false)]
    [InlineData("(({A,B,C,D},{((A,B),2),((B,D),2),((A,C),1),((C,D),1)}),A,D)", "{A,C,D}", true)]
    public void SSSP_Verifier(string instance, string certificate, bool expected)
    {
        SPSP problem = new SPSP(instance);
        SPSPVerifier verifier = new SPSPVerifier();
        bool result = verifier.verify(problem, certificate);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SSSP_Verifier_Rejects_Empty_Certificates_When_Path_Exists()
    {
        string instance = "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)";
        SPSP problem = new SPSP(instance);
        SPSPVerifier verifier = new SPSPVerifier();
        Assert.False(verifier.verify(problem, "{}"));
    }

    // ----- Visualization ----- //
    [Fact]
    public void StepsVisualization_Highlights_Final_Path_Nodes_As_Solution()
    {
        string instance = "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)";
        SPSP problem = new SPSP(instance);
        SPSPVisualization visualization = new SPSPVisualization();

        var steps = new List<object> { "{1,3,5}" };
        var frames = visualization.StepsVisualization(problem, steps);

        var frame = Assert.IsType<API_GraphJSON>(frames[0]);
        Assert.Contains(frame.nodes, n => n.name == "1" && (n.color == "Solution" || n.color == "ElementHighlight"));
        Assert.Contains(frame.nodes, n => n.name == "5" && (n.color == "Solution" || n.color == "ElementHighlight"));
    }

    // ----- Helper ----- //
    // The purpose of this helper function is to parse a certificate like "{1,3,5}" and sums the edge weights along the path
    private static int TotalPathWeight(SPSP problem, string certificate)
    {
        var nodes = certificate.Trim('{', '}').Split(',').ToList();
        var adjacency = SPSPSolver.BuildAdjacencyList(problem.graph);

        int total = 0;
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            var edge = adjacency[nodes[i]].First(e => e.neighbor == nodes[i + 1]);
            total += edge.weight;
        }
        return total;
    }
}
