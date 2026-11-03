using API.DummyClasses;
using API.Interfaces;
using API.Problems.NPComplete.NPC_CSP;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class CSP_Tests {
    [Fact]
    public void DefaultInstance_HasExpectedPositionsPoolAndMetadata() {
        CSP problem = new();
        Assert.Equal(CSP.DefaultInstance, problem.instance);
        Assert.Equal(problem.defaultInstance, new CSP(problem.defaultInstance).instance);
        Assert.Equal(new[] { ("A", 1, 2), ("B", 2, 2), ("C", 1, 1), ("D", 2, 1) }, problem.C0);
        Assert.Equal(new[] { ("B", 1, 2), ("E", 2, 2), ("C", 1, 1), ("D", 2, 1) }, problem.C1);
        Assert.Equal((0, 2), problem.poolPosition);
        Assert.Equal(new[] { "E" }, problem.poolCores);
        Assert.Equal(20, problem.K);
        IProblem metadata = problem;
        Assert.False(metadata is IGraphProblem);
        Assert.IsType<DummySolver>(metadata.defaultSolver);
        Assert.IsType<DummyVerifier>(metadata.defaultVerifier);
        Assert.IsType<DummyVisualization>(metadata.defaultVisualization);
        Assert.Equal(ComplexityClass.NPComplete, metadata.complexityClass);
        Assert.Equal(ProblemVariant.Decision, metadata.problemVariant);
        Assert.Equal(ProblemType.SequencingAndScheduling, metadata.problemType);
        Assert.Contains("Quinton Smith", metadata.contributors);
        foreach (string value in new[] { metadata.problemName, metadata.formalDefinition,
            metadata.problemDefinition, metadata.inputDescription, metadata.outputDescription,
            metadata.source, metadata.sourceFile, metadata.instanceFormat, metadata.certificateFormat })
            Assert.False(string.IsNullOrWhiteSpace(value));
    }

    [Fact]
    public void Layouts_PreserveCoreOrderCoordinatesAndPoolContents() {
        CSP problem = new("(((Z,-2,5),(A,3,8),(P,0,9,{Fresh_1,Fresh_2})),((Fresh_2,-2,5),(Z,3,8)),27)");
        Assert.Equal(new[] { ("Z", -2, 5), ("A", 3, 8) }, problem.C0);
        Assert.Equal(new[] { ("Fresh_2", -2, 5), ("Z", 3, 8) }, problem.C1);
        Assert.Equal((0, 9), problem.poolPosition);
        Assert.True(problem.poolCores.SetEquals(new[] { "Fresh_1", "Fresh_2" }));
        Assert.Equal(27, problem.K);
    }

    [Fact]
    public void Whitespace_IsAcceptedAndPreservedInInstance() {
        const string input = " \n( ( ( A_1 , 1 , 2 ) , ( P , 0 , 2 , { b2 } ) ), (( b2 , 1 , 2 )), 20 )\t";
        CSP problem = new(input);
        Assert.Equal(input, problem.instance);
        Assert.Equal(("A_1", 1, 2), Assert.Single(problem.C0));
        Assert.Equal(("b2", 1, 2), Assert.Single(problem.C1));
        Assert.Equal("b2", Assert.Single(problem.poolCores));
    }

    [Theory]
    [InlineData("(((A,1,1),(P,0,0,{})),((A,1,1)),0)", 0)]
    [InlineData("(((A,1,1),(P,0,0,{B})),((B,1,1)),00020)", 20)]
    [InlineData("(((A,1,1),(P,0,0,{})),((A,1,1)),2147483647)", int.MaxValue)]
    public void ValidLayouts_AcceptNonnegativeInt32Bounds(string input, int bound) {
        CSP problem = new(input);
        Assert.Single(problem.C0);
        Assert.Single(problem.C1);
        Assert.Equal(bound, problem.K);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n")]
    [InlineData("A")]
    [InlineData("()")]
    [InlineData("(((A)),((A)),0)")] // Obsolete matrix format.
    [InlineData("(((A,1,1)),((A,1,1)),0)")] // Missing pool.
    [InlineData("((),((A,1,1)),0)")]
    [InlineData("(((A,1,1),(Q,0,0,{})),((A,1,1)),0)")]
    [InlineData("(((A,1,1),(P,0,0)),((A,1,1)),0)")]
    [InlineData("(((A,1,1),(P,0,0,{},extra)),((A,1,1)),0)")]
    [InlineData("(((A,1),(P,0,0,{})),((A,1,1)),0)")]
    [InlineData("(((A,1,1,extra),(P,0,0,{})),((A,1,1)),0)")]
    [InlineData("(((A,1,1),(P,0,0,{})),((A,1)),0)")]
    [InlineData("(((A,1,1),(P,0,0,{})),((A,1,1,extra)),0)")]
    [InlineData("(((A,x,1),(P,0,0,{})),((A,1,1)),0)")]
    [InlineData("(((A,1,1),(P,x,0,{})),((A,1,1)),0)")]
    [InlineData("(((A,1,1),(P,0,0,{})),((A,1,y)),0)")]
    [InlineData("(((A,2147483648,1),(P,0,0,{})),((A,1,1)),0)")]
    [InlineData("(((A,1,1),(P,0,0,{})),((A,1,1)),-1)")]
    [InlineData("(((A,1,1),(P,0,0,{})),((A,1,1)),2147483648)")]
    [InlineData("(((A,1,1),(P,0,0,{})),((A,1,1)),1.5)")]
    [InlineData("(((A,1,1),(P,0,0,{})),((A,1,1)),one)")]
    [InlineData("(((A,1,1),(P,0,0,{})),((A,1,1)),0")]
    public void MalformedInput_ThrowsProblemParseException(string? input) {
        Assert.Throws<ProblemParseException>(() => new CSP(input!));
    }

    [Fact]
    public void DummySolver_ReturnsNoCertificate() {
        IProblem problem = new CSP();
        Assert.Empty(problem.defaultSolver.solve(problem.instance));
        problem.defaultSolver.timerHasExpired = true;
        Assert.Empty(problem.defaultSolver.solve(problem.instance));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{{{")]
    [InlineData("((B,2,2,1,2),(E,0,2,2,2))")]
    public void PlaceholderVerifier_ReportsThatVerificationIsUnavailable(string certificate) {
        IProblem problem = new CSP();
        Assert.Throws<NoVerifierException>(() => problem.defaultVerifier.verify(problem.instance, certificate));
    }
}
