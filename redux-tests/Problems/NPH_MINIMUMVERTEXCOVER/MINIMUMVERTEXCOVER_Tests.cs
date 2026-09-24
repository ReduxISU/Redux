using Xunit;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Verifiers;
using API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Solvers;
using API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER;
namespace redux_tests;
#pragma warning disable CS1591

public class MINIMUMVERTEXCOVER_Tests {


    [Fact]
    public void defaultInstance_Test() {
        MINIMUMVERTEXCOVER vCov = new MINIMUMVERTEXCOVER();
        string defaultInstance = vCov.defaultInstance;
        Assert.Equal("({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}})", defaultInstance);
    }



    ///<summary>
    ///This test ensures that the vertexcover solver solves an input instance.
    ///We aren't using a random instance here, we are using a graph with 5 nodes that has a 5-clique
    ///ie. every node is connected to every other node. This ensures that when we run this approximation algorithm we only 
    ///get four nodes in the vertexcover output. Essentially, a property of the VC solver is that given a fully connected graph, it will output a 
    ///node list that is a proper subset of that graph (ie. a subset smaller than the full set). 
    ///</summary>
    [Fact]

    public void TwoApproximationMinimumVertexCover_Test() {
        string fiveClique = "({a,b,c,d,e},{{a,b},{a,c},{a,d},{a,e},{b,c},{b,d},{b,e},{c,e},{c,d},{d,e}})";
        MINIMUMVERTEXCOVER vCov = new MINIMUMVERTEXCOVER(fiveClique);
        TwoApproximationMinimumVertexCover vcSolver = new TwoApproximationMinimumVertexCover();
        string nodeOutput = vcSolver.solve(vCov);

        //We know from manually computing this using pen and paper that the above graph will always return a set of four nodes as the solution.
        //Note that we cannot tell exactly which nodes these are, since the solver has built in randomness. 
        Assert.Equal(4, nodeOutput.Split(',').Length);


    }


    // -------------------------------------------------------------------------
    // Self-describing formats (§1.5)
    // -------------------------------------------------------------------------

    [Fact]
    public void MINIMUMVERTEXCOVER_Declares_Formats() {
        MINIMUMVERTEXCOVER vCov = new MINIMUMVERTEXCOVER();
        Assert.False(string.IsNullOrWhiteSpace(vCov.instanceFormat));
        Assert.False(string.IsNullOrWhiteSpace(vCov.certificateFormat));
    }

    // -------------------------------------------------------------------------
    // Constructor — invalid instances (all must throw ProblemParseException)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("")]                                    // empty
    [InlineData("   ")]                                 // whitespace only
    [InlineData("{{a,b,c} : {(a,b)} : 3}")]             // old colon format
    [InlineData("abc")]                                 // bare string
    [InlineData("(({a,b,c},{{a,b}}),x)")]               // non-integer K
    [InlineData("(({a,b,c},{{a,b}})")]                  // unbalanced / truncated
    public void MINIMUMVERTEXCOVER_Constructor_Throws_On_Invalid_Instance(string instance) {
        Assert.Throws<ProblemParseException>(() => new MINIMUMVERTEXCOVER(instance));
    }

    // -------------------------------------------------------------------------
    // Verifier — malformed certificates (must throw CertificateParseException)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("")]        // empty
    [InlineData("   ")]     // whitespace only
    [InlineData("{}")]      // parses to a single empty token
    public void MINIMUMVERTEXCOVER_Verifier_Throws_On_Malformed_Certificate(string certificate) {
        MINIMUMVERTEXCOVER testVert = new MINIMUMVERTEXCOVER();
        MinimumVertexCoverVerifier verifier = testVert.defaultVerifier;
        Assert.Throws<CertificateParseException>(() => verifier.verify(testVert, certificate));
    }

    [Theory] //tests with default graph string Certificates of this test represent junk or empty data.
    [InlineData("({a,b,c,d},{{a,b},{a,c},{a,d}})", "{a}")] //four node graph dependent on a with a in cert
    [InlineData("({a,b,c,d},{{a,b},{a,c},{a,d}})", "{b,c,d}")] //four node graph dependent on a with all nodes except a in cert
    [InlineData("({a,b,c,d,e},{{a,b},{a,c},{a,d},{a,e},{b,c},{b,d},{b,e},{c,e},{c,d},{d,e}})", "{a,b,c,d}")] //five node connected graph, test four nodes
    [InlineData("({a,b,c,d,e},{{a,b},{a,c},{a,d},{a,e},{b,c},{b,d},{b,e},{c,e},{c,d},{d,e}})", "{e,b,c,d}")] //five node connected graph, test four nodes
    public void MINIMUMVERTEXCOVER_verify_theory_true(string MINIMUMVERTEXCOVER_Instance, string testCertificate) {
        MINIMUMVERTEXCOVER testVert = new MINIMUMVERTEXCOVER(MINIMUMVERTEXCOVER_Instance);
        MinimumVertexCoverVerifier verifier = testVert.defaultVerifier;
        bool isValidCover = verifier.verify(testVert, testCertificate);
        Assert.True(isValidCover);
    }

