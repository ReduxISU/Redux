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

    // -------------------------------------------------------------------------
    // CliqueBruteForce -- additional coverage
    // -------------------------------------------------------------------------

    [Fact]
    public void CliqueBruteForce_NoCliqueOfRequestedSize_ReturnsEmptyBraces() {
        // A graph with no edges at all cannot contain a 2-clique.
        CLIQUE clique = new CLIQUE("(({1,2,3},{}),2)");
        CliqueBruteForce solver = new CliqueBruteForce();

        string certificate = solver.solve(clique);

        Assert.Equal("{}", certificate);
    }

    [Fact]
    public void CliqueBruteForce_KEqualsFullNodeCount_TrivialSingleCombination() {
        // K == |nodes| means C(n,n)=1: exactly one combination (all nodes) is ever tried.
        CLIQUE clique = new CLIQUE("(({1,2,3},{{1,2},{2,3},{1,3}}),3)");
        CliqueBruteForce solver = new CliqueBruteForce();
        CliqueVerifier verifier = new CliqueVerifier();

        string certificate = solver.solve(clique);

        Assert.Equal("{1,2,3}", certificate);
        Assert.True(verifier.verify(clique, certificate));
    }

    [Fact]
    public void CliqueBruteForce_KEqualsOne_AnySingleNodeIsATrivialClique() {
        CLIQUE clique = new CLIQUE("(({1,2,3},{}),1)");
        CliqueBruteForce solver = new CliqueBruteForce();
        CliqueVerifier verifier = new CliqueVerifier();

        string certificate = solver.solve(clique);

        Assert.True(verifier.verify(clique, certificate), $"Solver output failed verifier for: {clique.instance}");
    }

    [Fact]
    public void CliqueBruteForce_GetSteps_RecordsRejectedCombinationsBeforeSuccess() {
        // Only the {3,4} pair is an edge, so the lexicographically-earlier size-2 combinations
        // (1,2), (1,3), (1,4), (2,3), (2,4) all get rejected and recorded by getSteps before the
        // winning combination (3,4) is found. Per getSteps' contract, the winning combo itself is
        // NOT appended -- it returns as soon as verify() succeeds.
        CLIQUE clique = new CLIQUE("(({1,2,3,4},{{3,4}}),2)");
        CliqueBruteForce solver = new CliqueBruteForce();
        CliqueVerifier verifier = new CliqueVerifier();

        List<string> steps = solver.getSteps(clique);

        Assert.Equal(5, steps.Count);
        foreach (string step in steps) {
            Assert.False(verifier.verify(clique, step));
        }
    }

    [Fact]
    public void CliqueBruteForce_GetSteps_NoSolution_EndsWithEmptyBraces() {
        CLIQUE clique = new CLIQUE("(({1,2,3},{}),2)");
        CliqueBruteForce solver = new CliqueBruteForce();

        List<string> steps = solver.getSteps(clique);

        Assert.Equal("{}", steps[steps.Count - 1]);
    }

    [Fact]
    public void CliqueBruteForce_GetSolutionDict_MapsSolvedAndUnsolvedNodes() {
        CliqueBruteForce solver = new CliqueBruteForce();
        string instance = "(({1,2,3},{{1,2},{2,3},{1,3}}),2)";

        Dictionary<string, bool> dict = solver.getSolutionDict(instance, "{1,2}");

        Assert.True(dict["1"]);
        Assert.True(dict["2"]);
        Assert.False(dict["3"]);
    }

    [Fact(Skip = "BUG: CliqueBruteForce.solve() throws CertificateParseException for K=0 instead of " +
        "returning a certificate (e.g. \"{}\", the trivially-true empty clique). indexListToCertificate() " +
        "correctly builds \"{}\" for an empty index list, but that \"{}\" is then handed to " +
        "CliqueVerifier.verify(), whose CertificateGrammar parse yields an empty node list -- which " +
        "verify() explicitly rejects by throwing CertificateParseException(\"certificate did not parse " +
        "to a non-empty list of node names\"). solve() has no try/catch around that call, so the " +
        "exception propagates out of solve() itself. See CliqueBruteForce.solve() and " +
        "CliqueVerifier.verify().")]
    public void CliqueBruteForce_KZero_ThrowsInsteadOfReturningEmptyCertificate() {
        CLIQUE clique = new CLIQUE("(({1,2,3},{{1,2},{2,3},{1,3}}),0)");
        CliqueBruteForce solver = new CliqueBruteForce();

        string certificate = solver.solve(clique);

        Assert.Equal("{}", certificate);
    }
}
