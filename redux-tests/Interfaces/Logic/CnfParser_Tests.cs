using API.Interfaces.Logic;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class CnfParser_Tests {

    // Renders clauses as e.g. "x1,!x2;x3" so structure can be asserted in one string.
    private static string Shape(CnfFormula f) =>
        string.Join(";", f.Clauses.Select(c => string.Join(",", c.Literals)));

    // -------------------------------------------------------------------------
    // Accepted inputs
    // -------------------------------------------------------------------------

    [Theory]
    // SAT and 3SAT default instances
    [InlineData("(!x3 | x4 | !x2 | x1 | x2) & (!x4 | !x1) & (x4 | x3 | !x1)", "!x3,x4,!x2,x1,x2;!x4,!x1;x4,x3,!x1")]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)", "x1,!x2,x3;!x1,x3,x1;x2,!x3,!x1")]
    // Shapes used by existing tests and reductions
    [InlineData("(x1) & (!x1)", "x1;!x1")]
    [InlineData("( a | b | c ) & ( !a | b | b )", "a,b,c;!a,b,b")]
    [InlineData("(a|b)&(!c|d)", "a,b;!c,d")]
    // Bare clauses that are unambiguous
    [InlineData("x1", "x1")]
    [InlineData("!x1", "!x1")]
    [InlineData("x1 | !x2 | x3", "x1,!x2,x3")]
    [InlineData("x1 & !x2 & (x3 | x4)", "x1;!x2;x3,x4")]
    // Whitespace of any kind, and between '!' and the variable
    [InlineData("\t(x1 |\n x2)\r\n&\t( ! x3 )  ", "x1,x2;!x3")]
    // Identifiers
    [InlineData("(_a | B_2 | longName99)", "_a,B_2,longName99")]
    public void Parse_Accepts(string input, string expectedShape) {
        Assert.Equal(expectedShape, Shape(CnfParser.Parse(input)));
    }

    [Fact]
    public void Parse_CollectsDistinctVariablesInOrder() {
        CnfFormula f = CnfParser.Parse("(b | !a) & (a | c | !b)");
        Assert.Equal(new[] { "b", "a", "c" }, f.Variables);
    }

    [Fact]
    public void Parse_RecordsClausePositions() {
        CnfFormula f = CnfParser.Parse("(a | b) &  c");
        Assert.Equal(new[] { 0, 11 }, f.Clauses.Select(c => c.Position));
    }

    [Theory]
    [InlineData("(!x3 | x4 | !x2 | x1 | x2) & (!x4 | !x1) & (x4 | x3 | !x1)")]
    [InlineData("x1 & !x2 & (x3 | x4)")]
    [InlineData("a | b")]
    public void ToString_RoundTrips(string input) {
        CnfFormula f = CnfParser.Parse(input);
        CnfFormula again = CnfParser.Parse(f.ToString());
        Assert.Equal(Shape(f), Shape(again));
        Assert.Equal(f.ToString(), again.ToString());
    }

    [Fact]
    public void ToString_IsCanonical() {
        Assert.Equal("(x1 | !x2) & (x3)", CnfParser.Parse("(x1|!x2)&x3").ToString());
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(false, false, true)]
    public void Evaluate_ChecksEveryClause(bool a, bool b, bool expected) {
        // (!a | b) is a -> b; the tautological second clause checks that every clause is consulted.
        CnfFormula f = CnfParser.Parse("(!a | b) & (a | !a)");
        Assert.Equal(expected, f.Evaluate(new Dictionary<string, bool> { ["a"] = a, ["b"] = b }));
    }

    // -------------------------------------------------------------------------
    // Rejected inputs (the old string-splitting parser accepted most of these)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("", 0, "empty")]
    [InlineData("   ", 3, "empty")]
    [InlineData("(x1 | x2", 8, "expected '|' or ')'")]
    [InlineData("x1 | x2)", 7, "expected '&' or end of input")]
    [InlineData(")x1(", 0, "expected a literal")]
    [InlineData("()", 0, "empty clause")]
    [InlineData("(x1 | x2) &", 11, "expected a literal")]
    [InlineData("& x1", 0, "expected a literal")]
    [InlineData("(x1 | | x2)", 6, "expected a literal")]
    [InlineData("(x1 x2)", 4, "expected '|' or ')'")]
    [InlineData("!!x1", 1, "double negation")]
    [InlineData("!(x1 | x2)", 1, "negating a parenthesized expression")]
    [InlineData("((x1 | x2))", 1, "nested parentheses")]
    [InlineData("(x1 & x2)", 4, "'&' inside a clause")]
    [InlineData("(x1) | (x2)", 5, "'|' cannot join")]
    [InlineData("x1 | x2 & x3", 0, "must be parenthesized")]
    [InlineData("x1 & x2 | x3", 5, "must be parenthesized")]
    [InlineData("(x-1)", 2, "unexpected character '-'")]
    [InlineData("(x$ | y)", 2, "unexpected character '$'")]
    [InlineData("(1x | y)", 1, "must start with a letter")]
    [InlineData("(x1 ∨ x2)", 4, "unexpected character")]
    public void Parse_Rejects(string input, int position, string messageFragment) {
        var ex = Assert.Throws<CnfParseException>(() => CnfParser.Parse(input));
        Assert.Equal(position, ex.Position);
        Assert.Contains(messageFragment, ex.Message);
        Assert.Contains($"column {position + 1}", ex.Message);
    }

    [Fact]
    public void Parse_Null_Throws() {
        Assert.Throws<ArgumentNullException>(() => CnfParser.Parse(null!));
    }
}
