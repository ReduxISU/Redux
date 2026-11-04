using API.Interfaces;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class CONSTRAINTSATISFACTION_Tests {
    [Fact]
    public void DefaultInstance_ParsesAndWiresExistingComponents() {
        var problem = new CONSTRAINTSATISFACTION();
        Assert.Equal(problem.defaultInstance, problem.instance);
        Assert.Equal(new[] { "x", "y" }, problem.X.ToList().Select(v => v.ToString()));
        Assert.Equal(2, problem.D.Count());
        Assert.Equal(new[] { "1", "2" }, problem.D[0].ToList().Select(v => v.ToString()));
        Assert.Single(problem.C.ToList());
        Assert.Contains("x!=y", problem.C[0].ToString());
        Assert.Equal(ComplexityClass.NPComplete, problem.complexityClass);
        Assert.Equal(ProblemType.MathematicalProgramming, problem.problemType);
        Assert.NotNull(problem.defaultSolver);
        Assert.NotNull(problem.defaultVerifier);
        Assert.NotNull(problem.defaultVisualization);
    }

    [Theory]
    [InlineData("((a,b,c),({-1,0},{1,2,3},{42}),{\"a<b\",\"b<c\"})", 3, 2)]
    [InlineData(" ( (x) , ( {-2,0,3} ) , {} ) ", 1, 0)]
    [InlineData("((),(),{})", 0, 0)]
    [InlineData("((x),({1}),{\"f(x,1)=1\"})", 1, 1)]
    public void CustomInstance_PreservesVariablesDomainsAndConstraints(string instance, int variables, int constraints) {
        var problem = new CONSTRAINTSATISFACTION(instance);
        Assert.Equal(instance, problem.instance);
        Assert.Equal(variables, problem.X.Count());
        Assert.Equal(variables, problem.D.Count());
        Assert.Equal(constraints, problem.C.Count());
    }

    [Fact]
    public void Parsing_PreservesDomainPositionsAndQuotedConstraintText() {
        var problem = new CONSTRAINTSATISFACTION("((y,x),({-2,3},{1}),{\"f(y, x)=1\"})");
        Assert.Equal(new[] { "y", "x" }, problem.X);
        Assert.Equal(new[] { -2, 3 }, problem.D[0]);
        Assert.Equal(new[] { 1 }, problem.D[1]);
        Assert.Equal("f(y, x)=1", Assert.Single(problem.C));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("garbage")]
    [InlineData("((x),({1}))")]
    [InlineData("((x),({1}),{},extra)")]
    [InlineData("((x,x),({1},{2}),{})")]
    [InlineData("((1x),({1}),{})")]
    [InlineData("((x,y),({1}),{})")]
    [InlineData("((x),({}),{})")]
    [InlineData("((x),({a}),{})")]
    [InlineData("((x),({2147483648}),{})")]
    [InlineData("((x),({1,01}),{})")]
    [InlineData("((x),({1,}),{})")]
    [InlineData("((x),({1}),{x=1})")]
    [InlineData("((x),({1}),{\"\"})")]
    [InlineData("((x),({1}),{\"x=1})")]
    [InlineData("((x),({1]),{})")]
    [InlineData("((x),({1}),{}) trailing")]
    public void MalformedInstance_ThrowsProblemParseException(string? instance) {
        var error = Assert.Throws<ProblemParseException>(() => new CONSTRAINTSATISFACTION(instance!));
        Assert.Equal("Constraint Satisfaction Problem", error.ProblemName);
        Assert.Equal(instance, error.Received);
    }
}
