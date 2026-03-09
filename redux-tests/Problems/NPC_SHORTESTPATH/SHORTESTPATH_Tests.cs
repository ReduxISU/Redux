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
    public void TestSolverAndVerfierIntegration()
    {
        //Create a simple graph instance 
        string graphInstance = "({1,2,3,4,5},{({1,2},1),({2,3},1),({3,4},1),({4,5},1),({1,5},10),({4,5},9)})";
        SHORTESTPATH problem = new SHORTESTPATH(graphInstance);
        DijkstraSolver solver = new DijkstraSolver();
        ShortestPathVerifier verifier = new ShortestPathVerifier();

        //Solve the problem
        string solution = solver.solve(problem);

        bool isValid = verifier.verify(problem, solution);

        Assert.True(isValid, "The verifier should confirm that the solution provided by the solver is correct.");
        Assert.True(isValid, $"Solution: {solution}");
    }
}
