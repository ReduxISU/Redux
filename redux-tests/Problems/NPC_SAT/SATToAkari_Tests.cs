using Xunit;
using API.Interfaces;
using API.Problems.NPComplete.NPC_AKARI;
using API.Problems.NPComplete.NPC_AKARI.Solvers;
using API.Problems.NPComplete.NPC_AKARI.Verifiers;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT.ReduceTo.NPC_AKARI;
using API.Problems.NPComplete.NPC_SAT.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

// Tests for SATToAkariReduction: the produced grid, the forward map (assignment -> bulbs), the backward map
// (bulbs -> assignment), and that the grid is solvable exactly when the formula is satisfiable.
public class SATToAkari_Tests {

    // (x1) by hand: x1's chain is h_0 = columns 1-3, junction at 4, h_1 = columns 5-6; the occurrence column is 3.
    private const string SingleLiteralGrid =
        "((#,#,#,#,#,#,#,#)," +
        "(#,_,_,_,1,_,_,#)," +
        "(#,#,#,1,#,#,#,#)," +
        "(#,#,#,_,#,#,#,#)," +
        "(#,#,#,_,#,#,#,#)," +
        "(#,#,#,1,#,#,#,#)," +
        "(#,#,#,#,#,#,#,#)," +
        "(#,#,#,#,#,#,#,#)," +
        "(#,#,#,#,#,#,#,#))";

    // Small formulas with known answers. Covers unit clauses, a tautological clause (x | !x), repeated literals,
    // clauses longer than three, and both satisfiable and unsatisfiable formulas.
    public static TheoryData<string, bool> Formulas => new() {
        { "(x1)", true },
        { "(!x1)", true },
        { "(x1) & (!x1)", false },
        { "(x1 | !x1)", true },
        { "(x1 | x1) & (!x1 | !x1)", false },
        { "(x1 | x2) & (!x1 | x2) & (x1 | !x2) & (!x1 | !x2)", false },
        { "(x1 | x2) & (!x1 | x2) & (x1 | !x2)", true },
        { "(x1 | x2 | x3) & (!x1) & (!x2) & (!x3)", false },
        { "(x1 | x2 | x3) & (!x1) & (!x2)", true },
        { "(x1 | !x2 | x3 | !x4) & (!x1 | x2) & (!x3 | x4) & (x2 | !x4)", true },
        { "(a | b) & (!a | c) & (!b | !c) & (b | c) & (!a | !b)", true },
    };

    private static bool BruteForceSatisfiable(string formula) {
        var sat = new SAT(formula);
        var variables = sat.literals.Select(l => l.TrimStart('!')).Distinct().ToList();
        var verifier = new SATVerifier();
        for (int mask = 0; mask < 1 << variables.Count; mask++) {
            string assignment = "(" + string.Join(",", variables.Select((v, i) => $"{v}:{((mask >> i & 1) == 1 ? "True" : "False")}")) + ")";
            if (verifier.verify(sat, assignment)) return true;
        }
        return false;
    }

    private static IEnumerable<string> AllAssignments(SAT sat) {
        var variables = sat.literals.Select(l => l.TrimStart('!')).Distinct().ToList();
        for (int mask = 0; mask < 1 << variables.Count; mask++) {
            yield return "(" + string.Join(",", variables.Select((v, i) => $"{v}:{((mask >> i & 1) == 1 ? "True" : "False")}")) + ")";
        }
    }

    #region Produced instance

    [Fact]
    public void SATToAkari_SingleLiteral_ProducesHandBuiltGrid() {
        var reduction = new SATToAkariReduction("(x1)");
        Assert.Equal(SingleLiteralGrid, reduction.reductionTo.instance);
    }

