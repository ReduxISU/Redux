using Xunit;
using API.Problems.NPComplete.NPC_DM3;
using API.Problems.NPComplete.NPC_DM3.Solvers;

namespace redux_tests;

#pragma warning disable CS1591

public class DM3_Tests {

    // -------------------------------------------------------------------------
    // Instantiation
    // -------------------------------------------------------------------------

    [Fact]
    public void DM3_Default_Instantiation() {
        DM3 problem = new DM3();
        Assert.Equal(DM3._defaultInstance, problem.defaultInstance);
        Assert.Equal(problem.defaultInstance, problem.instance);
    }

    // -------------------------------------------------------------------------
    // Parsing
    // -------------------------------------------------------------------------

    // Regression test for https://github.com/ReduxISU/Redux/issues/537 — ParseProblem used to
    // stride by 3 over the entire flattened instance string (header groups and M triples alike)
    // instead of stopping at each header group's own boundary, and ParseM independently chunked
    // the whole flattened stream into groups of 3, pulling the X/Y/Z header groups themselves into
    // M as spurious pseudo-triples. Both bugs corrupted the default instance: X came out as
    // {Paul,Madison,Chloe,Sally,Dave} (contaminated with Y/Z entries) and M.Count came out as 9
    // instead of 6.
    [Fact]
    public void DM3_ParseProblem_Cross_Contaminates_Header_Sets_And_M() {
        DM3 problem = new DM3();

        Assert.Equal(new List<string> { "Paul", "Sally", "Dave" }, problem.X);
        Assert.Equal(new List<string> { "Madison", "Austin", "Bob" }, problem.Y);
        Assert.Equal(new List<string> { "Chloe", "Frank", "Jake" }, problem.Z);
        Assert.Equal(6, problem.M.Count);
    }

    [Fact]
    public void DM3_ParseM_Only_Contains_M_Triples() {
        DM3 problem = new DM3();

        Assert.Equal(6, problem.M.Count);
        Assert.All(problem.M, triple => Assert.Equal(3, triple.Count));

        List<List<string>> expected = new List<List<string>> {
            new List<string> { "Paul", "Madison", "Chloe" },
            new List<string> { "Paul", "Austin", "Jake" },
            new List<string> { "Sally", "Bob", "Chloe" },
            new List<string> { "Sally", "Madison", "Frank" },
            new List<string> { "Dave", "Austin", "Chloe" },
            new List<string> { "Dave", "Bob", "Chloe" },
        };
        Assert.Equal(expected, problem.M);
    }

    // -------------------------------------------------------------------------
    // Solver
    // -------------------------------------------------------------------------

    [Fact]
    public void DM3_BruteForce_Solver_Finds_A_Matching_On_Default_Instance() {
        DM3 problem = new DM3();
        ThreeDimensionalMatchingBruteForce solver = new ThreeDimensionalMatchingBruteForce();

        string certificate = solver.solve(problem);

        Assert.NotEqual("{}", certificate);
        Assert.True(problem.defaultVerifier.verify(problem, certificate));
    }
}
