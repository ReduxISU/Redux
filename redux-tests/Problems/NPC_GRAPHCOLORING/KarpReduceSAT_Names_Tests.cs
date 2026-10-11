#pragma warning disable CS1591
using Xunit;
using API.Problems.NPComplete.NPC_GRAPHCOLORING;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo.NPC_SAT;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.Verifiers;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT.Solvers;
using API.Problems.NPComplete.NPC_SAT.Verifiers;

namespace API.Tests.Problems.NPC_GRAPHCOLORING;

// #825: Graph Coloring -> SAT names its variables by node position, so any node name reduces and no two (node, color)
// pairs share a variable.
public class KarpReduceSAT_Names_Tests {

    private static IEnumerable<string> Variables(SAT sat) =>
        sat.clauses.SelectMany(c => c).Select(l => l.TrimStart('!')).Distinct();

    [Fact]
    public void NodeNameThatIsNotASatVariable_Reduces_Solves_AndDecodesToAColoring() {
        var source = new GRAPHCOLORING("(({a-b,c},{{a-b,c}}),2)");
        SAT sat = new KarpReduceSAT(source).reductionTo;

        Assert.Equal(new[] { "v0_0", "v0_1", "v1_0", "v1_1" }, Variables(sat).OrderBy(v => v, StringComparer.Ordinal));

        string assignment = new SATBruteForceSolver().solve(sat.instance);
        Assert.True(new SATVerifier().verify(sat, assignment), assignment);

        // Read the coloring back: node i takes the color c whose variable v{i}_{c} is True.
        var values = assignment.Trim('(', ')').Split(',').Select(p => p.Split(':'))
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim() == "True");
        var classes = Enumerable.Range(0, source.K)
            .Select(c => source.nodes.Where((n, i) => values[KarpReduceSAT.VariableName(i, c)]).ToList())
            .Where(cls => cls.Count > 0)
            .Select(cls => "{" + string.Join(",", cls) + "}");
        string coloring = "{" + string.Join(",", classes) + "}";
        Assert.True(new GraphColoringVerifier().verify(source, coloring), coloring);
    }

    [Fact]
    public void NodesWhoseNamesRunIntoTheirColors_GetDistinctVariables() {
        // Before #825, node "a1" with color 0 and node "a" with color 10 both wrote "a10".
        var source = new GRAPHCOLORING("(({a1,a},{{a1,a}}),12)");
        SAT sat = new KarpReduceSAT(source).reductionTo;

        var variables = Variables(sat).ToList();
        Assert.Equal(2 * 12, variables.Count);
        Assert.Contains(KarpReduceSAT.VariableName(0, 0), variables);
        Assert.Contains(KarpReduceSAT.VariableName(1, 10), variables);
        Assert.NotEqual(KarpReduceSAT.VariableName(0, 0), KarpReduceSAT.VariableName(1, 10));
        // Each node's "takes a color" clause lists exactly its own 12 variables.
        Assert.Equal(Enumerable.Range(0, 12).Select(c => KarpReduceSAT.VariableName(0, c)), sat.clauses[0]);
    }
}