    // The worked example in Documentation/proofs/sat-to-akari.md, section 2.3, with its solution for x1 = True, x2 = False.
    private static readonly string[] ProofExampleRows = {
        "#########################",
        "#___1____1__#___1____1__#",
        "###1####1########1##1####",
        "###_####_########_##_####",
        "###_####_########_##_####",
        "###1_____________2##_####",
        "########_########_##_####",
        "########_########_##_####",
        "########_###########_####",
        "########1___________2####",
        "####################_####",
        "####################_####",
        "#########################",
    };
    private static readonly string[] ProofExampleSolutionRows = {
        "#########################",
        "#__*1___*1_*#*__1*___1*_#",
        "###1####1########1##1####",
        "###_####_########_##*####",
        "###*####_########*##_####",
        "###1____________*2##_####",
        "########_########_##_####",
        "########_########*##_####",
        "########*###########_####",
        "########1__________*2####",
        "####################*####",
        "####################_####",
        "#########################",
    };

    private static string AsGrid(string[] rows) =>
        "(" + string.Join(",", rows.Select(r => "(" + string.Join(",", r.ToCharArray()) + ")")) + ")";

    [Fact]
    public void SATToAkari_ProofExample_MatchesDocumentedGridAndSolution() {
        var reduction = new SATToAkariReduction("(x1 | !x2) & (x1 | x2)");
        Assert.Equal(AsGrid(ProofExampleRows), reduction.reductionTo.instance);
        Assert.Equal(AsGrid(ProofExampleSolutionRows), reduction.mapSolutions("(x1:True,x2:False)"));
    }

    [Fact]
    public void SATToAkari_DefaultInstance_OutputReparses() {
        var reduction = new SATToAkariReduction();
        var reparsed = new AKARI(reduction.reductionTo.instance);
        // 5 clauses -> 4 * 5 + 5 rows; 12 literal occurrences over 4 variables -> 5 * 12 + 2 * 4 + 1 columns.
        Assert.Equal(25, reparsed.Rows);
        Assert.Equal(69, reparsed.Cols);
    }

    [Theory]
    [MemberData(nameof(Formulas))]
    public void SATToAkari_GridSize_IsRowsPerClauseTimesColumnsPerOccurrence(string formula, bool _) {
        var sat = new SAT(formula);
        var reduction = new SATToAkariReduction(sat);
        int clauses = sat.clauses.Count;
        int occurrences = sat.literals.Count;
        int variables = sat.literals.Select(l => l.TrimStart('!')).Distinct().Count();
        Assert.Equal(4 * clauses + 5, reduction.reductionTo.Rows);
        Assert.Equal(5 * occurrences + 2 * variables + 1, reduction.reductionTo.Cols);
    }

    [Fact]
    public void SATToAkari_Grid_UsesOnlyClues1And2() {
        var reduction = new SATToAkariReduction();
        Assert.All(reduction.reductionTo.ClueValue, v => Assert.InRange(v, 1, 2));
    }

    [Fact]
    public void SATToAkari_Reduce_IsRepeatable() {
        var reduction = new SATToAkariReduction();
        string first = reduction.reductionTo.instance;
        Assert.Equal(first, reduction.reduce().instance);
        Assert.Equal(first, reduction.reductionTo.instance);
    }

    #endregion

    #region Forward map

    [Fact]
    public void SATToAkari_SingleLiteral_MapsTrueToItsUniqueSolution() {
        var reduction = new SATToAkariReduction("(x1)");
        string expected =
            "((#,#,#,#,#,#,#,#),(#,_,_,*,1,_,*,#),(#,#,#,1,#,#,#,#),(#,#,#,_,#,#,#,#),(#,#,#,*,#,#,#,#)," +
            "(#,#,#,1,#,#,#,#),(#,#,#,#,#,#,#,#),(#,#,#,#,#,#,#,#),(#,#,#,#,#,#,#,#))";
        Assert.Equal(expected, reduction.mapSolutions("(x1:True)"));
        Assert.False(new AkariVerifier().verify(reduction.reductionTo, reduction.mapSolutions("(x1:False)")));
    }