    [Theory] //tests with default graph string and various certificates, this shows that certificates can be accepted in many formats. (false case)
    [InlineData("({a,b,c,d},{{a,b},{a,c},{a,d}})", "{b,c}")] //four node graph dependent on a without a, or all other nodes in cert
    [InlineData("({a,b,c,d,e},{{a,b},{a,c},{a,d},{a,e},{b,c},{b,d},{b,e},{c,e},{c,d},{d,e}})", "{a,b}")] //five node connected graph, test two nodes (ideal solution is 3 nodes, two is impossible)
    [InlineData("({a,b,c,d,e},{{a,b},{a,c},{a,d},{a,e},{b,c},{b,d},{b,e},{c,e},{c,d},{d,e}})", "{e,b}")] //five node connected graph, test two nodes
    public void MINIMUMVERTEXCOVER_verify_theory_false(string MINIMUMVERTEXCOVER_Instance, string testCertificate) {
        MINIMUMVERTEXCOVER testVert = new MINIMUMVERTEXCOVER(MINIMUMVERTEXCOVER_Instance);
        MinimumVertexCoverVerifier verifier = testVert.defaultVerifier;
        bool isValidCover = verifier.verify(testVert, testCertificate);
        Assert.False(isValidCover);
    }

    // -------------------------------------------------------------------------
    // BruteForceMinimumVertexCover
    // -------------------------------------------------------------------------

    [Fact]
    public void BruteForceMinimumVertexCover_Output_Passes_Verifier() {
        MINIMUMVERTEXCOVER problem = new MINIMUMVERTEXCOVER("({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}})");
        BruteForceMinimumVertexCover solver = new BruteForceMinimumVertexCover();
        MinimumVertexCoverVerifier verifier = new MinimumVertexCoverVerifier();

        string certificate = solver.solve(problem);

        Assert.True(verifier.verify(problem, certificate), $"Solver output failed verifier for: {problem.instance}");
    }

    [Fact]
    public void BruteForceMinimumVertexCover_SingleEdge_MinimalCover() {
        // Two nodes, one edge: a size-1 cover must exist (either endpoint covers it).
        MINIMUMVERTEXCOVER problem = new MINIMUMVERTEXCOVER("({a,b},{{a,b}})");
        BruteForceMinimumVertexCover solver = new BruteForceMinimumVertexCover();
        MinimumVertexCoverVerifier verifier = new MinimumVertexCoverVerifier();

        string certificate = solver.solve(problem);

        Assert.True(verifier.verify(problem, certificate), $"Solver output failed verifier for: {problem.instance}");
    }

    [Fact]
    public void BruteForceMinimumVertexCover_FullyConnectedGraph_FindsCoverAmongTies() {
        // A 5-clique has many valid size-4 covers (any 4 of the 5 nodes); the solver only
        // needs to find one of them, exercising nextComb across several increments.
        MINIMUMVERTEXCOVER problem = new MINIMUMVERTEXCOVER(
            "({a,b,c,d,e},{{a,b},{a,c},{a,d},{a,e},{b,c},{b,d},{b,e},{c,e},{c,d},{d,e}})");
        BruteForceMinimumVertexCover solver = new BruteForceMinimumVertexCover();
        MinimumVertexCoverVerifier verifier = new MinimumVertexCoverVerifier();

        string certificate = solver.solve(problem);

        Assert.True(verifier.verify(problem, certificate), $"Solver output failed verifier for: {problem.instance}");
    }

    [Fact]
    public void BruteForceMinimumVertexCover_KEqualsFullNodeCount_TrivialSingleCombination() {
        // K == |nodes| means C(n,n)=1: exactly one combination (all nodes) is ever tried.
        MINIMUMVERTEXCOVER problem = new MINIMUMVERTEXCOVER("({a,b,c},{{a,b},{b,c}})");
        BruteForceMinimumVertexCover solver = new BruteForceMinimumVertexCover();
        MinimumVertexCoverVerifier verifier = new MinimumVertexCoverVerifier();

        string certificate = solver.solve(problem);

        Assert.True(verifier.verify(problem, certificate), $"Solver output failed verifier for: {problem.instance}");
    }

}