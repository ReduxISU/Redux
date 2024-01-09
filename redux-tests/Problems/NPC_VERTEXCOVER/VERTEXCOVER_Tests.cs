using Xunit;
using API.Interfaces.Graphs;
using API.Problems.NPComplete.NPC_VERTEXCOVER.ReduceTo.NPC_ARCSET;
using API.Problems.NPComplete.NPC_VERTEXCOVER.Verifiers;
using API.Problems.NPComplete.NPC_VERTEXCOVER.Solvers;
using API.Problems.NPComplete.NPC_VERTEXCOVER.NPHSolvers;
using API.Problems.NPComplete.NPC_VERTEXCOVER;
namespace redux_tests;
#pragma warning disable CS1591

public class VERTEXCOVER_Tests
{


    [Fact]
    public void defaultInstance_Test()
    {
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

    public void VCSolver_Test()
    {
        string fiveClique = "(({a,b,c,d,e},{{a,b},{a,c},{a,d},{a,e},{b,c},{b,d},{b,e},{c,e},{c,d},{d,e}}),5)";
        VERTEXCOVER vCov = new VERTEXCOVER(fiveClique);
        VCSolverJanita vcSolver = new VCSolverJanita();
        List<string> nodeOutput = vcSolver.Solve(vCov);

        //We know from manually computing this using pen and paper that the above graph will always return a set of four nodes as the solution.
        //Note that we cannot tell exactly which nodes these are, since the solver has built in randomness. 
        Assert.Equal(4, nodeOutput.Count);


    }


    [Theory] //tests with default graph string Certificates of this test represent junk or empty data. 
    [InlineData("(({a,b,c,d},{{a,b},{a,c},{a,d}}),1)", "{a}")] //four node graph dependent on a with a in cert
    [InlineData("(({a,b,c,d},{{a,b},{a,c},{a,d}}),1)", "{b,c,d}")] //four node graph dependent on a with all nodes except a in cert
    [InlineData("(({a,b,c,d,e},{{a,b},{a,c},{a,d},{a,e},{b,c},{b,d},{b,e},{c,e},{c,d},{d,e}}),5)","{a,b,c,d}}")] //five node connected graph, test four nodes
    [InlineData("(({a,b,c,d,e},{{a,b},{a,c},{a,d},{a,e},{b,c},{b,d},{b,e},{c,e},{c,d},{d,e}}),5)","{e,b,c,d}}")] //five node connected graph, test four nodes
    public void VERTEXCOVER_verify_theory_true(string VERTEXCOVER_Instance, string testCertificate){
        VERTEXCOVER testVert = new VERTEXCOVER(VERTEXCOVER_Instance);
        VCVerifierJanita verifier = testVert.defaultVerifier;
        bool isValidCover = verifier.Verify(testVert, testCertificate);
        Assert.True(isValidCover);
    }

    [Theory] //tests with default graph string and various certificates, this shows that certificates can be accepted in many formats. (false case)
    [InlineData("(({a,b,c,d},{{a,b},{a,c},{a,d}}),1)", "{b,c}")] //four node graph dependent on a without a, or all other nodes in cert
    [InlineData("(({a,b,c,d,e},{{a,b},{a,c},{a,d},{a,e},{b,c},{b,d},{b,e},{c,e},{c,d},{d,e}}),5)","{a,b}}")] //five node connected graph, test two nodes (ideal solution is 3 nodes, two is impossible)
    [InlineData("(({a,b,c,d,e},{{a,b},{a,c},{a,d},{a,e},{b,c},{b,d},{b,e},{c,e},{c,d},{d,e}}),5)","{e,b}}")] //five node connected graph, test two nodes
     public void VERTEXCOVER_verify_theory_false(string VERTEXCOVER_Instance, string testCertificate){
        VERTEXCOVER testVert = new VERTEXCOVER(VERTEXCOVER_Instance);
        VCVerifierJanita verifier = testVert.defaultVerifier;
        bool isValidCover = verifier.Verify(testVert, testCertificate);
        Assert.False(isValidCover);
    }



}