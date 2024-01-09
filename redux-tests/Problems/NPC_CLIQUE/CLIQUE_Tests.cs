
using Xunit;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_CLIQUE.Verifiers;
using API.Problems.NPComplete.NPC_CLIQUE.Solvers;

namespace redux_tests;
#pragma warning disable CS1591

public class CLIQUE_Tests
{

    [Fact]
    public void CLIQUEGraph_Default_Instantiation_Test()
    {

        string testValue = "";
        CLIQUE testingClique = new CLIQUE();
        CliqueGraph testGraph = testingClique.cliqueAsGraph;
        Assert.Equal(testingClique.instance, testGraph.ToString()); //Tests that the arcset instance string is equal to its generated graph string
        Assert.Equal(testingClique.defaultInstance, testGraph.ToString()); //Bonus test that ensures the default instance and the current instance are the same. 
        Assert.Equal("(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),3)", testingClique.defaultInstance); //tests default instance
    }


    [Fact]
    public void CLIQUEGraph_Custom_Instance()
    {

        string testValue = "";
        CLIQUE testingCli = new CLIQUE("(({1,2,3,4,5},{{4,1},{1,2},{4,3},{3,2},{2,4}}),1)");
        CliqueGraph testGraph = testingCli.cliqueAsGraph;
        Assert.Equal(testingCli.instance, testGraph.ToString()); //Tests that the arcset instance string is equal to its generated graph string
        Assert.Equal("(({1,2,3,4,5},{{4,1},{1,2},{4,3},{3,2},{2,4}}),1)", testingCli.instance); //
    }

    [Fact]
    public void CLIQUE_verify_trueOutput()
    {

        CLIQUE testCli = new CLIQUE();
        CliqueVerifier verifier = new CliqueVerifier();
        bool vBool = verifier.verify(testCli, "{1,2,4}");
        Assert.True(vBool);

    }

    [Theory] //tests with default graph string Certificates of this test represent junk or empty data. 
    [InlineData("(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),3)", "{5,2,3,4,1}")]
    // [InlineData("{{1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)},1}", "(3,2) (4,2)")]
    [InlineData("(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,3}}),3)", "{1,2,3,4}")]
    public void CLIQUE_verify_theory_false(string CLIQUE_Instance, string testCertificate)
    {   
        CLIQUE testCli = new CLIQUE(CLIQUE_Instance);
        CliqueVerifier verifier = new CliqueVerifier();
        bool valid = verifier.verify(testCli, testCertificate);
        Assert.False(valid);
    }

    [Theory] //tests with default graph string and various certificates, this shows that certificates can be accepted in many formats. (false case)
    [InlineData("(({1,2,3},{{1,2},{2,3},{3,1}}),3)", "{1,2,3}")]
    [InlineData("(({1,2,3,4},{{1,2},{2,3},{3,4},{3,1},{1,4},{2,4}}),4)", "{1,2,3,4}")]
    [InlineData("(({1,2,3,4},{{1,2},{3,4}}),2)","{1,2}")]
     public void CLIQUE_verify_theory_true(string CLIQUE_Instance, string testCertificate){
        CLIQUE testCli = new CLIQUE(CLIQUE_Instance);
        CliqueVerifier verifier = new CliqueVerifier();
        bool hasClique = verifier.verify(testCli, testCertificate);
        Assert.True(hasClique);
    }


    [Fact]
    public void CLIQUE_solve(){
        CLIQUE testCli = new CLIQUE();
        CliqueBruteForce solver = testCli.defaultSolver;
        string solvedString = solver.solve(testCli);
        Assert.Equal("{1,2,4}", solvedString);
    }


    [Theory]
    //default instance test. 
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)","(({x1,!x2,x3,!x1,x3_1,x1_1,x2,!x3,x1_2},{{x1,x3_1},{x1,x1_1},{x1,x2},{x1,!x3},{x1,x1_2},{!x2,!x1},{!x2,x3_1},{!x2,x1_1},{!x2,!x3},{!x2,x1_2},{x3,!x1},{x3,x3_1},{x3,x1_1},{x3,x2},{x3,x1_2},{!x1,!x2},{!x1,x3},{!x1,x2},{!x1,!x3},{x3_1,x1},{x3_1,!x2},{x3_1,x3},{x3_1,x2},{x3_1,x1_2},{x1_1,x1},{x1_1,!x2},{x1_1,x3},{x1_1,x2},{x1_1,!x3},{x1_1,x1_2},{x2,x1},{x2,x3},{x2,!x1},{x2,x3_1},{x2,x1_1},{!x3,x1},{!x3,!x2},{!x3,!x1},{!x3,x1_1},{x1_2,x1},{x1_2,!x2},{x1_2,x3},{x1_2,x3_1},{x1_2,x1_1}}),3)")]
    public void SAT3_To_CLIQUE_Reduction(string vertexInstance,string expectedArcsetInstance){
        SAT3 testSat = new SAT3(vertexInstance);
        SipserReduction reduction = new SipserReduction(testSat);
        CLIQUE reducedToCliqueInstance = reduction.reduce();
        string arcInstance = reducedToCliqueInstance.instance;
        Assert.Equal(expectedArcsetInstance, arcInstance);
    }

}