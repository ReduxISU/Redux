using API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT.ReduceTo.NPC_SAT3;
using API.Problems.NPComplete.NPC_SAT3;
using API.Interfaces;
using Xunit;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT.Solvers;
using API.Problems.NPComplete.NPC_SAT.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class SAT_Tests {

    // -------------------------------------------------------------------------
    // Instantiation
    // -------------------------------------------------------------------------

    [Fact]
    public void SAT_Default_Instantiation() {
        SAT sat = new SAT();
        Assert.Equal("(!x3 | x4 | !x2 | x1 | x2) & (!x4 | !x1) & (x4 | x3 | !x1)", sat.defaultInstance);
        Assert.Equal(sat.defaultInstance, sat.instance);
    }

    [Fact]
    public void SAT_Instance_Format_Described() {
        SAT sat = new SAT();
        Assert.NotNull(sat.instanceFormat);
        Assert.NotEmpty(sat.instanceFormat);
        Assert.Contains("Boolean formula", sat.instanceFormat);
        Assert.Contains("&", sat.instanceFormat);
        Assert.Contains("|", sat.instanceFormat);
    }

    [Fact]
    public void SAT_Certificate_Format_Described() {
        SAT sat = new SAT();
        Assert.NotNull(sat.certificateFormat);
        Assert.NotEmpty(sat.certificateFormat);
        Assert.Contains("True", sat.certificateFormat);
        Assert.Contains("False", sat.certificateFormat);
        Assert.Contains(":", sat.certificateFormat);
    }

    [Fact]
    public void SAT_Custom_Instance() {
        string instance = "(x1 | x2) & (!x1 | !x2) & (x1 | !x2)";
        SAT sat = new SAT(instance);
        Assert.Equal(instance, sat.instance);
    }

    // -------------------------------------------------------------------------
    // Parsing
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | x2) & (!x1 | !x2) & (x1 | !x2)", 3)]
    [InlineData("(x1 | x2 | x3) & (!x1 | x2)", 2)]
    [InlineData("(!x3 | x4 | !x2 | x1 | x2) & (!x4 | !x1) & (x4 | x3 | !x1)", 3)]
    public void SAT_Parses_Correct_Clause_Count(string instance, int expectedCount) {
        SAT sat = new SAT(instance);
        Assert.Equal(expectedCount, sat.clauses.Count);
    }

    [Theory]
    [InlineData("(x1 | x2 | x3)", new[] { "x1", "x2", "x3" })]
    [InlineData("(!x1 | !x2)", new[] { "!x1", "!x2" })]
    public void SAT_Parses_Correct_Literals_In_Clause(string instance, string[] expectedLiterals) {
        SAT sat = new SAT(instance);
        Assert.Equal(expectedLiterals, sat.clauses[0].ToArray());
    }

    // -------------------------------------------------------------------------
    // Verifier — valid certificates
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | x2) & (!x1 | x2)", "(x1:False,x2:True)")]       // x2=T satisfies both
    [InlineData("(x1 | x2 | x3) & (!x1 | x2)", "(x1:False,x2:True,x3:False)")]
    [InlineData("(!x3 | x4 | !x2 | x1 | x2) & (!x4 | !x1) & (x4 | x3 | !x1)",
                "(x3:False,x4:True,x2:False,x1:False)")]
    public void SAT_Verifier_Accepts_Valid_Certificate(string instance, string certificate) {
        SAT sat = new SAT(instance);
        SATVerifier verifier = new SATVerifier();
        Assert.True(verifier.verify(sat, certificate));
    }

    // -------------------------------------------------------------------------
    // Verifier — invalid certificates
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | x2) & (!x1 | !x2)", "(x1:True,x2:True)")]   // fails clause 2: (!T|!T)=F
    [InlineData("(x1 | x2) & (!x1 | !x2)", "(x1:False,x2:False)")] // fails clause 1: (F|F)=F
    public void SAT_Verifier_Rejects_Invalid_Certificate(string instance, string certificate) {
        SAT sat = new SAT(instance);
        SATVerifier verifier = new SATVerifier();
        Assert.False(verifier.verify(sat, certificate));
    }

    // -------------------------------------------------------------------------
    // Solver
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | x2 | x3)")]
    [InlineData("(!x1 | x2)")]
    [InlineData("(x1 | !x2 | x3 | !x4)")]
    public void SAT_Solver_Finds_Valid_Solution_For_Single_Clause(string instance) {
        SAT sat = new SAT(instance);
        SATBruteForceSolver solver = new SATBruteForceSolver();
        SATVerifier verifier = new SATVerifier();
        string certificate = solver.solve(instance);
        Assert.NotEqual("No solution exists", certificate);
        Assert.True(verifier.verify(sat, certificate));
    }

    // Regression: the old solver advanced the assignment before checking it, so the
    // all-false assignment was never tried correctly and these were reported unsatisfiable.
    [Theory]
    [InlineData("(!x1)")]
    [InlineData("(!x1) & (!x2)")]
    [InlineData("(!a | !b) & (!b | !c) & (!a)")]
    public void SAT_Solver_Finds_All_False_Assignment(string instance) {
        string certificate = new SATBruteForceSolver().solve(instance);
        Assert.NotEqual("No solution exists", certificate);
        Assert.True(new SATVerifier().verify(new SAT(instance), certificate));
    }

    [Theory]
    [InlineData("(x1) & (!x1)")]
    [InlineData("(a | b) & (!a | b) & (a | !b) & (!a | !b)")]
    public void SAT_Solver_Reports_Unsatisfiable(string instance) {
        Assert.Equal("No solution exists", new SATBruteForceSolver().solve(instance));
    }

    [Fact]
    public void SAT_Solver_Malformed_Instance_Throws_ProblemParseException() {
        Assert.Throws<ProblemParseException>(() => new SATBruteForceSolver().solve("(x1 | x2"));
    }

    [Fact]
    public void SAT_Solver_Certificate_Is_Valid_For_Multi_Clause_Instance() {
        // Satisfiable: {x1=True, x2=False} satisfies all three clauses.
        string instance = "(x1 | x2) & (!x1 | !x2) & (x1 | !x2)";
        SAT sat = new SAT(instance);
        SATBruteForceSolver solver = new SATBruteForceSolver();
        SATVerifier verifier = new SATVerifier();
        string certificate = solver.solve(instance);
        Assert.NotEqual("No solution exists", certificate);
        Assert.True(verifier.verify(sat, certificate));
    }

    // -------------------------------------------------------------------------
    // Instance parsing (shared CnfParser)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | x2", "expected '|' or ')'")]
    [InlineData("(x1 | x2) | (x3)", "'|' cannot join")]
    [InlineData("!(x1 & x2)", "negating a parenthesized expression")]
    [InlineData("", "instance is empty")]
    public void SAT_Rejects_Malformed_Instance(string instance, string messageFragment) {
        var ex = Assert.Throws<ProblemParseException>(() => new SAT(instance));
        Assert.Equal("SAT", ex.ProblemName);
        Assert.Contains(messageFragment, ex.Message);
    }

    [Fact]
    public void SAT_Accepts_Clauses_Longer_Than_Three() {
        SAT sat = new SAT("(a | b | c | d | !e) & f");
        Assert.Equal(new[] { 5, 1 }, sat.clauses.Select(c => c.Count));
    }

    // The reductions build instance strings by hand; they must stay within the grammar.
    [Fact]
    public void SAT_To_SAT3_Output_Instance_Reparses() {
        var reduction = new KarpSATToSAT3();
        SAT3 reparsed = new SAT3(reduction.reductionTo.instance);
        Assert.Equal(reduction.reductionTo.clauses, reparsed.clauses);
    }

    [Fact]
    public void GRAPHCOLORING_To_SAT_Output_Instance_Reparses() {
        var reduction = new KarpReduceSAT();
        SAT reparsed = new SAT(reduction.reductionTo.instance);
        Assert.Equal(reduction.reductionTo.clauses, reparsed.clauses);
    }
}
