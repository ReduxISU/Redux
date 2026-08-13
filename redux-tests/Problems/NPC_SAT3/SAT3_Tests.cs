using Xunit;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_SAT3.Solvers;
using API.Problems.NPComplete.NPC_SAT3.Verifiers;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_CLIQUE.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class SAT3_Tests {

    // -------------------------------------------------------------------------
    // Instantiation
    // -------------------------------------------------------------------------

    [Fact]
    public void SAT3_Default_Instantiation() {
        SAT3 sat3 = new SAT3();
        Assert.Equal("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)", sat3.defaultInstance);
        Assert.Equal(sat3.defaultInstance, sat3.instance);
    }

    [Fact]
    public void SAT3_Custom_Instance() {
        string instance = "(x1 | x2 | x3) & (!x1 | x2 | x3) & (x1 | !x2 | x3)";
        SAT3 sat3 = new SAT3(instance);
        Assert.Equal(instance, sat3.instance);
    }

    // -------------------------------------------------------------------------
    // Parsing
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | x2 | x3) & (!x1 | x2 | x3) & (x1 | !x2 | x3)", 3)]
    [InlineData("(x1 | x2 | x3) & (!x1 | !x2 | !x3)", 2)]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)", 3)]
    public void SAT3_Parses_Correct_Clause_Count(string instance, int expectedCount) {
        SAT3 sat3 = new SAT3(instance);
        Assert.Equal(expectedCount, sat3.clauses.Count);
    }

    [Theory]
    [InlineData("(x1 | x2 | x3) & (!x1 | x2 | x3) & (x1 | !x2 | x3)", 3)]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)", 3)]
    public void SAT3_Each_Clause_Has_Three_Literals(string instance, int expectedLiteralsPerClause) {
        SAT3 sat3 = new SAT3(instance);
        foreach (var clause in sat3.clauses) {
            Assert.Equal(expectedLiteralsPerClause, clause.Count);
        }
    }

    // -------------------------------------------------------------------------
    // Solver — satisfiable instances
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)")]  // default instance
    [InlineData("(x1 | x2 | x3) & (!x1 | x2 | x3) & (x1 | !x2 | x3)")]
    [InlineData("(!x1 | !x2 | !x3) & (x1 | x2 | x3) & (x1 | !x2 | x3)")]
    public void SAT3_Solver_Finds_Solution_For_Satisfiable_Instance(string instance) {
        SAT3 sat3 = new SAT3(instance);
        Sat3BacktrackingSolver solver = new Sat3BacktrackingSolver();
        string result = solver.solve(sat3);
        Assert.NotEqual("No Solution", result);
    }

    // -------------------------------------------------------------------------
    // Solver — unsatisfiable instance
    // -------------------------------------------------------------------------

    [Fact]
    public void SAT3_Solver_Returns_NoSolution_For_UNSAT_Instance() {
        // All 8 possible 3-literal clauses over x1, x2, x3 — negates every
        // possible truth assignment, so the formula is trivially UNSAT.
        string instance = "(x1 | x2 | x3) & (!x1 | !x2 | x3) & (x1 | !x2 | !x3) & (!x1 | x2 | !x3)" +
                          " & (!x1 | !x2 | !x3) & (x1 | x2 | !x3) & (!x1 | x2 | x3) & (x1 | !x2 | x3)";
        SAT3 sat3 = new SAT3(instance);
        Sat3BacktrackingSolver solver = new Sat3BacktrackingSolver();
        string result = solver.solve(sat3);
        Assert.Equal("No Solution", result);
    }

    // -------------------------------------------------------------------------
    // Verifier — valid certificates
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)", "(x1:True,x2:True,x3:False)")]
    [InlineData("(x1 | x2 | x3) & (!x1 | x2 | x3)", "(x1:False,x2:True,x3:False)")]
    [InlineData("(!x1 | !x2 | !x3) & (x1 | x2 | x3)", "(x1:True,x2:False,x3:False)")]
    public void SAT3_Verifier_Accepts_Valid_Certificate(string instance, string certificate) {
        SAT3 sat3 = new SAT3(instance);
        SAT3Verifier verifier = new SAT3Verifier();
        Assert.True(verifier.verify(sat3, certificate));
    }

    // -------------------------------------------------------------------------
    // Verifier — invalid certificates
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | x2 | x3) & (!x1 | !x2 | !x3)", "(x1:True,x2:True,x3:True)")]   // fails clause 2
    [InlineData("(x1 | x2 | x3) & (!x1 | !x2 | !x3)", "(x1:False,x2:False,x3:False)")] // fails clause 1
    public void SAT3_Verifier_Rejects_Invalid_Certificate(string instance, string certificate) {
        SAT3 sat3 = new SAT3(instance);
        SAT3Verifier verifier = new SAT3Verifier();
        Assert.False(verifier.verify(sat3, certificate));
    }

    // -------------------------------------------------------------------------
    // Solver + Verifier — round-trip
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)")]
    [InlineData("(!x1 | !x2 | !x3) & (x1 | x2 | x3) & (x1 | !x2 | x3)")]
    public void SAT3_Solver_Certificate_Passes_Verifier(string instance) {
        SAT3 sat3 = new SAT3(instance);
        Sat3BacktrackingSolver solver = new Sat3BacktrackingSolver();
        SAT3Verifier verifier = new SAT3Verifier();
        string certificate = solver.solve(sat3);
        Assert.True(verifier.verify(sat3, certificate));
    }

    // -------------------------------------------------------------------------
    // BUG: solver returns a partial certificate (unassigned variables omitted)
    //
    // When all clauses contain x1, setting x1=True satisfies the formula
    // immediately. The backtracking solver stops and returns varStates, which
    // only contains the variables it actually assigned — so x2 and x3 are
    // absent from the certificate string.
    //
    // Fix: in Sat3BacktrackingSolver.solve(), after findSolution() returns,
    // iterate over sat3.literals and add any variable not already in the
    // solution dictionary with a default value of false.
    // -------------------------------------------------------------------------

    [Fact]
    public void SAT3_Solver_Certificate_Contains_All_Variables() {
        // Every clause contains x1, so x1=True satisfies the formula without
        // the solver ever needing to assign x2 or x3. The returned certificate
        // should still include all three variables.
        string instance = "(x1 | x2 | x3) & (x1 | !x2 | x3) & (x1 | x2 | !x3)";
        SAT3 sat3 = new SAT3(instance);
        Sat3BacktrackingSolver solver = new Sat3BacktrackingSolver();
        string certificate = solver.solve(sat3);

        // Each variable name must appear in the certificate
        Assert.Contains("x1:", certificate);
        Assert.Contains("x2:", certificate);
        Assert.Contains("x3:", certificate);
    }

    // -------------------------------------------------------------------------
    // SAT3 → CLIQUE reduction (Sipser)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)", 3, 9)]
    [InlineData("(x1 | x2 | x3) & (!x1 | !x2 | !x3)", 2, 6)]
    [InlineData("(x1 | x2 | x3) & (!x1 | x2 | x3) & (x1 | !x2 | x3) & (x1 | x2 | !x3)", 4, 12)]
    public void SAT3_To_CLIQUE_Reduction_Structure(string sat3Instance, int expectedK, int expectedNodes) {
        // K must equal the number of SAT3 clauses; one node per literal per clause.
        SAT3 sat3 = new SAT3(sat3Instance);
        SipserReduceToCliqueStandard reduction = new SipserReduceToCliqueStandard(sat3);
        CLIQUE clique = reduction.reductionTo;
        Assert.Equal(expectedK, clique.K);
        Assert.Equal(expectedNodes, clique.nodes.Count);
    }

    [Theory]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)")]
    [InlineData("(x1 | x2 | x3) & (!x1 | !x2 | !x3) & (x1 | !x2 | x3)")]
    public void SAT3_To_CLIQUE_Reduction_SAT3_Solution_Maps_To_Valid_Clique_Certificate(string sat3Instance) {
        // A valid SAT3 assignment must map to a valid k-clique certificate.
        SAT3 sat3 = new SAT3(sat3Instance);
        SipserReduceToCliqueStandard reduction = new SipserReduceToCliqueStandard(sat3);
        CLIQUE clique = reduction.reductionTo;

        Sat3BacktrackingSolver solver = new Sat3BacktrackingSolver();
        string sat3Solution = solver.solve(sat3);
        Assert.NotEqual("No Solution", sat3Solution);

        string cliqueCertificate = reduction.mapSolutions(sat3Solution);

        CliqueVerifier cliqueVerifier = new CliqueVerifier();
        Assert.True(cliqueVerifier.verify(clique, cliqueCertificate));
    }

    [Fact]
    public void SAT3_To_CLIQUE_Reduction_MalformedCertificate_Throws_ReductionInputException() {
        // Regression: a CLIQUE-shaped certificate (items missing the ':' separator)
        // once threw IndexOutOfRangeException from Split(":"), surfacing as an
        // opaque HTTP 500. It must now throw the typed ReductionInputException so
        // the controller can return a 400 with a format hint.
        SAT3 sat3 = new SAT3("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)");
        SipserReduceToCliqueStandard reduction = new SipserReduceToCliqueStandard(sat3);
        Assert.Throws<API.Interfaces.ReductionInputException>(
            () => reduction.mapSolutions("{x1_0,x2_1,!x3_3}"));
    }
}
