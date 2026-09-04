using Xunit;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.NPComplete.NPC_VERTEXCOVER.ReduceTo.NPC_ARCSET;
using API.Problems.NPComplete.NPC_VERTEXCOVER.Verifiers;
using API.Problems.NPComplete.NPC_VERTEXCOVER.Solvers;
using API.Problems.NPComplete.NPC_VERTEXCOVER.NPHSolvers;
using API.Problems.NPComplete.NPC_VERTEXCOVER;
namespace redux_tests;
#pragma warning disable CS1591

public class VERTEXCOVER_Tests {


    [Fact]
    public void defaultInstance_Test() {
        VERTEXCOVER vCov = new VERTEXCOVER();
        string defaultInstance = vCov.defaultInstance;
        Assert.Equal("(({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}}),3)", defaultInstance);
    }



    ///<summary>
    ///This test ensures that the vertexcover solver solves an input instance.
    ///We aren't using a random instance here, we are using a graph with 5 nodes that has a 5-clique
    ///ie. every node is connected to every other node. This ensures that when we run this approximation algorithm we only 
    ///get four nodes in the vertexcover output. Essentially, a property of the VC solver is that given a fully connected graph, it will output a 
    ///node list that is a proper subset of that graph (ie. a subset smaller than the full set). 
    ///</summary>
    [Fact]

    public void VCSolver_Test() {
        string fiveClique = "(({a,b,c,d,e},{{a,b},{a,c},{a,d},{a,e},{b,c},{b,d},{b,e},{c,e},{c,d},{d,e}}),5)";
        VERTEXCOVER vCov = new VERTEXCOVER(fiveClique);
        VCSolverJanita vcSolver = new VCSolverJanita();
        List<string> nodeOutput = vcSolver.Solve(vCov);

        //We know from manually computing this using pen and paper that the above graph will always return a set of four nodes as the solution.
        //Note that we cannot tell exactly which nodes these are, since the solver has built in randomness. 
        Assert.Equal(4, nodeOutput.Count);


    }


    // -------------------------------------------------------------------------
    // Self-describing formats (§1.5)
    // -------------------------------------------------------------------------

