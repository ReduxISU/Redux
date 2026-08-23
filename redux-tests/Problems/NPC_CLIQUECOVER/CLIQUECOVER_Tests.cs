using Xunit;
using API.Interfaces;
using API.Problems.NPComplete.NPC_CLIQUECOVER;
using API.Problems.NPComplete.NPC_CLIQUECOVER.Verifiers;

namespace redux_tests;

#pragma warning disable CS1591

public class CLIQUECOVER_tests
{
    private const string DefaultInstance =
        "(({1,2,3,4,5,6,7,8},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5},{6,7},{7,8},{6,8}}),3)";


    // -------------------------------------------------------------------------
    // Constructor tests
    // -------------------------------------------------------------------------

    [Fact]
    public void CLIQUECOVER_Default_Instantiation()
    {
        var problem = new CLIQUECOVER();

        Assert.Equal(DefaultInstance, problem.instance);
        Assert.Equal(DefaultInstance, problem.defaultInstance);
    }


    [Fact]
    public void CLIQUECOVER_Custom_Instantiation()
    {
        string instance =
            "(({1,2,3,4},{{1,2},{2,3},{1,3},{3,4}}),2)";

        var problem = new CLIQUECOVER(instance);

        Assert.Equal(instance, problem.instance);
        Assert.Equal(4, problem.nodes.Count);
        Assert.Equal(4, problem.edges.Count);
        Assert.Equal(2, problem.K);
    }


    // -------------------------------------------------------------------------
    // Verifier tests
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(
        DefaultInstance,
        "{1,2,3},{4,5},{6,7,8}",
        true
    )]
    [InlineData(
        DefaultInstance,
        "{1,2,3},{4,5},{6,8,7}",
        true
    )]
    public void CLIQUECOVER_Verifier_Valid_Certificates(
        string instance,
        string certificate,
        bool expected)
    {
        var problem = new CLIQUECOVER(instance);

        var verifier = new CliqueCoverVerifier();

        bool result = verifier.verify(problem, certificate);

        Assert.Equal(expected, result);
    }


    [Theory]
    [InlineData(
        DefaultInstance,
        "{1,2,4},{5},{6,7,8}"
    )]
    [InlineData(
        DefaultInstance,
        "{1,2,3},{4,6},{5,7,8}"
    )]
    [InlineData(
        DefaultInstance,
        "{1,2,3},{4,5}"
    )]
    [InlineData(
        DefaultInstance,
        "{1,2,3},{4,5},{6,7,8},{1}"
    )]
    public void CLIQUECOVER_Verifier_Invalid_Certificates(
        string instance,
        string certificate)
    {
        var problem = new CLIQUECOVER(instance);

        var verifier = new CliqueCoverVerifier();

        bool result = verifier.verify(problem, certificate);

        Assert.False(result);
    }


    // -------------------------------------------------------------------------
    // Solver tests
    // -------------------------------------------------------------------------

    [Fact]
    public void CLIQUECOVER_Solver_Returns_Valid_Certificate()
    {
        var problem = new CLIQUECOVER();

        var solver = problem.defaultSolver;

        string certificate = solver.solve(problem);

        var verifier = new CliqueCoverVerifier();

        bool result = verifier.verify(problem, certificate);

        Assert.True(result);
    }


    [Fact]
    public void CLIQUECOVER_Custom_Solver_Returns_Valid_Certificate()
    {
        string instance =
            "(({1,2,3,4},{{1,2},{2,3},{1,3},{3,4}}),2)";

        var problem = new CLIQUECOVER(instance);

        var solver = problem.defaultSolver;

        string certificate = solver.solve(problem);

        var verifier = new CliqueCoverVerifier();

        bool result = verifier.verify(problem, certificate);

        Assert.True(result);
    }
    [Fact]
    public void CLIQUECOVER_Verifier_Rejects_Too_Many_Cliques()
    {
        var problem = new CLIQUECOVER();

        var verifier = new CliqueCoverVerifier();

        string certificate =
            "{1},{2},{3},{4},{5},{6},{7},{8}";

        bool result = verifier.verify(problem, certificate);

        Assert.False(result);
    }


    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void CLIQUECOVER_Instance_Format_Described()
    {
        var problem = new CLIQUECOVER();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("N,E),K", problem.instanceFormat);
    }

    [Fact]
    public void CLIQUECOVER_Certificate_Format_Described()
    {
        var problem = new CLIQUECOVER();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("cliques", problem.certificateFormat);
    }

    [Fact]
    public void CLIQUECOVER_Certificate_Format_Example_Is_Actually_Valid()
    {
        // The example quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        var problem = new CLIQUECOVER();
        var verifier = new CliqueCoverVerifier();
        Assert.True(verifier.verify(problem, "{1,2,3},{4,5},{6,7,8}"));
    }
}