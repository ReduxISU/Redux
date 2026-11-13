using API.Interfaces;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE;
using SPADE;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class WEIGHTEDMAXCLIQUE_Tests {
    [Fact]
    public void DefaultInstance_ParsesGraphWeightsAndMetadata() {
        var problem = new WEIGHTEDMAXCLIQUE();
        Assert.Equal(WEIGHTEDMAXCLIQUE.DefaultInstance, problem.instance);
        Assert.Equal(problem.instance, problem.defaultInstance);
        Assert.Equal(ComplexityClass.NPHard, problem.complexityClass);
        Assert.Equal(ProblemVariant.Optimization, problem.problemVariant);
        Assert.Equal(ProblemType.GraphTheory, problem.problemType);
        Assert.Equal(new[] { "a", "b", "c", "d" }, problem.nodes.Order(StringComparer.Ordinal));
        Assert.Equal(4, problem.edges.Count);
        foreach (var edge in new[] { ("a", "b"), ("a", "c"), ("b", "c"), ("b", "d") }) {
            Assert.Contains(problem.edges, e => (e.Key == edge.Item1 && e.Value == edge.Item2)
                || (e.Value == edge.Item1 && e.Key == edge.Item2));
        }
        Assert.Equal(4, problem.weights["a"]);
        Assert.Equal(3, problem.weights["b"]);
        Assert.Equal(5, problem.weights["c"]);
        Assert.Equal(7, problem.weights["d"]);
        Assert.Equal(19, problem.totalVertexWeight);
        Assert.Contains("({a,b,c},12)", problem.certificateFormat);
        Assert.Contains("({},0)", problem.certificateFormat);
        Assert.Contains(WEIGHTEDMAXCLIQUE.InstanceGrammar, problem.instanceFormat);
        var graph = problem.graph.ToAPIGraph();
        Assert.Equal(4, graph.nodes.Count);
        Assert.Equal(4, graph.links.Count);
        Assert.All(graph.links, link => { Assert.False(link.weighted); Assert.False(link.directed); });
    }

    [Fact]
    public void Weights_AreAssociatedByIdentifierRatherThanPosition() {
        var problem = new WEIGHTEDMAXCLIQUE("( ( { node_2 , node_1 , isolated } , { { node_1 , node_2 } } ), ( ( isolated , 0 ), ( node_1 , 7 ), ( node_2 , 3 ) ) )");
        Assert.Equal(7, problem.weights["node_1"]);
        Assert.Equal(3, problem.weights["node_2"]);
        Assert.Equal(0, problem.weights["isolated"]);
        Assert.Equal(10, problem.totalVertexWeight);
        Assert.Single(problem.edges);
        Assert.Contains("isolated", problem.nodes);
    }

    [Theory]
    [InlineData("(({},{}),())", 0, 0)]
    [InlineData("(({a},{}),((a,0)))", 1, 0)]
    [InlineData("(({a,b},{}),((b,1),(a,1)))", 2, 2)]
    [InlineData("(({a,b,c,d},{{a,b},{c,d}}),((d,0),(b,0),(a,0),(c,0)))", 4, 0)]
    public void Parser_AcceptsEmptyIsolatedDisconnectedZeroAndUnitWeightGraphs(string input, int count, long total) {
        var problem = new WEIGHTEDMAXCLIQUE(input);
        Assert.Equal(count, problem.nodes.Count);
        Assert.Equal(count, problem.weights.Count);
        Assert.Equal(total, problem.totalVertexWeight);
        Assert.Equal(count, problem.graph.ToAPIGraph().nodes.Count);
    }

    [Theory]
    [InlineData("(({a,b},{}),((a,1)))", "missing weight")]
    [InlineData("(({a},{}),((a,1),(a,1)))", "duplicate weight")]
    [InlineData("(({a},{}),((a,1),(a,2)))", "duplicate weight")]
    [InlineData("(({a},{}),((b,1)))", "unknown vertex")]
    [InlineData("(({},{}),((a,1)))", "unknown vertex")]
    [InlineData("(({a},{}),((a,-1)))", "nonnegative integer")]
    [InlineData("(({a},{}),((a,1.5)))", "nonnegative integer")]
    [InlineData("(({a},{}),((a,one)))", "nonnegative integer")]
    [InlineData("(({a},{}),((a,+1)))", "nonnegative integer")]
    [InlineData("(({a},{}),((a,9223372036854775808)))", "nonnegative integer")]
    [InlineData("(({a},{}),((a,(1))))", "nonnegative integer")]
    [InlineData("(({a},{}),((a,1,2)))", "exactly two")]
    [InlineData("(({a},{}),({a,1}))", "parenthesized tuple")]
    [InlineData("(({a},{}),{(a,1)})", "parenthesized list")]
    public void Parser_RejectsInvalidWeightsWithMeaningfulErrors(string input, string detail) {
        var error = Assert.Throws<ProblemParseException>(() => new WEIGHTEDMAXCLIQUE(input));
        Assert.Contains(detail, error.Message);
        Assert.Equal(input, error.Received);
        Assert.Equal("Weighted Maximum Clique", error.ProblemName);
    }

    [Theory]
    [InlineData("(({a,b},{{a,z}}),((a,1),(b,1)))")]
    [InlineData("(({a,b},{(a,b)}),((a,1),(b,1)))")]
    [InlineData("(({a,b},{{a}}),((a,1),(b,1)))")]
    [InlineData("(({a,b},{{a,a}}),((a,1),(b,1)))")]
    [InlineData("(({a,b,c},{{a,b,c}}),((a,1),(b,1),(c,1)))")]
    [InlineData("(({a,b},{a,b}),((a,1),(b,1)))")]
    [InlineData("(({a,b},({a,b})),((a,1),(b,1)))")]
    [InlineData("(({},{{a,b}}),())")]
    public void Parser_RejectsInvalidEdges(string input) {
        Assert.Throws<ProblemParseException>(() => new WEIGHTEDMAXCLIQUE(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("garbage")]
    [InlineData("(({a},{}),((a,1)))trailing")]
    [InlineData("(({a},{}),((a,1)))()")]
    [InlineData("(({a},{}),((a,1)),extra)")]
    [InlineData("(({a},{},extra),((a,1)))")]
    [InlineData("(({a},{}),((a,1))")]
    [InlineData("(({a},{}),((a,1))))")]
    [InlineData("(({a,,b},{}),((a,1),(b,2)))")]
    [InlineData("(({a,},{}),((a,1)))")]
    [InlineData("(({a},{}),((a,1,)))")]
    [InlineData("(({a},{}),((a,,1)))")]
    [InlineData("(({a},{}),((a,1),))")]
    [InlineData("(({a},{}),((a 1)))")]
    [InlineData("(({a},{}),((a,1]}))")]
    [InlineData("(((a),{}),((a,1)))")]
    [InlineData("(({(a,b)},{}),(((a,b),1)))")]
    public void Parser_RejectsMalformedInput(string? input) {
        Assert.Throws<ProblemParseException>(() => new WEIGHTEDMAXCLIQUE(input!));
    }

    [Fact]
    public void Weights_AllowInt64BoundaryAndRejectOverflowingTotal() {
        var problem = new WEIGHTEDMAXCLIQUE("(({a,b},{{a,b}}),((a,9223372036854775797),(b,10)))");
        Assert.Equal(long.MaxValue, problem.totalVertexWeight);
        Assert.Equal(long.MaxValue - 10, problem.weights["a"]);
        Assert.Equal(long.MaxValue, checked(problem.weights["a"] + problem.weights["b"]));
        Assert.Equal(long.MaxValue, new WEIGHTEDMAXCLIQUE("(({a},{}),((a,9223372036854775807)))").weights["a"]);
        var error = Assert.Throws<ProblemParseException>(() =>
            new WEIGHTEDMAXCLIQUE("(({a,b},{}),((a,9223372036854775807),(b,1)))"));
        Assert.Contains("sum of vertex weights", error.Message);
    }

    [Fact]
    public void SpadeGrammar_PreservesWeightListDuplicatesButCanRetainReversedEdges() {
        // Locks down the installed SPADE behavior on which this parser relies.
        StringParser parser = new(WEIGHTEDMAXCLIQUE.InstanceGrammar);
        parser.parse("(({a,b},{{a,b},{b,a}}),((a,1),(a,1),(b,2)))");
        Assert.Equal(3, parser["W"].Count());
        Assert.Equal(2, parser["E"].Count());
        parser.parse("(({},{}),())");
        Assert.Equal(0, parser["N"].Count());
        Assert.Equal(0, parser["W"].Count());
    }

    [Fact]
    public void Parser_NormalizesDuplicateUndirectedEdgesForModelAndVisualization() {
        var problem = new WEIGHTEDMAXCLIQUE("(({a,b},{{a,b},{b,a},{a,b}}),((a,1),(b,2)))");
        Assert.Equal(new KeyValuePair<string, string>("a", "b"), Assert.Single(problem.edges));
        Assert.Single(problem.graph.ToAPIGraph().links);
    }

    [Fact]
    public void Problem_IsRegisteredWithItsRealDefaultSolver() {
        IGraphProblem problem = new WEIGHTEDMAXCLIQUE();
        Assert.Equal(typeof(WEIGHTEDMAXCLIQUE), ProblemProvider.Problems["weightedmaxclique"]);
        Assert.Equal(typeof(WEIGHTEDMAXCLIQUE), ProblemProvider.GraphProblems["weightedmaxclique"]);
        Assert.IsType<API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Solvers.WeightedMaxCliqueBruteForceSolver>(problem.defaultSolver);
        Assert.NotNull(problem.graph.ToAPIGraph());
    }

    [Fact]
    public void ProblemOperations_UseStableIndicesAndCheckedVertexWeights() {
        var problem = new WEIGHTEDMAXCLIQUE();
        Assert.True(problem.AreAdjacent(0, 1));
        Assert.True(problem.AreAdjacent(1, 0));
        Assert.False(problem.AreAdjacent(0, 0));
        Assert.True(problem.IsClique([0, 1, 2]));
        Assert.False(problem.IsClique([0, 3]));
        Assert.False(problem.IsClique([0, 0]));
        Assert.True(problem.CanAdd(2, [0, 1]));
        Assert.False(problem.CanAdd(3, [0, 1]));
        Assert.Equal(12, problem.Value([0, 1, 2]));
        Assert.Equal("({a,b,c},12)", problem.FormatCertificate([2, 0, 1], 12));
        Assert.True(problem.IsClique([]));
        Assert.Equal(0, problem.Value([]));
        Assert.Equal("({},0)", problem.FormatCertificate([], 0));
    }
}
