using Xunit;
using API.Problems.NPComplete.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_CLIQUE.Verifiers;
using API.Problems.NPComplete.NPC_CLIQUE.Solvers;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_CLIQUE;
using API.Interfaces;
using SPADE;

namespace redux_tests;
#pragma warning disable CS1591

public class CLIQUE_Tests {
    private const string DefaultInstance =
        "(({1,2,3,4,5,6},{{4,1},{1,2},{4,3},{3,2},{2,4},{5,2},{3,5},{5,4},{3,6},{6,4},{1,6}}),4)";

    [Fact]
    public void CLIQUE_Default_Instantiation() {
        CLIQUE clique = new CLIQUE();
        UtilCollectionGraph graph = clique.graph;
        Assert.Equal(4, clique.K);
        Assert.Equal(clique.instance, "(" + graph.ToString() + ",4)");
        Assert.Equal(clique.defaultInstance, "(" + graph.ToString() + ",4)");
        Assert.Equal(new UtilCollection(DefaultInstance), new UtilCollection(clique.defaultInstance));
    }

    [Fact]
    public void CLIQUE_Custom_Instantiation() {
        CLIQUE clique = new CLIQUE("(({1,2,3,4,5},{{4,1},{1,2},{4,3},{3,2},{2,4}}),1)");
        UtilCollectionGraph graph = clique.graph;
        Assert.Equal(1, clique.K);
        Assert.Equal(clique.instance, "(" + graph.ToString() + ",1)");
        Assert.Equal("(({1,2,3,4,5},{{4,1},{1,2},{4,3},{3,2},{2,4}}),1)", clique.instance);
    }

    [Fact]
    public void CLIQUE_Formats_Reflect_Spade_Grammars() {
        CLIQUE clique = new CLIQUE();
        Assert.Contains(CLIQUE.InstanceGrammar, clique.instanceFormat);
        Assert.Contains(CliqueVerifier.CertificateGrammar, clique.certificateFormat);
        Assert.Contains(CliqueVerifier.CertificateExample, clique.certificateFormat);
    }

    [Theory] // certificate must list exactly K pairwise-adjacent nodes
    [InlineData(DefaultInstance, "{2,3,4,5}", true)]
    [InlineData("(({1,2,3},{{1,2},{2,3},{3,1}}),3)", "{1,2,3}", true)]
    [InlineData("(({1,2,3,4},{{1,2},{2,3},{3,4},{3,1},{1,4},{2,4}}),4)", "{1,2,3,4}", true)]
    [InlineData("(({1,2,3,4},{{1,2},{3,4}}),2)", "{1,2}", true)]
    // wrong size: fewer/more nodes than K
    [InlineData(DefaultInstance, "{1,2,3}", false)]
    [InlineData("(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),3)", "{5,2,3,4,1}", false)]
    // right size, but not all pairwise adjacent
    [InlineData("(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,3}}),4)", "{1,2,3,4}", false)]
    // right size, but a node is not in the graph (regression: K==1 skips the
    // pairwise-adjacency check, so membership must be validated explicitly)
    [InlineData("(({1,2,3},{{1,2},{2,3},{3,1}}),1)", "{99}", false)]
    [InlineData("(({1,2,3},{{1,2},{2,3},{3,1}}),1)", "{1}", true)]
    [InlineData("(({1,2,3,4},{{1,2},{2,3},{3,4},{3,1},{1,4},{2,4}}),3)", "{1,2,99}", false)]
    public void CLIQUE_verifier(string instance, string certificate, bool expected) {
        CLIQUE clique = new CLIQUE(instance);
        CliqueVerifier verifier = new CliqueVerifier();
        Assert.Equal(expected, verifier.verify(clique, certificate));
    }

    [Fact]
    public void CLIQUE_verifier_rejects_empty_certificate() {
        CLIQUE clique = new CLIQUE();
        CliqueVerifier verifier = new CliqueVerifier();
        Assert.Throws<CertificateParseException>(() => verifier.verify(clique, ""));
    }

    [Theory] // brute-force solver returns the first clique of size K in node order
    [InlineData(DefaultInstance, "{2,3,4,5}")]
    [InlineData("(({1,2,3},{{1,2},{2,3},{3,1}}),3)", "{1,2,3}")]
    [InlineData("(({1,2,3,4},{{1,2},{3,4}}),2)", "{1,2}")]
    public void CLIQUE_solver(string instance, string certificate) {
        CLIQUE clique = new CLIQUE(instance);
        CliqueBruteForce solver = clique.defaultSolver;
        Assert.Equal(certificate, solver.solve(clique));
    }

    [Fact] // a satisfiable SAT3 reduces to a clique whose solution verifies
    public void SAT3_To_CLIQUE_Reduction_Is_Sound() {
        SAT3 sat = new SAT3();
        SipserReduceToCliqueStandard reduction = new SipserReduceToCliqueStandard(sat);
        CLIQUE reduced = reduction.reductionTo;

        Assert.Equal(sat.clauses.Count, reduced.K);
        string certificate = reduced.defaultSolver.solve(reduced);
        Assert.True(reduced.defaultVerifier.verify(reduced, certificate));
    }
}