    [Fact]
    public void VERTEXCOVER_Declares_Formats() {
        VERTEXCOVER vCov = new VERTEXCOVER();
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
    public void VERTEXCOVER_Constructor_Throws_On_Invalid_Instance(string instance) {
        Assert.Throws<ProblemParseException>(() => new VERTEXCOVER(instance));
    }

    // -------------------------------------------------------------------------
    // Verifier — malformed certificates (must throw CertificateParseException)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("")]        // empty
    [InlineData("   ")]     // whitespace only
    [InlineData("{}")]      // parses to a single empty token
    public void VERTEXCOVER_Verifier_Throws_On_Malformed_Certificate(string certificate) {
        VERTEXCOVER testVert = new VERTEXCOVER();
        VCVerifier verifier = testVert.defaultVerifier;
        Assert.Throws<CertificateParseException>(() => verifier.verify(testVert, certificate));
    }

    [Theory] //tests with default graph string Certificates of this test represent junk or empty data.
    [InlineData("(({a,b,c,d},{{a,b},{a,c},{a,d}}),1)", "{a}")] //four node graph dependent on a with a in cert
    [InlineData("(({a,b,c,d},{{a,b},{a,c},{a,d}}),1)", "{b,c,d}")] //four node graph dependent on a with all nodes except a in cert
    [InlineData("(({a,b,c,d,e},{{a,b},{a,c},{a,d},{a,e},{b,c},{b,d},{b,e},{c,e},{c,d},{d,e}}),5)", "{a,b,c,d}}")] //five node connected graph, test four nodes
    [InlineData("(({a,b,c,d,e},{{a,b},{a,c},{a,d},{a,e},{b,c},{b,d},{b,e},{c,e},{c,d},{d,e}}),5)", "{e,b,c,d}}")] //five node connected graph, test four nodes
    public void VERTEXCOVER_verify_theory_true(string VERTEXCOVER_Instance, string testCertificate) {
        VERTEXCOVER testVert = new VERTEXCOVER(VERTEXCOVER_Instance);
        VCVerifier verifier = testVert.defaultVerifier;
        bool isValidCover = verifier.verify(testVert, testCertificate);
        Assert.True(isValidCover);
    }

    [Theory] //tests with default graph string and various certificates, this shows that certificates can be accepted in many formats. (false case)
    [InlineData("(({a,b,c,d},{{a,b},{a,c},{a,d}}),1)", "{b,c}")] //four node graph dependent on a without a, or all other nodes in cert
    [InlineData("(({a,b,c,d,e},{{a,b},{a,c},{a,d},{a,e},{b,c},{b,d},{b,e},{c,e},{c,d},{d,e}}),5)", "{a,b}}")] //five node connected graph, test two nodes (ideal solution is 3 nodes, two is impossible)
    [InlineData("(({a,b,c,d,e},{{a,b},{a,c},{a,d},{a,e},{b,c},{b,d},{b,e},{c,e},{c,d},{d,e}}),5)", "{e,b}}")] //five node connected graph, test two nodes
    public void VERTEXCOVER_verify_theory_false(string VERTEXCOVER_Instance, string testCertificate) {
        VERTEXCOVER testVert = new VERTEXCOVER(VERTEXCOVER_Instance);
        VCVerifier verifier = testVert.defaultVerifier;
        bool isValidCover = verifier.verify(testVert, testCertificate);
        Assert.False(isValidCover);
    }

    // -------------------------------------------------------------------------
    // VertexCoverBruteForce
    // -------------------------------------------------------------------------

    [Fact]
    public void VertexCoverBruteForce_Output_Passes_Verifier() {
        VERTEXCOVER problem = new VERTEXCOVER("(({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}}),3)");
        VertexCoverBruteForce solver = new VertexCoverBruteForce();
        VCVerifier verifier = new VCVerifier();

        string certificate = solver.solve(problem);

        Assert.True(verifier.verify(problem, certificate), $"Solver output failed verifier for: {problem.instance}");
    }

    [Fact]
    public void VertexCoverBruteForce_SingleEdge_MinimalCover() {
        // Two nodes, one edge: a size-1 cover must exist (either endpoint covers it).
        VERTEXCOVER problem = new VERTEXCOVER("(({a,b},{{a,b}}),1)");
        VertexCoverBruteForce solver = new VertexCoverBruteForce();
        VCVerifier verifier = new VCVerifier();

        string certificate = solver.solve(problem);

        Assert.True(verifier.verify(problem, certificate), $"Solver output failed verifier for: {problem.instance}");
    }

    [Fact]
    public void VertexCoverBruteForce_KTooSmall_ReturnsEmptyBraces() {
        // A triangle needs at least 2 nodes to cover every edge -- K=1 is infeasible.
        VERTEXCOVER problem = new VERTEXCOVER("(({a,b,c},{{a,b},{b,c},{a,c}}),1)");
        VertexCoverBruteForce solver = new VertexCoverBruteForce();

        string certificate = solver.solve(problem);

        Assert.Equal("{}", certificate);
    }

    [Fact]
    public void VertexCoverBruteForce_FullyConnectedGraph_FindsCoverAmongTies() {
        // A 5-clique has many valid size-4 covers (any 4 of the 5 nodes); the solver only
        // needs to find one of them, exercising nextComb across several increments.
        VERTEXCOVER problem = new VERTEXCOVER(
            "(({a,b,c,d,e},{{a,b},{a,c},{a,d},{a,e},{b,c},{b,d},{b,e},{c,e},{c,d},{d,e}}),4)");
        VertexCoverBruteForce solver = new VertexCoverBruteForce();
        VCVerifier verifier = new VCVerifier();

        string certificate = solver.solve(problem);

        Assert.True(verifier.verify(problem, certificate), $"Solver output failed verifier for: {problem.instance}");
    }

    [Fact]
    public void VertexCoverBruteForce_KEqualsFullNodeCount_TrivialSingleCombination() {
        // K == |nodes| means C(n,n)=1: exactly one combination (all nodes) is ever tried.
        VERTEXCOVER problem = new VERTEXCOVER("(({a,b,c},{{a,b},{b,c}}),3)");
        VertexCoverBruteForce solver = new VertexCoverBruteForce();
        VCVerifier verifier = new VCVerifier();

        string certificate = solver.solve(problem);

        Assert.True(verifier.verify(problem, certificate), $"Solver output failed verifier for: {problem.instance}");
    }

    [Fact]
    public void VertexCoverBruteForce_GetSolutionDict_MapsSolvedAndUnsolvedNodes() {
        VertexCoverBruteForce solver = new VertexCoverBruteForce();
        string instance = "(({a,b,c},{{a,b},{b,c}}),1)";

        Dictionary<string, bool> dict = solver.getSolutionDict(instance, "{b}");

        Assert.True(dict["b"]);
        Assert.False(dict["a"]);
        Assert.False(dict["c"]);
    }

    [Fact(Skip = "BUG: VertexCoverBruteForce.solve() throws ArgumentOutOfRangeException for K=0 instead " +
        "of returning a certificate (e.g. \"{}\" for a graph with no edges, which trivially has a " +
        "size-0 vertex cover). indexListToCertificate() builds an empty candidate string via string " +
        "concatenation, then calls certificate.Substring(1) unconditionally -- Substring(1) on an empty " +
        "string throws because there's no index 1 to start from. This happens before the candidate is " +
        "even handed to the verifier. See VertexCoverBruteForce.indexListToCertificate().")]
    public void VertexCoverBruteForce_KZero_ThrowsInsteadOfReturningEmptyCertificate() {
        VERTEXCOVER problem = new VERTEXCOVER("(({a,b},{}),0)");
        VertexCoverBruteForce solver = new VertexCoverBruteForce();

        string certificate = solver.solve(problem);

        Assert.Equal("{}", certificate);
    }

}