    [Theory]
    [MemberData(nameof(Formulas))]
    public void SATToAkari_MapSolutions_IsValidExactlyForSatisfyingAssignments(string formula, bool _) {
        var sat = new SAT(formula);
        var reduction = new SATToAkariReduction(sat);
        var satVerifier = new SATVerifier();
        var akariVerifier = new AkariVerifier();
        foreach (string assignment in AllAssignments(sat)) {
            bool satisfies = satVerifier.verify(sat, assignment);
            Assert.Equal(satisfies, akariVerifier.verify(reduction.reductionTo, reduction.mapSolutions(assignment)));
        }
    }

    [Fact]
    public void SATToAkari_DefaultInstance_MapsKnownSolution() {
        var reduction = new SATToAkariReduction();
        // A satisfying assignment of the default SAT instance.
        string mapped = reduction.mapSolutions("(x1:True,x2:False,x3:False,x4:False)");
        Assert.True(new AkariVerifier().verify(reduction.reductionTo, mapped));
    }

    [Fact]
    public void SATToAkari_MapSolutions_AcceptsShortFormsAndEqualsSign() {
        var reduction = new SATToAkariReduction("(x1 | x2) & (!x1)");
        Assert.Equal(reduction.mapSolutions("(x1:False,x2:True)"), reduction.mapSolutions("(x1=F, x2=T)"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("(x1)")]
    [InlineData("(x1:Maybe)")]
    [InlineData("(:True)")]
    public void SATToAkari_MapSolutions_RejectsMalformedAssignments(string certificate) {
        var reduction = new SATToAkariReduction("(x1)");
        Assert.Throws<ReductionInputException>(() => reduction.mapSolutions(certificate));
    }

    #endregion

    #region Backward map and correctness

    [Theory]
    [MemberData(nameof(Formulas))]
    public void SATToAkari_GridIsSolvable_ExactlyWhenFormulaIsSatisfiable(string formula, bool satisfiable) {
        Assert.Equal(satisfiable, BruteForceSatisfiable(formula));
        var reduction = new SATToAkariReduction(formula);
        string solution = new AkariConstraintPropagation().solve(reduction.reductionTo);
        Assert.Equal(satisfiable, solution != AkariVerifier.NoSolution);
        if (satisfiable) {
            Assert.True(new AkariVerifier().verify(reduction.reductionTo, solution));
        }
    }

    [Theory]
    [MemberData(nameof(Formulas))]
    public void SATToAkari_MapBack_OfSolverSolution_SatisfiesFormula(string formula, bool satisfiable) {
        if (!satisfiable) return;
        var reduction = new SATToAkariReduction(formula);
        string solution = new AkariConstraintPropagation().solve(reduction.reductionTo);
        string assignment = ((IReduction)reduction).mapSolutionBack(solution)!;
        Assert.True(new SATVerifier().verify(reduction.reductionFrom, assignment));
    }

    [Theory]
    [MemberData(nameof(Formulas))]
    public void SATToAkari_RoundTrip_ReturnsTheAssignment(string formula, bool _) {
        var sat = new SAT(formula);
        var reduction = new SATToAkariReduction(sat);
        foreach (string assignment in AllAssignments(sat)) {
            if (!new SATVerifier().verify(sat, assignment)) continue;
            string back = ((IReduction)reduction).mapSolutionBack(reduction.mapSolutions(assignment))!;
            Assert.Equal(assignment, back);
        }
    }

    [Fact]
    public void SATToAkari_MapBack_ReportsNoSolutionForNoSolution() {
        var reduction = new SATToAkariReduction("(x1) & (!x1)");
        Assert.Equal("{}", ((IReduction)reduction).mapSolutionBack(AkariVerifier.NoSolution));
    }

    [Fact]
    public void SATToAkari_MapBack_RejectsGridOfAnotherInstance() {
        var reduction = new SATToAkariReduction("(x1)");
        Assert.ThrowsAny<FormatException>(() => ((IReduction)reduction).mapSolutionBack("((_,*))"));
    }

    [Fact]
    public void SATToAkari_HasBackwardMap() {
        Assert.True(((IReduction)new SATToAkariReduction()).hasBackwardMap());
    }

    #endregion
}
