using API.Interfaces;
using API.Problems.NPComplete.NPC_UNRESTRICTEDLONGESTPATH;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class UNRESTRICTEDLONGESTPATH_Tests {
    [Fact]
    public void DefaultInstance_ParsesExpectedGraphAndThreshold() {
        var problem = new UNRESTRICTEDLONGESTPATH();
        Assert.Equal(problem.defaultInstance, problem.instance);
        Assert.Equal(6, problem.nodes.Count);
        Assert.Equal(8, problem.edges.Count);
        Assert.Equal(5, problem.K);
        Assert.True(problem.defaultVerifier.verify(problem, "{1,2,3,5,4,6}"));
    }

    [Theory]
    [InlineData("(({a},{}),0)", 1, 0, 0)]
    [InlineData("(({a,b},{}),1)", 2, 0, 1)]
    [InlineData("(({a,b,c,d},{{a,b},{c,d}}),2)", 4, 2, 2)]
    [InlineData("(({a,b,c,d},{{a,b},{b,c}}),2)", 4, 2, 2)]
    [InlineData("(({a,b},{{b,a}}),1)", 2, 1, 1)]
    [InlineData("(({a,b},{{a,b}}),2)", 2, 1, 2)]
    [InlineData("(({a},{}),2147483647)", 1, 0, 2147483647)]
    [InlineData(" ( ( { a , b } , { { a , b } } ) , 1 ) ", 2, 1, 1)]
    public void ValidInstances_PreserveDeclaredState(string input, int nodeCount, int edgeCount, int k) {
        var problem = new UNRESTRICTEDLONGESTPATH(input);
        Assert.Equal(input, problem.instance);
        Assert.Equal(nodeCount, problem.nodes.Count);
        Assert.Equal(edgeCount, problem.edges.Count);
        Assert.Equal(k, problem.K);
        Assert.Equal(nodeCount, problem.graph.ToAPIGraph().nodes.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{{{")]
    [InlineData("not an instance")]
    [InlineData("(({},{}),0)")]
    [InlineData("(({a},{}),-1)")]
    [InlineData("(({a},{}),+1)")]
    [InlineData("(({a},{}),1.5)")]
    [InlineData("(({a},{}),x)")]
    [InlineData("(({a},{}),2147483648)")]
    [InlineData("(({a,a},{}),0)")]
    [InlineData("(({a,b},{{a,a}}),1)")]
    [InlineData("(({a,b},{{a}}),1)")]
    [InlineData("(({a,b},{{a,c}}),1)")]
    [InlineData("(({a,b},{{a,b},{a,b}}),1)")]
    [InlineData("(({a,b},{{a,b},{b,a}}),1)")]
    [InlineData("(({a,b,c},{{a,b,c}}),1)")]
    [InlineData("(({a,b},{(a,b)}),1)")]
    [InlineData("(({a,b},{{{a,b},4}}),1)")]
    [InlineData("(({a,b},{{a,b}}),1,99)")]
    [InlineData("(({a,b},{{a,b}},{extra}),1)")]
    [InlineData("(({a,b},{{a,b}}),1)junk")]
    [InlineData("(({a,,b},{}),0)")]
    [InlineData("(({a,b,},{}),0)")]
    [InlineData("(({a b},{}),0)")]
    [InlineData("(({a,b},{{a,b},}),1)")]
    public void InvalidInstances_ThrowProblemParseException(string? input) {
        var exception = Assert.Throws<ProblemParseException>(() => new UNRESTRICTEDLONGESTPATH(input!));
        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
        Assert.Equal("UNRESTRICTEDLONGESTPATH", exception.ProblemName);
    }

    [Fact]
    public void ParsedProperties_CannotBeReassignedAndCollectionsRejectMutation() {
        var problem = new UNRESTRICTEDLONGESTPATH("(({a,b},{{a,b}}),1)");
        foreach (string name in new[] { "nodes", "edges", "K", "graph", "instance" }) {
            Assert.Null(typeof(UNRESTRICTEDLONGESTPATH).GetProperty(name)!.SetMethod);
        }
        Assert.Throws<NotSupportedException>(() => ((IList<string>)problem.nodes).Add("c"));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<KeyValuePair<string, string>>)problem.edges).Add(new("b", "c")));
        Assert.Equal(2, problem.nodes.Count);
        Assert.Single(problem.edges);
    }

    [Fact]
    public void VerifierName_MatchesProblemName() {
        var problem = new UNRESTRICTEDLONGESTPATH();
        Assert.Equal($"Default {problem.problemName} Verifier", problem.defaultVerifier.verifierName);
    }

    [Theory]
    [InlineData("(({a},{}),0)", "{a}", true)]
    [InlineData("(({a},{}),0)", "{}", false)]
    [InlineData("(({a,b},{}),1)", "{a,b}", false)]
    [InlineData("(({a,b,c},{{a,b},{b,c}}),1)", "{c,b,a}", true)]
    [InlineData("(({a,b,c},{{a,b},{b,c}}),2)", "{a,b,c}", true)]
    [InlineData("(({a,b,c},{{a,b},{b,c}}),3)", "{a,b,c}", false)]
    [InlineData("(({a,b,c},{{a,b},{b,c}}),2)", "{a,b,a}", false)]
    [InlineData("(({a,b,c},{{a,b},{b,c}}),2)", "{a,b,x}", false)]
    [InlineData("(({a,b,c,d},{{a,b},{c,d}}),2)", "{a,b,c}", false)]
    [InlineData("(({a,b,c,d},{{a,b},{b,c}}),2)", "{a,b,d}", false)]
    public void Verifier_HandlesBoundaryAndPathCases(string input, string certificate, bool expected) {
        var problem = new UNRESTRICTEDLONGESTPATH(input);
        Assert.Equal(expected, problem.defaultVerifier.verify(problem, certificate));
    }

    [Theory]
    [InlineData("(({a},{}),0)", true)]
    [InlineData("(({a,b},{}),1)", false)]
    [InlineData("(({a,b},{{a,b}}),2)", false)]
    public void Solver_DeterministicBoundaryResultsMatchVerifier(string input, bool expectedYes) {
        var problem = new UNRESTRICTEDLONGESTPATH(input);
        string answer = problem.defaultSolver.solve(problem);
        Assert.Equal(expectedYes, problem.defaultVerifier.verify(problem, answer));
        if (!expectedYes) Assert.Equal("{}", answer);
    }
}
