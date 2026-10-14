using Xunit;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01.Solvers;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01.Verifiers;

namespace redux_tests;

#pragma warning disable CS1591

public class INTPROGRAMMING01_Tests {
    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void INTPROGRAMMING01_Instance_Format_Described() {
        INTPROGRAMMING01 problem = new INTPROGRAMMING01();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("<=", problem.instanceFormat);
    }

    [Fact]
    public void INTPROGRAMMING01_Certificate_Format_Described() {
        INTPROGRAMMING01 problem = new INTPROGRAMMING01();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("bits", problem.certificateFormat);
    }

    [Fact]
    public void INTPROGRAMMING01_Certificate_Format_Example_Is_Actually_Valid() {
        // The "Example: (0 0 0)" quoted in certificateFormat must be a real,
        // verifiable certificate for defaultInstance — not just descriptive prose.
        INTPROGRAMMING01 problem = new INTPROGRAMMING01();
        GenericVerifier01INTP verifier = new GenericVerifier01INTP();
        Assert.True(verifier.verify(problem, GenericVerifier01INTP.CertificateExample));
    }

    [Theory]
    [InlineData("(2 0 2)")]
    [InlineData("(-1 0 0)")]
    public void INTPROGRAMMING01_Verifier_Rejects_NonBinary_Assignments(string certificate) {
        INTPROGRAMMING01 problem = new INTPROGRAMMING01();
        GenericVerifier01INTP verifier = new GenericVerifier01INTP();

        Assert.False(verifier.verify(problem, certificate));
    }

    // -------------------------------------------------------------------------
    // IntegerProgrammingBruteForce
    // -------------------------------------------------------------------------

    [Fact]
    public void INTPROGRAMMING01_BruteForce_Default_Instance_Needs_NonTrivial_Assignment() {
        // The default must not be satisfied by all zeros (or all ones): only x = (0 1 0) is found
        // after the solver has rejected (0 0 0) and (1 0 0).
        INTPROGRAMMING01 problem = new INTPROGRAMMING01();
        GenericVerifier01INTP verifier = new GenericVerifier01INTP();

        Assert.False(verifier.verify(problem, "(0 0 0)"));
        Assert.False(verifier.verify(problem, "(1 1 1)"));

        string solution = new IntegerProgrammingBruteForce().solve(problem);

        Assert.Equal("(0 1 0)", solution);
        Assert.True(verifier.verify(problem, solution));
    }

    // -------------------------------------------------------------------------
    // Parsing (#707)
    // -------------------------------------------------------------------------

    [Fact]
    public void INTPROGRAMMING01_Parser_Tolerates_Repeated_Whitespace() {
        INTPROGRAMMING01 problem = new INTPROGRAMMING01("( 1   -1 ),(0  1)<=(  -1   2 )");
        Assert.Equal(new List<int> { 1, -1 }, problem.C[0]);
        Assert.Equal(new List<int> { 0, 1 }, problem.C[1]);
        Assert.Equal(new List<int> { -1, 2 }, problem.d);
    }

    [Theory]
    [InlineData("(1 -1),(0 1)<=(-1)")]          // d shorter than the rows of C
    [InlineData("(1 -1)<=(-1 2)")]              // d longer than the rows of C
    [InlineData("(1 -1),(0)<=(-1 2)")]          // ragged rows
    [InlineData("(1 -1)(0 1)")]                 // no <=
    [InlineData("(1 -1)<=(0)<=(1)")]            // two <=
    [InlineData("(1 x)<=(0)")]                  // not an integer
    [InlineData("(1 -1)<=()")]                  // empty d
    [InlineData("<=(0)")]                       // empty C
    [InlineData("")]
    public void INTPROGRAMMING01_Parser_Rejects_Malformed_Instances(string instance) {
        Assert.Throws<API.Interfaces.ProblemParseException>(() => new INTPROGRAMMING01(instance));
    }

    [Theory]
    [InlineData("")]
    [InlineData("(0 x 0)")]
    [InlineData("(0 0)")]
    [InlineData("(0 1 0 0)")]
    public void INTPROGRAMMING01_Verifier_Returns_False_For_Malformed_Certificates(string certificate) {
        Assert.False(new GenericVerifier01INTP().verify(new INTPROGRAMMING01(), certificate));
    }

    [Fact]
    public void INTPROGRAMMING01_Verifier_Tolerates_Repeated_Whitespace_In_Certificate() {
        Assert.True(new GenericVerifier01INTP().verify(new INTPROGRAMMING01(), "(0   1  0)"));
    }

    [Fact]
    public void INTPROGRAMMING01_BruteForce_Returns_Empty_When_No_Assignment_Satisfies() {
        // A single variable x in {0,1} with constraint x <= -1: neither x=0 (Cx=0) nor
        // x=1 (Cx=1) can satisfy <= -1, so the solver must exhaust both candidates
        // (including nextBinary's carry-rollover from [1] back to [0]) and return "()".
        INTPROGRAMMING01 problem = new INTPROGRAMMING01("(1)<=(-1)");
        IntegerProgrammingBruteForce solver = new IntegerProgrammingBruteForce();

        string solution = solver.solve(problem);

        Assert.Equal("()", solution);
    }

    [Fact]
    public void INTPROGRAMMING01_BruteForce_Finds_Assignment_Requiring_Multiple_Candidates() {
        // Two variables, x0 - x1 <= -1. x=(0,0) gives 0 (fails), x=(1,0) gives 1 (fails),
        // and only x=(0,1) gives -1 (satisfies). This forces the solver through both a
        // plain increment and a carry-rollover of nextBinary before succeeding.
        INTPROGRAMMING01 problem = new INTPROGRAMMING01("(1 -1)<=(-1)");
        IntegerProgrammingBruteForce solver = new IntegerProgrammingBruteForce();

        string solution = solver.solve(problem);

        Assert.Equal("(0 1)", solution);
        Assert.True(new GenericVerifier01INTP().verify(problem, solution));
    }

    [Fact]
    public void INTPROGRAMMING01_BruteForce_Single_Always_Satisfiable_Constraint() {
        // A single variable with a constraint that any assignment satisfies (x <= 5):
        // the very first candidate x=(0) already works.
        INTPROGRAMMING01 problem = new INTPROGRAMMING01("(1)<=(5)");
        IntegerProgrammingBruteForce solver = new IntegerProgrammingBruteForce();

        string solution = solver.solve(problem);

        Assert.Equal("(0)", solution);
        Assert.True(new GenericVerifier01INTP().verify(problem, solution));
    }

    [Fact]
    public void INTPROGRAMMING01_BruteForce_Multiple_Constraints_Output_Passes_Verifier() {
        string[] instances = {
            INTPROGRAMMING01._defaultInstance,
            "(1 -1)<=(-1)",
            "(1)<=(5)",
            "(-1 1 -1),(0 0 -1),(-1 -1 1)<=(0 0 0)", // the former default; all zeros satisfies it
        };
        foreach (string inst in instances) {
            INTPROGRAMMING01 problem = new INTPROGRAMMING01(inst);
            IntegerProgrammingBruteForce solver = new IntegerProgrammingBruteForce();
            string solution = solver.solve(problem);
            if (solution == "()") continue; // no satisfying assignment exists; nothing to verify
            Assert.True(new GenericVerifier01INTP().verify(problem, solution), $"Solver output failed verifier for: {inst}");
        }
    }
}
