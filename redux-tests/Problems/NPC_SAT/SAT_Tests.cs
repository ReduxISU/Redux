using Xunit;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT.Solvers;
using API.Problems.NPComplete.NPC_SAT.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class SAT_Tests
{

    // -------------------------------------------------------------------------
    // Instantiation
    // -------------------------------------------------------------------------

    [Fact]
    public void SAT_Default_Instantiation()
    {
        SAT sat = new SAT();
        Assert.Equal("(!x3 | x4 | !x2 | x1 | x2) & (!x4 | !x1) & (x4 | x3 | !x1)", sat.defaultInstance);
        Assert.Equal(sat.defaultInstance, sat.instance);
    }

    [Fact]
    public void SAT_Custom_Instance()
    {
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
    public void SAT_Parses_Correct_Clause_Count(string instance, int expectedCount)
    {
        SAT sat = new SAT(instance);
        Assert.Equal(expectedCount, sat.clauses.Count);
    }

    [Theory]
    [InlineData("(x1 | x2 | x3)", new[] { "x1", "x2", "x3" })]
    [InlineData("(!x1 | !x2)", new[] { "!x1", "!x2" })]
    public void SAT_Parses_Correct_Literals_In_Clause(string instance, string[] expectedLiterals)
    {
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
    public void SAT_Verifier_Accepts_Valid_Certificate(string instance, string certificate)
    {
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
    public void SAT_Verifier_Rejects_Invalid_Certificate(string instance, string certificate)
    {
        SAT sat = new SAT(instance);
        SATVerifier verifier = new SATVerifier();
        Assert.False(verifier.verify(sat, certificate));
    }

    // -------------------------------------------------------------------------
    // Solver — single-clause instances (unaffected by the increment bug since
    // there is only one clause, so increment fires exactly once per combination)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | x2 | x3)")]
    [InlineData("(!x1 | x2)")]
    [InlineData("(x1 | !x2 | x3 | !x4)")]
    public void SAT_Solver_Finds_Valid_Solution_For_Single_Clause(string instance)
    {
        SAT sat = new SAT(instance);
        SATBruteForceSolver solver = new SATBruteForceSolver();
        SATVerifier verifier = new SATVerifier();
        string certificate = solver.solve(instance);
        Assert.NotEqual("No solution exists", certificate);
        Assert.True(verifier.verify(sat, certificate));
    }

    // -------------------------------------------------------------------------
    // BUG: increment called inside the clause loop
    //
    // SATBruteForceSolver.solve() calls increment(literalDict) once per clause
    // in the inner foreach, instead of once per outer combination. For a formula
    // with c clauses and n variables, this means the assignment advances c times
    // per outer iteration rather than once, causing the solver to evaluate each
    // clause against a DIFFERENT assignment and return a certificate that was
    // never actually checked as a whole.
    //
    // Example: (x1 | x2) & (!x1 | !x2) & (x1 | !x2)
    //   Valid solution: {x1=T, x2=F}
    //   Bug causes solver to return: (x1:True, x2:True)
    //   (x1:True, x2:True) fails clause 2: (!x1 | !x2) = (F | F) = False
    //
    // Fix: move `literalDict = increment(literalDict)` to BEFORE the inner
    // foreach loop, so the assignment advances once per combination.
    // -------------------------------------------------------------------------

    [Fact]
    public void SAT_Solver_Certificate_Is_Valid_For_Multi_Clause_Instance()
    {
        // Satisfiable: {x1=True, x2=False} satisfies all three clauses.
        // The increment bug causes the solver to return (x1:True, x2:True),
        // which fails clause 2 (!x1 | !x2) = (F | F) = False.
        string instance = "(x1 | x2) & (!x1 | !x2) & (x1 | !x2)";
        SAT sat = new SAT(instance);
        SATBruteForceSolver solver = new SATBruteForceSolver();
        SATVerifier verifier = new SATVerifier();
        string certificate = solver.solve(instance);
        Assert.NotEqual("No solution exists", certificate);
        Assert.True(verifier.verify(sat, certificate));
    }
}
