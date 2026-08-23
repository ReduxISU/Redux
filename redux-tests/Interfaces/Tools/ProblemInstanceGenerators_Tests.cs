using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using API.Interfaces.Tools;

namespace redux_tests;
#pragma warning disable CS1591

public class ProblemInstanceGenerators_Tests
{
    // --- UndirectedGraphInstance ---
    // density=0 and density=100 are the only edge-inclusion probabilities that are
    // deterministic (Random.Next(100) is always in [0,99]), so they're used to get
    // exact, non-flaky expected output instead of asserting against random edges.

    [Fact]
    public void UndirectedGraphInstance_Density100_IncludesEveryPair()
    {
        var result = ProblemInstanceGenerators.UndirectedGraphInstance(n: 4, k: -1, density: 100);

        Assert.Equal("{{0,1,2,3},{{0,1},{0,2},{0,3},{1,2},{1,3},{2,3}}}", result);
    }

    [Fact]
    public void UndirectedGraphInstance_Density0_HasNoEdges()
    {
        var result = ProblemInstanceGenerators.UndirectedGraphInstance(n: 4, k: -1, density: 0);

        Assert.Equal("{{0,1,2,3},}", result);
    }

    [Fact]
    public void UndirectedGraphInstance_NonNegativeK_IsAppended()
    {
        var result = ProblemInstanceGenerators.UndirectedGraphInstance(n: 3, k: 5, density: 0);

        Assert.Equal("{{0,1,2},,5}", result);
    }

    [Fact]
    public void UndirectedGraphInstance_NegativeK_IsOmitted()
    {
        var result = ProblemInstanceGenerators.UndirectedGraphInstance(n: 3, k: -1, density: 0);

        Assert.DoesNotContain(",5}", result);
        Assert.Equal("{{0,1,2},}", result);
    }

    [Fact]
    public void UndirectedGraphInstance_DefaultParameters_UsesFiveNodes()
    {
        var result = ProblemInstanceGenerators.UndirectedGraphInstance();

        Assert.StartsWith("{{0,1,2,3,4},", result);
    }

    // --- DirectedGraphInstance ---

    [Fact]
    public void DirectedGraphInstance_Density100_IncludesEveryOrderedPair()
    {
        var result = ProblemInstanceGenerators.DirectedGraphInstance(n: 3, k: -1, density: 100);

        Assert.Equal("{{0,1,2},{(0,1),(0,2),(1,0),(1,2),(2,0),(2,1)}}", result);
    }

    [Fact]
    public void DirectedGraphInstance_Density0_HasNoEdges()
    {
        var result = ProblemInstanceGenerators.DirectedGraphInstance(n: 3, k: -1, density: 0);

        Assert.Equal("{{0,1,2},}", result);
    }

    [Fact]
    public void DirectedGraphInstance_NonNegativeK_IsAppended()
    {
        var result = ProblemInstanceGenerators.DirectedGraphInstance(n: 2, k: 1, density: 0);

        Assert.Equal("{{0,1},,1}", result);
    }

    [Fact]
    public void DirectedGraphInstance_DefaultParameters_UsesFiveNodes()
    {
        var result = ProblemInstanceGenerators.DirectedGraphInstance();

        Assert.StartsWith("{{0,1,2,3,4},", result);
    }

    // --- Sat3Instance ---
    // Clause/literal count and shape are deterministic; only the literal choice
    // (which variable, negated or not) is random, so structure is what's asserted.

    private static readonly Regex ClausePattern = new(@"^\(!?x(\d+) \| !?x(\d+) \| !?x(\d+)\)$");

    [Fact]
    public void Sat3Instance_ZeroClauses_ReturnsEmptyString()
    {
        var result = ProblemInstanceGenerators.Sat3Instance(n: 3, c: 0);

        Assert.Equal("", result);
    }

    [Fact]
    public void Sat3Instance_ProducesRequestedClauseCount()
    {
        var result = ProblemInstanceGenerators.Sat3Instance(n: 3, c: 5);

        var clauses = result.Split(" & ");
        Assert.Equal(5, clauses.Length);
        Assert.All(clauses, clause => Assert.Matches(ClausePattern, clause));
    }

    [Fact]
    public void Sat3Instance_LiteralVariableIndices_StayWithinRequestedRange()
    {
        var result = ProblemInstanceGenerators.Sat3Instance(n: 2, c: 20);

        var indices = result.Split(" & ")
            .SelectMany(clause => ClausePattern.Match(clause).Groups.Values.Skip(1))
            .Select(g => int.Parse(g.Value));

        Assert.All(indices, i => Assert.InRange(i, 0, 1));
    }

    [Fact]
    public void Sat3Instance_DefaultParameters_ProducesThreeClauses()
    {
        var result = ProblemInstanceGenerators.Sat3Instance();

        Assert.Equal(3, result.Split(" & ").Length);
    }
}
