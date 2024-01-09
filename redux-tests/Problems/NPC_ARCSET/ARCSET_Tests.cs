//ArcsetTests.cs
using Xunit;
using API.Problems.NPComplete.NPC_ARCSET;
using API.Interfaces.Graphs;
using API.Problems.NPComplete.NPC_VERTEXCOVER.ReduceTo.NPC_ARCSET;
using API.Problems.NPComplete.NPC_ARCSET.Verifiers;
using API.Problems.NPComplete.NPC_ARCSET.Solvers;
using API.Problems.NPComplete.NPC_VERTEXCOVER;
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

        string testValue = "";
        ARCSET testingArc = new ARCSET();
        ArcsetGraph testGraph = testingArc.directedGraph;
        Assert.Equal(testingArc.instance, testGraph.ToString()); //Tests that the arcset instance string is equal to its generated graph string
        Assert.Equal(testingArc.defaultInstance, testGraph.ToString()); //Bonus test that ensures the default instance and the current instance are the same. 
        Assert.Equal( "(({1,2,3,4},{(1,2),(2,4),(3,2),(4,1),(4,3)}),1)",testingArc.defaultInstance); //tests default instance
    }

    [Fact]
     public void ARCSETGraph_Custom_Instance(){

        string testValue = "";
        ARCSET testingArc = new ARCSET("(({1,2,3,4,5},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)");
        ArcsetGraph testGraph = testingArc.directedGraph;
        Assert.Equal(testingArc.instance, testGraph.ToString()); //Tests that the arcset instance string is equal to its generated graph string
        Assert.Equal("(({1,2,3,4,5},{(1,2),(2,4),(3,2),(4,1),(4,3)}),1)",testingArc.instance); //
    }

    [Fact]
    //This test tests a basic DFS, but we also need searches that show how the dfs picks order of nodes to travel down. 
    public void ARCSETGraph_DFS(){
        string testValue = "";
        ARCSET testingArc = new ARCSET("(({1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)");
        ArcsetGraph testingGraph = testingArc.directedGraph;
        List<Edge> eList = testingGraph.DFS();
        Node edge1_node1 = new Node("4");
        Node edge1_node2 = new Node("1");
        Edge testEdge1 = new Edge(edge1_node1,edge1_node2 );

        //We know that this test will pass because the starting position of the dfs is the first node in node list, and there are no choices for the dfs to choose left or right.
        //More sophisticated tests will test how the dfs makes a choice between two paths. 
        Assert.Equal(testEdge1.directedString(), eList[1].directedString());

        Node edge2_node1 = new Node("3");
        Node edge2_node2 = new Node("2");
        Edge testEdge2 = new Edge(edge2_node1, edge2_node2);
        Assert.Equal(testEdge2.directedString(), eList[0].directedString());
    }


    [Fact]
    public void ARCSET_verify_falseoutput(){

        ARCSET testArc = new ARCSET();
        AlexArcsetVerifier verifier = new AlexArcsetVerifier();
        Assert.False(verifier.verify(testArc,"(3,2),(4,1)"));
    }

    [Theory] //tests with default graph string Certificates of this test represent junk or empty data. 
    [InlineData("(({1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)","(2,4)")]
    [InlineData("(({1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)","{(2,4)}")]
    public void ARCSET_verify_theory_true(string ARCSET_Instance, string testCertificate){
        ARCSET testArc = new ARCSET(ARCSET_Instance);
        AlexArcsetVerifier verifier = new AlexArcsetVerifier();
        bool isStillArcset = verifier.verify(testArc, testCertificate);
        Assert.True(isStillArcset);
    }

    [Theory] //tests with default graph string and various certificates, this shows that certificates can be accepted in many formats. (false case)
    [InlineData("(({1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)","(3,2),(4,1)")]
    [InlineData("(({1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)","(4,1),(1,2)")]
    [InlineData("(({1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)","(3,2) (4,1)")]
    [InlineData("(({1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)","{(4,1),(3,2)}")]
    [InlineData("(({1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)"," ")]
    [InlineData("(({1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)}),1)","(4,2)")]
     public void ARCSET_verify_theory_false(string ARCSET_Instance, string testCertificate){
        ARCSET testArc = new ARCSET(ARCSET_Instance);
        AlexArcsetVerifier verifier = new AlexArcsetVerifier();
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
        Assert.Equal(expectedArcsetInstance, arcInstance);
    }

}