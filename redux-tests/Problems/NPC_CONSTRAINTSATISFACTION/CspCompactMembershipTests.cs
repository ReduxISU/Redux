using API.Interfaces;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Solvers;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Exercise exactly what the webpage sends: remove spaces from otherwise valid
// constraints, then run every search strategy and independently verify the result.
public class CONSTRAINTSATISFACTION_CompactMembership_Tests {
    private static ISolver<CONSTRAINTSATISFACTION>[] Solvers() => [
        new ConstraintSatisfactionBruteForce(), new ConstraintSatisfactionBacktracking(),
        new ConstraintSatisfactionIntelligent(), new ConstraintSatisfactionIntelligent { ForwardChecking = false }
    ];

    [Theory]
    [InlineData("x in {2,5}", true)]
    [InlineData("x not in {2,5}", false)]
    [InlineData("x in {}", false)]
    [InlineData("x not in {}", true)]
    [InlineData("x in [2,5]", true)]
    [InlineData("x in (2,5)", false)]
    [InlineData("x in [2,5)", true)]
    [InlineData("x in (2,5]", false)]
    [InlineData("x not in [2,5]", false)]
    [InlineData("x not in (2,5)", true)]
    [InlineData("x not in [2,5)", false)]
    [InlineData("x not in (2,5]", true)]
    [InlineData("x IN {2}", true)]
    [InlineData("x NoT IN (2,5)", true)]
    [InlineData("x + y in {7, x*y}", true)]
    [InlineData("x + y not in [1,6]", true)]
    [InlineData("(x+y) in [7,8)", true)]
    [InlineData("(x+y) not in [7,8)", false)]
    [InlineData("x/10 in [.2,.5)", true)]
    [InlineData("x/10 not in (.2,.5)", true)]
    [InlineData("-x in {-2}", true)]
    [InlineData("-x not in [-2,-1]", false)]
    [InlineData("x in [y-z,y/z+1)", true)]
    [InlineData("x not in {y-z,z}", false)]
    [InlineData("2 in {2,3}", true)]
    [InlineData("2 not in (2,5)", true)]
    [InlineData(".2 not in (.2,.5)", true)]
    public void SpacedAndWebpageCompactedConstraints_Agree(string expression, bool expected) {
        foreach (string text in new[] { expression, expression.Replace(" ", "") }) {
            var problem = new CONSTRAINTSATISFACTION($"((x,y,z),({{2}},{{5}},{{3}}),{{\"{text}\"}})");
            var verifier = new ConstraintSatisfactionVerifier();
            Assert.Equal(expected, verifier.verify(problem, "(x:2,y:5,z:3)"));
            foreach (var solver in Solvers()) {
                string answer = solver.solve(problem);
                Assert.Equal(expected, answer != "{}");
                if (expected) Assert.True(verifier.verify(problem, answer));
            }
        }
    }

    [Theory]
    [InlineData("xin(2,5)", "(x:3)")]
    [InlineData("xnotin(2,5)", "(x:1)")]
    [InlineData("xin{2,5}", "(x:2)")]
    [InlineData("xnotin{2,5}", "(x:1)")]
    public void WebpageInput_SearchesDomainsAndReturnsVerifiableCertificate(string expression, string expected) {
        var problem = new CONSTRAINTSATISFACTION($"((x),({{1,2,3,4,5}}),{{\"{expression}\"}})");
        foreach (var solver in Solvers()) {
            string answer = solver.solve(problem);
            Assert.Equal(expected, answer);
            Assert.True(new ConstraintSatisfactionVerifier().verify(problem, answer));
        }
    }

