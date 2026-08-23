using Xunit;
using API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH;
using API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Solvers;
using API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class UNSTRUCTUREDSEARCH_tests {
    [Fact]
    public void DEUTSCH_Default_Instantiation() {
        var problem = new UNSTRUCTUREDSEARCH();
        Assert.Equal("(0, 1, 0, 0)", problem.instance);
        Assert.Equal("(0, 1, 0, 0)", problem.defaultInstance);
    }

    [Fact]
    public void DEUTSCH_Custom_Instantiation() {
        string instance = "(0, 0, 0, 1)";
        var problem = new UNSTRUCTUREDSEARCH(instance);
        Assert.Equal(instance, problem.instance);
    }

    [Theory] //tests verifier
    [InlineData("(1,0,0,0)", 0, true)]
    [InlineData("(1,0,0,0)", 1, false)]
    [InlineData("(1,0,0,0)", 2, false)]
    [InlineData("(1,0,0,0)", 3, false)]
    [InlineData("(0,1,0,0)", 1, true)]
    [InlineData("(0,0,1,0)", 2, true)]
    [InlineData("(0,0,0,1)", 3, true)]
    public void DEUTSCH_verifier(string instance, int certificate, bool expected) {
        var problem = new UNSTRUCTUREDSEARCH(instance);
        var verifier = problem.defaultVerifier;
        bool result = verifier.verify(problem, certificate.ToString());
        Assert.Equal(expected, result);

    }

    [Theory] //tests solver
    [InlineData("(1,0,0,0)", 0)]
    [InlineData("(0,1,0,0)", 1)]
    [InlineData("(0,0,1,0)", 2)]
    [InlineData("(0,0,0,1)", 3)]
    public void UNSTRUCTUREDSEARCH_solver(string instance, int certificate) {
        var problem = new UNSTRUCTUREDSEARCH(instance);
        var solver = new UnstructuredSearchSolver();
        string solvedString = solver.solve(problem);
        Assert.Equal($"{certificate}", solvedString);
    }

    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void UNSTRUCTUREDSEARCH_Instance_Format_Described()
    {
        var problem = new UNSTRUCTUREDSEARCH();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("f(0)", problem.instanceFormat);
    }

    [Fact]
    public void UNSTRUCTUREDSEARCH_Certificate_Format_Described()
    {
        var problem = new UNSTRUCTUREDSEARCH();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("index", problem.certificateFormat);
    }

    [Fact]
    public void UNSTRUCTUREDSEARCH_Certificate_Format_Example_Is_Actually_Valid()
    {
        // The "Example: 1" quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        var problem = new UNSTRUCTUREDSEARCH();
        var verifier = new UnstructuredSearchVerifier();
        Assert.True(verifier.verify(problem, "1"));
    }
}