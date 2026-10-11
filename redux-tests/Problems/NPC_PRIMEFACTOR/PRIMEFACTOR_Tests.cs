using Xunit;
using API.Problems.NPComplete.NPC_PRIMEFACTOR;
using API.Problems.NPComplete.NPC_PRIMEFACTOR.Verifiers;
using API.Problems.NPComplete.NPC_PRIMEFACTOR.Solvers;

namespace redux_tests;
#pragma warning disable CS1591

public class PRIMEFACTOR_tests {
    [Fact]
    public void DEUTSCH_Default_Instantiation() {
        var problem = new PRIMEFACTOR();
        Assert.Equal("12", problem.instance);
        Assert.Equal("12", problem.defaultInstance);
    }

    [Fact]
    public void PRIMEFACTOR_Custom_Instantiation() {
        string instance = "56";
        var problem = new PRIMEFACTOR(instance);
        Assert.Equal(instance, problem.instance);
    }

    [Theory] //tests verifier
    [InlineData("12", "(2,2,3)")]
    [InlineData("12", "(2,3,2)")]
    [InlineData("12", "(3,2,2)")]
    [InlineData("7", "(7)")]
    [InlineData("100", "(2,5,2,5)")]
    [InlineData("97", "(97)")]
    [InlineData("3072", "(2,2,2,2,2,2,2,2,2,2,3)")]
    [InlineData("3072", "(2,2,2,2,2,2,2,3,2,2,2)")]
    [InlineData("87625999", "(71,839,1471)")]
    [InlineData("87625999", "(1471,71,839)")]
    public void PRIMEFACTOR_verifier(string instance, string certificate) {
        var problem = new PRIMEFACTOR(instance);
        var verifier = new PrimeFactorVerifier();
        Assert.True(verifier.verify(problem, certificate));
    }

    // #812: every factor must be prime, and the product must not overflow.
    [Theory]
    [InlineData("12", "(12)")]
    [InlineData("12", "(4,3)")]
    [InlineData("12", "(1,12)")]
    [InlineData("12", "(1,2,2,3)")]
    [InlineData("12", "(-2,-6)")]
    [InlineData("12", "(-2,-2,3)")]
    [InlineData("131073", "(65537,65537)")]
    [InlineData("1", "(1)")]
    public void PRIMEFACTOR_verifier_RefusesFactorsThatAreNotPrime(string instance, string certificate) {
        Assert.False(new PrimeFactorVerifier().verify(new PRIMEFACTOR(instance), certificate));
    }

    [Theory]
    [InlineData("1", "()")]   // 1 is the empty product
    [InlineData("2147483647", "(2147483647)")]
    [InlineData("2147483646", "(2,3,3,7,11,31,151,331)")]
    public void PRIMEFACTOR_verifier_AcceptsThePrimeFactorization(string instance, string certificate) {
        Assert.True(new PrimeFactorVerifier().verify(new PRIMEFACTOR(instance), certificate));
    }

    [Theory] //tests solver
    [InlineData("15", "(3,5)")]
    [InlineData("7", "(7)")]
    [InlineData("100", "(2,2,5,5)")]
    [InlineData("97", "(97)")]
    [InlineData("3072", "(2,2,2,2,2,2,2,2,2,2,3)")]
    [InlineData("87625999", "(71,839,1471)")]
    public void PRIMEFACTOR_solver(string instance, string certificate) {
        var problem = new PRIMEFACTOR(instance);
        var solver = new PrimeFactorSolver();
        string solvedString = solver.solve(problem);
        Assert.Equal(certificate, solvedString);
    }

    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void PRIMEFACTOR_Instance_Format_Described() {
        var problem = new PRIMEFACTOR();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("integer", problem.instanceFormat);
    }

    [Fact]
    public void PRIMEFACTOR_Certificate_Format_Described() {
        var problem = new PRIMEFACTOR();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("factors", problem.certificateFormat);
    }

    [Fact]
    public void PRIMEFACTOR_Certificate_Format_Example_Is_Actually_Valid() {
        // The "Example: (3,5)" quoted in certificateFormat must be a real,
        // verifiable certificate for defaultInstance — not just descriptive prose.
        var problem = new PRIMEFACTOR();
        var verifier = new PrimeFactorVerifier();
        Assert.True(verifier.verify(problem, PrimeFactorVerifier.CertificateExample));
    }
}