    [Fact]
    public void DeclaredNamesContainingKeywords_ArePreserved() {
        // A complete name in a comparison is never split. A name ending in "in"
        // can also precede another membership operator without losing its suffix.
        var problem = new CONSTRAINTSATISFACTION(
            "((x,xnotin,index,within),({2},{3},{4},{5}),{\"xnotin=3\",\"index=4\",\"within=5\",\"xnotinin{3}\",\"withinnotin{2}\",\"xin{xnotin-1}\"})");
        foreach (var solver in Solvers()) {
            string answer = solver.solve(problem);
            Assert.Equal("(x:2, xnotin:3, index:4, within:5)", answer);
            Assert.True(new ConstraintSatisfactionVerifier().verify(problem, answer));
        }
    }

    [Theory]
    [InlineData("xnotin[2,5]")]
    [InlineData("xnotIN{2}")]
    public void AmbiguousCompactedNames_ThrowRatherThanGuess(string expression) {
        // The GUI erased the distinction between "x not in" and "xnot in".
        var problem = new CONSTRAINTSATISFACTION($"((x,xnot),({{2}},{{6}}),{{\"{expression}\"}})");
        Assert.Contains("parenthesize", Assert.Throws<ProblemParseException>(
            () => new ConstraintSatisfactionVerifier().verify(problem, "(x:2,xnot:6)")).Message);
        foreach (var solver in Solvers()) Assert.Throws<ProblemParseException>(() => solver.solve(problem));
    }

    [Theory]
    [InlineData("(x)notin(2,5)", true)]
    [InlineData("(xnot)in[2,5]", false)]
    [InlineData("(xnot)notin[2,5]", true)]
    [InlineData("xNoTiN{2}", false)]
    public void ParenthesizedLeftOperand_ResolvesCompactedNameAmbiguity(string expression, bool expected) {
        var problem = new CONSTRAINTSATISFACTION($"((x,xnot),({{2}},{{6}}),{{\"{expression}\"}})");
        Assert.Equal(expected, new ConstraintSatisfactionVerifier().verify(problem, "(x:2,xnot:6)"));
        foreach (var solver in Solvers()) Assert.Equal(expected, solver.solve(problem) != "{}");
    }

    [Theory]
    [InlineData("unknownnotin(2,5)")]
    [InlineData("Xnotin(2,5)")]
    [InlineData("xnot[2,5]")]
    [InlineData("xnotin[2,]")]
    [InlineData("xnotin{2,}")]
    [InlineData("xnotin[2,5]junk")]
    [InlineData("xnotin[2,5]in{1}")]
    [InlineData("x=yin(2,5)")]
    [InlineData("xin{yin(2,5)}")]
    [InlineData("xin[yin(2,5),5]")]
    public void InvalidCompactConstraints_StillThrow(string expression) {
        var problem = new CONSTRAINTSATISFACTION($"((x,y),({{2}},{{5}}),{{\"{expression}\"}})");
        Assert.Throws<ProblemParseException>(() => new ConstraintSatisfactionVerifier().verify(problem, "(x:2,y:5)"));
        foreach (var solver in Solvers()) Assert.Throws<ProblemParseException>(() => solver.solve(problem));
    }

    [Fact]
    public void CompactMembership_DefersUntilEveryDependencyIsAssigned() {
        var problem = new CONSTRAINTSATISFACTION("((x,y),({2},{5}),{\"xnotin{y-3}\"})");
        var model = new ConstraintSatisfactionModel(problem);
        Assert.True(model.Consistent([2m, null]));
        Assert.False(model.Consistent([2m, 5m]));
    }

    [Fact]
    public void CompactMembership_PreservesDecimalDomainsAndVerifierContract() {
        var problem = new CONSTRAINTSATISFACTION("((x),({.1,.2,.3}),{\"xnotin[.1,.2)\"})");
        var verifier = new ConstraintSatisfactionVerifier();
        Assert.True(verifier.verify(problem, "(x:.2)"));
        Assert.False(verifier.verify(problem, "(x:.1)"));
        Assert.Throws<CertificateParseException>(() => verifier.verify(problem, "(x:.4)"));
        Assert.Throws<CertificateParseException>(() => verifier.verify(problem, "()"));
        foreach (var solver in Solvers()) Assert.True(verifier.verify(problem, solver.solve(problem)));
    }
}
