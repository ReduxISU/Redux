using Xunit;
using API.Problems.NPComplete.NPC_EDITDISTANCE;
using API.Problems.NPComplete.NPC_EDITDISTANCE.Verifiers;
using API.Problems.NPComplete.NPC_EDITDISTANCE.Solvers;
using API.Interfaces;

namespace redux_tests;
#pragma warning disable CS1591

public class EDITDISTANCE_Tests {
    [Fact]
    public void EDITDISTANCE_Default_Instantiation() {
        var problem = new EDITDISTANCE();
        Assert.Equal("(horse, ros)", problem.instance);
        Assert.Equal("(horse, ros)", problem.defaultInstance);
    }

    [Fact]
    public void EDITDISTANCE_Custom_Instantiation() {
        var problem = new EDITDISTANCE("(intention, execution, 5)");
        Assert.Equal("(intention, execution, 5)", problem.instance);

    }

    [Theory] //tests verifier
    [InlineData("(horse, ros, 3)", "3")]
    [InlineData("(intention, execution, 5)", "5")]
    [InlineData("(cute, cute, 0)", "0")]
    [InlineData("(a, b, 1)", "1")]
    [InlineData("(cut, cuts, 1)", "1")]
    [InlineData("(cuts, cut, 1)", "1")]
    [InlineData("(abc, def, 3)", "3")]
    [InlineData("(cat, cut, 1)", "1")]
    [InlineData("(a, a, 0)", "0")]
    public void EDITDISTANCE_verifier(string instance, string certificate) {
        var problem = new EDITDISTANCE(instance);
        var verifier = new EditDistanceVerifier();
        Assert.True(verifier.verify(problem, certificate));
    }


    [Theory] //tests solver
    [InlineData("(horse, ros, 3)", "3")]
    [InlineData("(intention, execution, 5)", "5")]
    [InlineData("(cute, cute, 0)", "0")]
    [InlineData("(a, b, 1)", "1")]
    [InlineData("(cut, cuts, 1)", "1")]
    [InlineData("(cuts, cut, 1)", "1")]
    [InlineData("(abc, def, 3)", "3")]
    [InlineData("(cat, cut, 1)", "1")]
    [InlineData("(a, a, 0)", "0")]
    public void EDITDISTANCE_solver(string instance, string certificate) {
        var problem = new EDITDISTANCE(instance);
        var solver = new EditDistanceDPSolver();
        string solvedString = solver.solve(problem);
        Assert.Equal(certificate, solvedString);
    }

    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void EDITDISTANCE_Instance_Format_Described() {
        var problem = new EDITDISTANCE();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("(x, y)", problem.instanceFormat);
    }

    [Fact]
    public void EDITDISTANCE_Certificate_Format_Described() {
        var problem = new EDITDISTANCE();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("edit operations", problem.certificateFormat);
    }

    [Fact]
    public void EDITDISTANCE_Certificate_Format_Example_Is_Actually_Valid() {
        // The "Example: 3" quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        var problem = new EDITDISTANCE();
        var verifier = new EditDistanceVerifier();
        Assert.True(verifier.verify(problem, EditDistanceVerifier.CertificateExample));
    }

    // -------------------------------------------------------------------------
    // Parsing (#706)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(horse, ros)", "horse", "ros")]
    [InlineData("horse,ros", "horse", "ros")]
    [InlineData("(horse, ros, 3)", "horse", "ros")]
    [InlineData("( a ,  b )", "a", "b")]
    [InlineData("(\"a,b\", c)", "a,b", "c")]
    [InlineData("(a, \"x, y\")", "a", "x, y")]
    [InlineData("(\"a,b\", \"c,d\", 2)", "a,b", "c,d")]
    [InlineData("(\"say \\\"hi\\\"\", b)", "say \"hi\"", "b")]
    [InlineData("(\"back\\\\slash\", b)", "back\\slash", "b")]
    [InlineData("(\" padded \", b)", " padded ", "b")]
    [InlineData("(, ros)", "", "ros")]
    [InlineData("(\"\", ros)", "", "ros")]
    public void EDITDISTANCE_Parses_Strings(string instance, string x, string y) {
        var problem = new EDITDISTANCE(instance);
        Assert.Equal(x, problem.sourceString);
        Assert.Equal(y, problem.targetString);
    }

    [Theory]
    [InlineData("")]
    [InlineData("()")]
    [InlineData("(horse)")]
    [InlineData("horse")]
    [InlineData("(a, b, c)")]           // unquoted comma-containing string: third field is not an integer
    [InlineData("(a, b, 1, 2)")]
    [InlineData("(\"a, b)")]            // unterminated quote
    [InlineData("(\"a\"x, b)")]         // text after closing quote
    [InlineData("(a\"b, c)")]           // quote inside an unquoted string
    public void EDITDISTANCE_Rejects_Malformed_Instances(string instance) {
        Assert.Throws<ProblemParseException>(() => new EDITDISTANCE(instance));
    }

    [Fact]
    public void EDITDISTANCE_Solves_And_Verifies_String_With_Comma() {
        var problem = new EDITDISTANCE("(\"a,b\", \"a;b\")");
        Assert.Equal("1", new EditDistanceDPSolver().solve(problem));
        Assert.True(new EditDistanceVerifier().verify(problem, "1"));
        Assert.False(new EditDistanceVerifier().verify(problem, "2"));
    }
}
