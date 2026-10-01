using API.Interfaces;
using API.Problems.NPComplete.NPC_EDITDISTANCE;
using API.Problems.NPComplete.NPC_SUBSETSUM;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class InputWhitespace_Tests {
    [Theory]
    [InlineData("(({1, 2}, {(1, 2)}), 3)", "(({1,2},{(1,2)}),3)")]
    [InlineData("{1,\n2,\r\n3,\t4}", "{1,2,3,4}")]
    [InlineData("  \n({1,2},\r\n  3)\r\n", "({1,2},3)")]
    [InlineData("( { a , b } ; [ c ] : d = e )", "({a,b};[c]:d=e)")]
    [InlineData("{\r\n  1,\r\n  2\r\n}", "{1,2}")]
    [InlineData("x1:True,\nx2:False", "x1:True,x2:False")]
    public void Normalize_RemovesWhitespaceAroundDelimiters(string input, string expected) =>
        Assert.Equal(expected, InputWhitespace.Normalize(input));

    [Theory]
    [InlineData("{New York, Los Angeles}", "{New York,Los Angeles}")]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x2)", "(x1 | !x2 | x3)&(!x1 | x2)")] // only the spaces next to the parentheses go
    [InlineData("(-1 1 -1),(0 0 -1)", "(-1 1 -1),(0 0 -1)")]
    [InlineData("{New  York}", "{New  York}")] // a run of plain spaces between words is left as typed
    public void Normalize_KeepsSpacesBetweenNonDelimiters(string input, string expected) =>
        Assert.Equal(expected, InputWhitespace.Normalize(input));

    [Theory]
    [InlineData("{New\nYork}", "{New York}")]
    [InlineData("{New\r\n  York}", "{New York}")]
    [InlineData("{New\tYork}", "{New York}")]
    [InlineData("(x1 |\n!x2)", "(x1 | !x2)")]
    public void Normalize_CollapsesLineBreaksAndTabsBetweenWordsToOneSpace(string input, string expected) =>
        Assert.Equal(expected, InputWhitespace.Normalize(input));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n")]
    public void Normalize_BlankInputBecomesEmpty(string input) =>
        Assert.Equal("", InputWhitespace.Normalize(input));

    [Fact]
    public void Normalize_Null_BecomesEmpty() =>
        Assert.Equal("", InputWhitespace.Normalize(null));

    [Fact]
    public void Normalize_IsIdempotent() {
        string once = InputWhitespace.Normalize(" ({New York,\nLos Angeles}, 3)\r\n");
        Assert.Equal(once, InputWhitespace.Normalize(once));
    }

    [Fact]
    public void ForInstanceOfType_OptedOutProblem_OnlyTrims() {
        Assert.Equal("(horse,\n ros)", InputWhitespace.ForInstanceOfType(typeof(EDITDISTANCE), "\n(horse,\n ros) \r\n"));
        Assert.Equal("(horse,ros)", InputWhitespace.ForInstanceOfType(typeof(SUBSETSUM), "(horse,\n ros) "));
    }

    [Fact]
    public void VerifyCertificate_AlreadyValidCertificate_IsNeverAltered() {
        var seen = new List<string>();
        bool result = ParseGuard.VerifyCertificate(new SUBSETSUM(), "{a, b}", c => { seen.Add(c); return true; });
        Assert.True(result);
        Assert.Equal(new[] { "{a, b}" }, seen);
    }

    [Fact]
    public void VerifyCertificate_FalseThenNormalized_RetriesOnceAndReturnsRetryResult() {
        var seen = new List<string>();
        bool result = ParseGuard.VerifyCertificate(new SUBSETSUM(), "{1,\n2}", c => { seen.Add(c); return c == "{1,2}"; });
        Assert.True(result);
        Assert.Equal(new[] { "{1,\n2}", "{1,2}" }, seen);
    }

    [Fact]
    public void VerifyCertificate_BothAttemptsThrow_ReportsOriginalError() {
        var problem = new SUBSETSUM();
        var ex = Assert.Throws<CertificateParseException>(() => ParseGuard.VerifyCertificate(problem, "{1,\n2}", c =>
            throw new FormatException(c == "{1,2}" ? "retry" : "original")));
        Assert.Equal("original", ex.Message);
        Assert.Equal("{1,\n2}", ex.Received);
    }
}
