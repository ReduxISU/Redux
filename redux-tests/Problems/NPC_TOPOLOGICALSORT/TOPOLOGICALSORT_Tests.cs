using System;
using Xunit;
using API.Problems.NPComplete.NPC_TOPOLOGICALSORT;
using API.Problems.NPComplete.NPC_TOPOLOGICALSORT.Solvers;
using API.Problems.NPComplete.NPC_TOPOLOGICALSORT.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class TOPOLOGICALSORT_Tests
{
    [Fact]
    public void TOPOLOGICALSORT_Default_Instantiation()
    {
        TOPOLOGICALSORT problem = new TOPOLOGICALSORT();
        Assert.Equal("({1,2,3,4,5,6},{(1,2),(1,3),(2,4),(3,4),(4,5),(3,6)})", problem.instance);
        Assert.Equal("({1,2,3,4,5,6},{(1,2),(1,3),(2,4),(3,4),(4,5),(3,6)})", problem.defaultInstance);
        Assert.Equal(6, problem.nodes.Count);
        Assert.Equal(6, problem.edges.Count);
    }

    [Fact]
    public void TOPOLOGICALSORT_Custom_Instantiation()
    {
        string instance = "({A,B,C},{(A,B),(B,C)})";
        TOPOLOGICALSORT problem = new TOPOLOGICALSORT(instance);
        Assert.Equal(instance, problem.instance);
        Assert.Equal(3, problem.nodes.Count);
        Assert.Equal(2, problem.edges.Count);
    }

    [Theory]
    [InlineData("({1,2,3},{(1,2),(2,3)})", "{1,2,3}", true)]
    [InlineData("({1,2,3},{(1,2),(2,3)})", "{3,2,1}", false)]
    [InlineData("({1,2,3},{(1,2),(2,3)})", "{1,3,2}", false)]
    [InlineData("({1,2,3},{(1,2),(2,3)})", "{1,2}", false)]
    [InlineData("({1,2,3},{(1,2),(2,3)})", "{1,2,3,3}", false)]
    [InlineData("({1,2,3,4,5,6},{(1,2),(1,3),(2,4),(3,4),(4,5),(3,6)})", "{1,2,3,4,6,5}", true)]
    [InlineData("({1,2,3,4,5,6},{(1,2),(1,3),(2,4),(3,4),(4,5),(3,6)})", "{1,3,2,4,6,5}", true)]
    [InlineData("({A,B,C},{(A,B),(B,C)})", "{A,B,C}", true)]
    [InlineData("({1,2,3},{(1,2),(2,3)})", "{}", false)]
    public void TOPOLOGICALSORT_Verifier(string instance, string certificate, bool expected)
    {
        TOPOLOGICALSORT problem = new TOPOLOGICALSORT(instance);
        TopologicalSortVerifier verifier = new TopologicalSortVerifier();
        bool result = verifier.verify(problem, certificate);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("({1,2,3},{(1,2),(2,3)})")]
    [InlineData("({1,2,3,4,5,6},{(1,2),(1,3),(2,4),(3,4),(4,5),(3,6)})")]
    [InlineData("({A,B,C,D},{(A,B),(A,C),(B,D),(C,D)})")]
    public void TOPOLOGICALSORT_Solver_ProducesValidOrder(string instance)
    {
        var problem = new TOPOLOGICALSORT(instance);
        var solver = new KahnsAlgorithm();
        var verifier = new TopologicalSortVerifier();
        string solution = solver.solve(problem);
        Assert.True(verifier.verify(problem, solution));
    }

    [Fact]
    public void TOPOLOGICALSORT_Solver_DefaultInstance()
    {
        var problem = new TOPOLOGICALSORT();
        var solver = new KahnsAlgorithm();
        var verifier = new TopologicalSortVerifier();
        string solution = solver.solve(problem);
        Assert.True(verifier.verify(problem, solution));
    }

    [Theory]
    [InlineData("({1,2,3},{(1,2),(2,3),(3,1)})")]
    [InlineData("({1,2},{(1,2),(2,1)})")]
    [InlineData("({1},{(1,1)})")]
    public void TOPOLOGICALSORT_Solver_CycleReturnsEmpty(string instance)
    {
        var problem = new TOPOLOGICALSORT(instance);
        string solution = new KahnsAlgorithm().solve(problem);
        Assert.Equal("{}", solution);
    }

    [Fact]
    public void TOPOLOGICALSORT_SingleNode_NoEdges()
    {
        var problem = new TOPOLOGICALSORT("({1},{})");
        var solver = new KahnsAlgorithm();
        var verifier = new TopologicalSortVerifier();
        string solution = solver.solve(problem);
        Assert.Equal("{1}", solution);
        Assert.True(verifier.verify(problem, solution));
    }

    [Fact]
    public void TOPOLOGICALSORT_TwoNodes_NoEdges()
    {
        var problem = new TOPOLOGICALSORT("({1,2},{})");
        var solver = new KahnsAlgorithm();
        var verifier = new TopologicalSortVerifier();
        string solution = solver.solve(problem);
        Assert.True(verifier.verify(problem, solution));
    }

    [Fact]
    public void TOPOLOGICALSORT_DisconnectedDAG()
    {
        var problem = new TOPOLOGICALSORT("({1,2,3,4},{(1,2),(3,4)})");
        var solver = new KahnsAlgorithm();
        var verifier = new TopologicalSortVerifier();
        string solution = solver.solve(problem);
        Assert.True(verifier.verify(problem, solution));
    }
}
