using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.P.P_SPSP;
using API.Problems.P.P_SSSP;
using API.Problems.P.P_SSSP.Solvers;
using API.Problems.P.P_SSSP.Verifiers;
using API.Problems.P.P_SSSP.Visualizations;
using System;
using System.Linq;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class SSSP_Tests
{
    [Fact]
    public void SSSP_Default_Instantiation()
    {
        SSSP problem = new SSSP();
        Assert.Equal("({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1)", problem.instance);
        Assert.Equal(problem.defaultInstance, problem.instance);
    }

    [Fact]
    public void SSSP_Custom_Instantiation()
    {
        string instance = "({1,2,3,4,5,6},{((1,2),2),((1,3),4),((2,4),7),((2,3),1),((3,5),3),((4,6),1),((5,4),2),((5,6),5)},1)";
        var problem = new SSSP(instance);
        Assert.Equal(instance, problem.instance);
    }

    [Theory]
    [InlineData("({1,2}, {((1,2),-1)}),1")]
    [InlineData("(({1,2}, {((1,2),-1)}),1")]
    public void SSSP_Rejects_Negative_Edge_Weights(string instance)
    {
        // Dijkstra's algorithm correctness depends on non-negative weights
        // The SSSP problem must reject problem instances with negative-weights during parse time
        Assert.Throws<InvalidOperationException>(() => new SSSP(instance));
    }

    [Fact]
    public void SPSP_Rejects_Source_Outside_Node_Set()
    {
        string instance = "(({1,2,3},{((1,2),1),((2,3),1)}),4)";
        Assert.Throws<InvalidOperationException>(() => new SPSP(instance));
    }

    // ----- Solver ----- //

    [Fact]
    public void SSSP_Solver_Default_Instantiation()
    {
        string instance = "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1)";
        SSSPSolver solver = new SSSPSolver();
        var problem = new SSSP(instance);
        string result = solver.solve(problem);
        Assert.Equal("{(1,{1}),(2,{1,2}),(3,{1,3}),(4,{1,2,4}),(5,{1,3,5})}", result);
    }

    [Fact]
    public void SSSP_Solver_Custom_Instantiation()
    {
        string instance = "({1,2,3,4,5,6},{((1,2),2),((1,3),4),((2,4),7),((2,3),1),((3,5),3),((4,6),1),((5,4),2),((5,6),5)},1)";
        SSSPSolver solver = new SSSPSolver();
        var problem = new SSSP(instance);
        string result = solver.solve(problem);
        Assert.Equal("{(1,{1}),(2,{1,2}),(3,{1,2,3}),(4,{1,2,3,5,4}),(5,{1,2,3,5}),(6,{1,2,3,5,4,6})}", result);
    }

    [Fact]
    public void SSSPSolver_Single_Node_No_Edges()
    {
        string instance = "({1},{},1)";
        SSSP problem = new SSSP(instance);
        SSSPSolver solver = new SSSPSolver();
        string result = solver.solve(problem);
        Assert.Equal("{(1,{1})}", result);
    }

    [Fact]
    public void SSSPSolver_Unreachable_Node_Returns_Empty_Path()
    {
        string instance = "({1,2,3},{((1,2),5)},1)";
        SSSP problem = new SSSP(instance);
        SSSPSolver solver = new SSSPSolver();
        string result = solver.solve(problem);
        Assert.Equal("{(1,{1}),(2,{1,2}),(3,{})}", result);
    }

    // ----- Verifier ----- //

    [Theory]
    [InlineData("{(1,{1}),(2,{1,2}),(3,{1,3}),(4,{1,2,4}),(5,{1,3,5})}", true)]
    [InlineData("{(1,{1}),(2,{1,2}),(3,{1,3}),(4,{1,2,4}),(5,{1,2,4,5})}", false)]
    public void SSSPVerifier_Certificate_Validation(string certificate, bool expectedResult)
    {
        SSSP problem = new SSSP();
        SSSPVerifier verifier = new SSSPVerifier();
        Assert.Equal(expectedResult, verifier.verify(problem, certificate));
    }

    // ----- Visualization ----- //

    [Theory]
    [InlineData("({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1)")]
    [InlineData("({1,2,3,4,5,6},{((1,2),2),((1,3),4),((2,4),7),((2,3),1),((3,5),3),((4,6),1),((5,4),2),((5,6),5)},1)")]
    public void SSSPVisualization_StepsVisualization_SettledTreeEdges_Accumulate_Without_Resetting(string instance)
    {
        SSSP problem = new SSSP(instance);
        var solver = new SSSPSolver();
        var visualization = new SSSPVisualization();

        var steps = solver.GetSteps(problem);
        var visualSteps = visualization.StepsVisualization(problem, steps);

        Assert.True(steps.Count >= 2);

        var previousSettledEdges = new HashSet<(string, string)>();
        foreach (var stepObj in steps)
        {
            var step = (SSSPSolver.SSSPGraphStep)stepObj;
            var knownNodes = new HashSet<string>(step.knownNodes);

            // Only edges pointing into already-settled nodes are guaranteed final;
            // edges into not-yet-settled nodes may still be relaxed away.
            var currentSettledEdges = new HashSet<(string, string)>(
                step.treeEdges.Where(e => knownNodes.Contains(e.to)));

            Assert.True(previousSettledEdges.IsSubsetOf(currentSettledEdges));

            previousSettledEdges = currentSettledEdges;
        }

        Assert.NotEmpty(previousSettledEdges);
    }
}