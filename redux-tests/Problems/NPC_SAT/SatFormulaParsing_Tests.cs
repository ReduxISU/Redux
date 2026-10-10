using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using API.Interfaces;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT3;

namespace redux_tests;

#pragma warning disable CS1591

/// <summary>
/// The formula rule SAT and 3SAT share (#759): variable names are letters, digits and underscores
/// starting with a letter; '!' is NOT and only goes directly in front of a name; '|' is OR, '&' is AND,
/// parentheses group clauses, and whitespace between tokens is ignored. Anything else is a parse error.
/// </summary>
public class SatFormulaParsing_Tests {

    // Each case names the text the error message must quote back.
    public static TheoryData<string, string> RejectedFormulas => new() {
        { "garbage text", "text" },
        { "(a!b) & (!ab)", "a!b" },
        { "(x:1)", "x:1" },
        { "(x,1)", "x,1" },
        { "(ab\tcd | x1)", "cd" },
        { "!!x", "!!x" },
        { "(x1 | )", ")" },
        { "(x1 | x2) & & (x3)", "&" },
        { "()", "()" },
        { "(x1 | x2", "(" },
        { "(x1 | x2))", ")" },
        { "! x1", "!" },
        { "!(x1 | x2)", "!" },
        { "x1 | (x2 & x3)", "|" },
        { "(1 | x2)", "1" },
        { "(_x | x2)", "_x" },
        { "(x-1 | x2)", "x-1" },
        { "(x1' | x2)", "x1'" },
        { "(~x1 | x2)", "~x1" },
        { "(¬x1 | x2)", "¬x1" },
        { "(x1 ∨ x2)", "∨" },
        { "(café | x2)", "café" },
    };

    [Theory]
    [MemberData(nameof(RejectedFormulas))]
    public void SAT_Rejects_Invalid_Formula_Naming_The_Offending_Text(string formula, string offending) {
        var ex = Assert.Throws<ProblemParseException>(() => new SAT(formula));
        Assert.Equal("SAT", ex.ProblemName);
        Assert.Contains(offending, ex.Message);
    }

    [Theory]
    [MemberData(nameof(RejectedFormulas))]
    public void SAT3_Rejects_Invalid_Formula_Naming_The_Offending_Text(string formula, string offending) {
        var ex = Assert.Throws<ProblemParseException>(() => new SAT3(formula));
        Assert.Equal("3SAT", ex.ProblemName);
        Assert.Contains(offending, ex.Message);
    }

    [Theory]
    [InlineData("(x1 | !x2) & (x_3)", "x1,!x2;x_3")]
    [InlineData("(X1 | !Abc_9) & (aB | ab)", "X1,!Abc_9;aB,ab")]
    [InlineData("   (  x1|!x2 )&(x3   |  x4)  ", "x1,!x2;x3,x4")]
    [InlineData("hello", "hello")]
    [InlineData("x1 | !x2", "x1,!x2")]
    [InlineData("((x1|x2)&(x3))", "x1,x2;x3")]
    [InlineData("(x1 | (x2)) & x3", "x1,x2;x3")]
    [InlineData("(x1 |\tx2)\r\n& (x3)", "x1,x2;x3")]
    public void SAT_And_SAT3_Accept_Valid_Formula(string formula, string expectedClauses) {
        var expected = expectedClauses.Split(';').Select(c => c.Split(',').ToList()).ToList();
        Assert.Equal(expected, new SAT(formula).clauses);
        Assert.Equal(expected, new SAT3(formula).clauses);
    }

    [Fact]
    public void Whitespace_Other_Than_Spaces_Is_Stored_As_Spaces() {
        // Solvers re-read problem.instance and strip only ' ', so tabs and newlines are turned into
        // spaces when the problem is built.
        Assert.Equal("(x1 | x2)  & (x3)", new SAT("(x1 |\tx2)\r\n& (x3)").instance);
        Assert.Equal("(x1 | x2)  & (x3)", new SAT3("(x1 |\tx2)\r\n& (x3)").instance);
    }

    [Fact]
    public void Valid_Formula_Keeps_Its_Instance_Text_Unchanged() {
        string sat = new SAT().defaultInstance, sat3 = new SAT3().defaultInstance;
        Assert.Equal(sat, new SAT(sat).instance);
        Assert.Equal(sat3, new SAT3(sat3).instance);
    }

    [Fact]
    public void SAT3_Still_Rejects_A_Clause_With_Four_Literals() {
        var ex = Assert.Throws<ProblemParseException>(() => new SAT3("(x1 | x2 | x3 | x4)"));
        Assert.Contains("4 literals", ex.Message);
    }
}

public class SatFormulaParsing_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public SatFormulaParsing_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("satbruteforcesolver", "(x:1) & (x2)", "x:1")]
    [InlineData("sat3backtrackingsolver", "(a!b) & (!ab)", "a!b")]
    public async Task Solve_InvalidVariableName_Returns400NamingTheText(string solver, string formula, string offending) {
        var response = await _client.PostAsJsonAsync($"/ProblemProvider/solve?solver={solver}", formula, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("instance_parse_error", body.RootElement.GetProperty("error").GetString());
        Assert.Equal(formula, body.RootElement.GetProperty("received").GetString());
        Assert.Contains(offending, body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Verify_GarbageText_Returns400() {
        var request = new { Certificate = "(garbage:True,text:True)", ProblemInstance = "garbage text" };
        var response = await _client.PostAsJsonAsync("/ProblemProvider/verify?verifier=satverifier", request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("instance_parse_error", body);
    }
}
