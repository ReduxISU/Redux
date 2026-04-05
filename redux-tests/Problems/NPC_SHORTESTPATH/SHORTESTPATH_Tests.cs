using System;
using Xunit;
using API.Problems.NPComplete.NPC_SHORTESTPATH;
using API.Problems.NPComplete.NPC_SHORTESTPATH.Solvers;
using API.Problems.NPComplete.NPC_SHORTESTPATH.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class SHORTESTPATH_Tests
{
    [Fact]
    public void SHORTESTPATH_Default_Instantiation()
    {
        SHORTESTPATH problem = new SHORTESTPATH();
        Assert.Equal("({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)})", problem.instance);
        Assert.Equal("({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)})", problem.defaultInstance);
    }

    [Fact]
    public void SHORTESTPATH_Custom_Instantiation()
    {
        string instance = "({1,2,3,4,5,6},{((1,2),8),((1,3),4),((2,3),6),((3,5),5),((2,4),3),((4,5),9),((3,6),1),((4,6),12),((5,6),5)})";
        var problem = new SHORTESTPATH(instance);
        Assert.Equal(instance, problem.instance);
    }

    [Theory] //Tests independent set verifier with a few certificates

    [InlineData("({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)})", "{1,3,5}", true)]
    [InlineData("({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)})", "{1,2,3,5}", false)]
    [InlineData("({1,2,3,4,5,6},{((1,2),8),((1,3),4),((2,3),6),((3,5),5),((2,4),3),((4,5),9),((3,6),1),((4,6),12),((5,6),5)})", "{1,3,6}", true)]
    [InlineData("({1,2,3,4,5,6},{((1,2),8),((1,3),4),((2,3),6),((3,5),5),((2,4),3),((4,5),9),((3,6),1),((4,6),12),((5,6),5)})", "{1,2,4,5}", false)]
    [InlineData("(({1,2,3,4},{((1,2),1),((2,3),1),((3,4),1),((1,4),10)}),2,4)", "{2,3,4}", true)]
    [InlineData("(({1,2,3,4},{((1,2),1),((2,3),1),((3,4),1),((1,4),10)}),2,4)", "{1,2,3,4}", false)]
    [InlineData("(({A,B,C,D},{((A,B),2),((B,D),2),((A,C),1),((C,D),1)}),A,D)", "{A,C,D}", true)]
    public void SHORTESTPATH_Verifier(string instance, string certificate, bool expected)
    {
        SHORTESTPATH problem = new SHORTESTPATH(instance);
        ShortestPathVerifier verifier = new ShortestPathVerifier();
        bool result = verifier.verify(problem, certificate);
        Assert.Equal(expected, result);
    }

    [Theory] //Tests solver
    [InlineData("({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)})", "{1,3,5}")]
    [InlineData("({1,2,3,4,5,6},{((1,2),8),((1,3),4),((2,3),6),((3,5),5),((2,4),3),((4,5),9),((3,6),1),((4,6),12),((5,6),5)})", "{1,3,6}")]
    [InlineData("(({1,2,3,4},{((1,2),1),((2,3),1),((3,4),1),((1,4),10)}),2,4)", "{2,3,4}")]
    [InlineData("(({A,B,C,D},{((A,B),2),((B,D),2),((A,C),1),((C,D),1)}),A,D)", "{A,C,D}")]
    public void SHORTESTPATH_Solver(string instance, string certificate)
    {
        SHORTESTPATH problem = new SHORTESTPATH(instance);
        DijkstraSolver solver = new DijkstraSolver();
        string solution = solver.solve(problem);
        Assert.Equal(certificate, solution);
    }

    [Theory]
    [InlineData("({1,2},{((1,2),-1)})")]
    [InlineData("(({1,2},{((1,2),-1)}),1,2)")]
    public void SHORTESTPATH_Rejects_Negative_Weights(string instance)
    {
        Assert.Throws<InvalidOperationException>(() => new SHORTESTPATH(instance));
    }

    [Fact]
    public void SHORTESTPATH_Rejects_Source_Outside_Node_Set()
    {
        string instance = "(({1,2,3},{((1,2),1),((2,3),1)}),4,3)";
        Assert.Throws<InvalidOperationException>(() => new SHORTESTPATH(instance));
    }
}
