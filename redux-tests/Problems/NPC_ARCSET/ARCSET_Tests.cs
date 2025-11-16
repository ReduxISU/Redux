//ArcsetTests.cs
using Xunit;
using API.Problems.NPComplete.NPC_ARCSET;
using API.Interfaces.Graphs;
using API.Problems.NPComplete.NPC_VERTEXCOVER.ReduceTo.NPC_ARCSET;
using API.Problems.NPComplete.NPC_ARCSET.Verifiers;
using API.Problems.NPComplete.NPC_ARCSET.Solvers;
using API.Problems.NPComplete.NPC_VERTEXCOVER;
using API.Interfaces;
using SPADE;
namespace redux_tests;

#pragma warning disable CS1591

public class ARCSET_Tests
{
    [Fact]
    public void ARCSET_Generic_Add_Test()
    {
        Assert.Equal(2, (1 + 1));
    }

    /// <summary>
    /// Tests the default instance of an arcset graph
    /// 
    /// </summary>
    [Fact]
    public void ARCSETGraph_Default_Instantiation_Test(){

        ARCSET testingArc = new ARCSET();
        UtilCollectionGraph testGraph = testingArc.graph;
        Assert.Equal(testingArc.instance, "(" + testGraph.ToString() + ",1)"); //Tests that the arcset instance string is equal to its generated graph string plus the rest of the problem
        Assert.Equal(1, testingArc.K);
        Assert.Equal(testingArc.defaultInstance, "(" + testGraph.ToString() + ",1)"); //Bonus test that ensures the default instance and the current instance are the same. 
        Assert.Equal( "(({1,2,3,4},{(1,2),(2,4),(3,2),(4,1),(4,3)}),1)",testingArc.defaultInstance); //tests default instance
    }

    [Fact]
     public void ARCSETGraph_Custom_Instance(){

        string testValue = "";
        ARCSET testingArc = new ARCSET("(({1,2,3,4,5},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)");
        UtilCollectionGraph testGraph = testingArc.graph;
        Assert.Equal(testingArc.instance, "(" + testGraph.ToString() + ",1)"); //Tests that the arcset instance string is equal to its generated graph string, plus the rest of the problem
        Assert.Equal(1, testingArc.K);  //test that it parsed the K value correctly
        Assert.Equal("(({1,2,3,4,5},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)", testingArc.instance); //
    }

    [Fact]
    public void ARCSET_verify_falseoutput(){

        ARCSET testArc = new ARCSET();
        ArcSetVerifier verifier = new ArcSetVerifier();
        Assert.False(verifier.verify(testArc,"{(3,2),(4,1)}"));
    }

    [Theory] //tests with default graph string Certificates of this test represent junk or empty data. 
    [InlineData("(({1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)","{(2,4)}")]
    public void ARCSET_verify_theory_true(string ARCSET_Instance, string testCertificate){
        ARCSET testArc = new ARCSET(ARCSET_Instance);
        ArcSetVerifier verifier = new ArcSetVerifier();
        bool isStillArcset = verifier.verify(testArc, testCertificate);
        Assert.True(isStillArcset);
    }

    [Theory] //tests with default graph string and various certificates, this shows that certificates can be accepted in many formats. (false case)
    [InlineData("(({1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)","{(3,2),(4,1)}")]
    [InlineData("(({1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)","{(4,1),(1,2)}")]
    [InlineData("(({1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)","{(3,2) (4,1)}")]
    [InlineData("(({1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)","{(4,1),(3,2)}")]
    [InlineData("(({1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)"," ")]
    [InlineData("(({1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)","{(4,2)}")]
     public void ARCSET_verify_theory_false(string ARCSET_Instance, string testCertificate){
        ARCSET testArc = new ARCSET(ARCSET_Instance);
        ArcSetVerifier verifier = new ArcSetVerifier();
        bool isStillArcset = verifier.verify(testArc, testCertificate);
        Assert.False(isStillArcset);
    }


    [Fact]
    public void ARCSET_solve(){
        ARCSET testArc = new ARCSET();
        ArcSetBruteForce solver = testArc.defaultSolver;
        string solvedString = solver.solve(testArc);
        Assert.Equal("{(2,4)}", solvedString);
    }


    [Theory]
    //default instance test. 
    [InlineData("(({a,b,c,d,e,f,g},{{a,b},{a,c},{c,d},{c,e},{d,f},{e,f},{e,g}}),3)","(({a0,a1,b0,b1,c0,c1,d0,d1,e0,e1,f0,f1,g0,g1},{(a0,a1),(a1,b0),(a1,c0),(b0,b1),(b1,a0),(c0,c1),(c1,a0),(c1,d0),(c1,e0),(d0,d1),(d1,c0),(d1,f0),(e0,e1),(e1,c0),(e1,f0),(e1,g0),(f0,f1),(f1,d0),(f1,e0),(g0,g1),(g1,e0)}),3)")]
    public void Vertex_To_Arcset_Reduction(string vertexInstance,string expectedArcsetInstance){
        VERTEXCOVER testVCover = new VERTEXCOVER(vertexInstance);
        LawlerKarp reduction = new LawlerKarp(testVCover);
        ARCSET reducedToArcsetInstance = reduction.reduce();
        string arcInstance = reducedToArcsetInstance.instance;
        Assert.Equal(new UtilCollection(expectedArcsetInstance), new UtilCollection(arcInstance));
    }

}