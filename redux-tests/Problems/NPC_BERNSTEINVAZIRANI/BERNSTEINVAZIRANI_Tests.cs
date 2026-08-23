using Xunit;
using API.Interfaces;
using API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI;
using API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class BERNSTEINVAZIRANI_tests {
    [Fact]
    public void BERNSTEINVAZIRANI_Default_Instantiation() {
        var problem = new BERNSTEINVAZIRANI();
        Assert.Equal("(0,1,0,1,1,0,1,0)", problem.instance);
        Assert.Equal("(0,1,0,1,1,0,1,0)", problem.defaultInstance);
    }

    [Fact]
    public void BERNSTEINVAZIRANI_Custom_Instantiation() {
        string instance = "(0,1,0,1,0,1,0,1)";
        var problem = new BERNSTEINVAZIRANI(instance);
        Assert.Equal(instance, problem.instance);
    }

    [Theory] //tests verifier
    [InlineData("(0,1,0,1,1,0,1,0)", "(1,0,1)", true)]
    [InlineData("(0,1,1,0,1,0,0,1)", "(1,1,1)", true)]
    [InlineData("(0,1,0,1,0,1,0,1)", "(0,0,1)", true)]
    [InlineData("(0,0,0,0,0,0,0,0)", "(0,0,0)", true)]
    public void BERNSTEINVAZIRANI_verifier(string instance, string certificate, bool expected) {
        var problem = new BERNSTEINVAZIRANI(instance);
        var verifier = new BernsteinVaziraniClassicalVerifier();
        bool result = verifier.verify(problem, certificate);
        Assert.Equal(expected, result);

    }

    // -------------------------------------------------------------------------
    // Constructor — invalid instances (all must throw ProblemParseException)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("101")]           // bare string — SPADE parse failure
    [InlineData("")]              // empty — SPADE parse failure
    [InlineData("(0,2,1,0)")]   // invalid bit value
    [InlineData("(0,x,1,0)")]   // non-numeric value
    [InlineData("(0,1,0)")]     // 3 entries — not a power of 2
    [InlineData("(0,1,0,1,0)")] // 5 entries — not a power of 2
    public void BERNSTEINVAZIRANI_Constructor_Throws_On_Invalid_Instance(string instance) {
        Assert.Throws<ProblemParseException>(() => new BERNSTEINVAZIRANI(instance));
    }

    // -------------------------------------------------------------------------
    // Verifier — invalid certificates (all must throw CertificateParseException)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("101")]         // old bare-string format — SPADE parse failure
    [InlineData("")]            // empty — SPADE parse failure
    [InlineData("(2,0,1)")]    // invalid bit value
    [InlineData("(x,0,1)")]    // non-numeric value
    [InlineData("(1,0)")]      // too few bits for a 3-bit problem
    [InlineData("(1,0,1,0)")] // too many bits for a 3-bit problem
    public void BERNSTEINVAZIRANI_Verifier_Throws_On_Invalid_Certificate(string certificate) {
        var problem = new BERNSTEINVAZIRANI("(0,1,0,1,1,0,1,0)");
        var verifier = new BernsteinVaziraniClassicalVerifier();
        Assert.Throws<CertificateParseException>(() => verifier.verify(problem, certificate));
    }

    [Theory] //tests solver
    [InlineData("(0,1,0,1,1,0,1,0)", "(1,0,1)")]
    [InlineData("(0,1,1,0,1,0,0,1)", "(1,1,1)")]
    [InlineData("(0,0,0,0,0,0,0,0)", "(0,0,0)")]
    [InlineData("(0,1,0,1,0,1,0,1)", "(0,0,1)")]
    public void BERNSTEINVAZIRANI_solver(string instance, string certificate) {
        var problem = new BERNSTEINVAZIRANI(instance);
        var solver = problem.defaultSolver;
        string solvedString = solver.solve(problem);
        Assert.Equal(certificate, solvedString);
    }
}