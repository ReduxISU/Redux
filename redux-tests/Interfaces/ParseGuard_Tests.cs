using API.Interfaces;
using API.Problems.NPComplete.NPC_SUBSETSUM;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class ParseGuard_Tests {
    [Fact]
    public void CreateProblem_NonParseConstructorFailure_IsWrappedWithInnerException() {
        // MINCUT's constructor fails with a SPADE exception rather than ProblemParseException.
        var ex = Assert.Throws<ProblemParseException>(() => ParseGuard.CreateProblem(typeof(API.Problems.P.P_MINCUT.MINCUT), "({1,2"));
        Assert.NotNull(ex.InnerException);
        Assert.Equal("({1,2", ex.Received);
    }

    [Fact]
    public void CreateProblem_ProblemParseException_PassesThroughUnchanged() {
        var ex = Assert.Throws<ProblemParseException>(() => ParseGuard.CreateProblem<SUBSETSUM>("not an instance"));
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void VerifyCertificate_MalformedTextException_BecomesCertificateParseException() {
        var problem = new SUBSETSUM();
        var ex = Assert.Throws<CertificateParseException>(() =>
            ParseGuard.VerifyCertificate(problem, "x", () => throw new FormatException("bad")));
        Assert.IsType<FormatException>(ex.InnerException);
    }

    [Fact]
    public void VerifyCertificate_UnrelatedException_IsNotDisguisedAsParseError() {
        var problem = new SUBSETSUM();
        Assert.Throws<DivideByZeroException>(() =>
            ParseGuard.VerifyCertificate(problem, "x", () => throw new DivideByZeroException()));
    }
}
