using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_KERNEL;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class KernelReduction_Tests {
    [Theory]
    [InlineData("(x)", true)]
    [InlineData("(x) & (!x)", false)]
    [InlineData("(x | !x)", true)]
    [InlineData("(x | x | x) & (!x)", false)]
    [InlineData("(x | y) & (!x | y) & (x | !y) & (!x | !y)", false)]
    [InlineData("(x | y) & (!x | y) & (x | !y)", true)]
    public void Graph_has_kernel_exactly_when_formula_is_satisfiable(string formula, bool expected) {
        var reduction = new ChvatalReduceToKernel(formula);
        var graph = reduction.reductionTo;
        int[] vertices = graph.vertices.ToArray();
        bool found = false;
        for (int mask = 0; mask < (1 << vertices.Length); mask++) {
            string candidate = "{" + string.Join(",", vertices.Where((_, i) => (mask & (1 << i)) != 0)) + "}";
            if (graph.defaultVerifier.verify(graph, candidate)) { found = true; break; }
        }
        Assert.Equal(expected, found);
    }

    [Fact]
    public void Satisfying_assignment_maps_to_a_verified_kernel_and_reduce_is_repeatable() {
        var reduction = new ChvatalReduceToKernel("(x | !y | x) & (!x | y)");
        string certificate = reduction.mapSolutions("(x:True,y:True)");
        Assert.True(reduction.reductionTo.defaultVerifier.verify(reduction.reductionTo, certificate));
        var again = reduction.reduce();
        Assert.True(again.defaultVerifier.verify(again, certificate));
    }
}
