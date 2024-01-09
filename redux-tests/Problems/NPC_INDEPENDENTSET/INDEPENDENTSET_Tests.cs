using Xunit;
using API.Problems.NPComplete.NPC_INDEPENDENTSET;
using API.Problems.NPComplete.NPC_INDEPENDENTSET.Verifiers;
using API.Problems.NPComplete.NPC_INDEPENDENTSET.Solvers;
using API.Problems.NPComplete.NPC_INDEPENDENTSET.ReduceTo.NPC_CLIQUE;

namespace redux_tests;
#pragma warning disable CS1591

public class INDPENDENTSET_Tests {

   [Fact]
   public void INDEPENDENTSET_Default_Instantiation() {
    INDEPENDENTSET independentset = new INDEPENDENTSET();
    IndependentSetGraph graph = independentset.independentSetAsGraph;
    Assert.Equal(independentset.instance, graph.ToString());
    Assert.Equal(independentset.defaultInstance, graph.ToString());
    Assert.Equal("(({a,b,c,d,e,f,g,h,i},{{a,b},{b,a},{b,c},{c,a},{a,c},{c,b},{a,d},{d,a},{d,e},{e,a},{a,e},{e,d},{a,f},{f,a},{f,g},{g,a},{a,g},{g,f},{a,h},{h,a},{h,i},{i,a},{a,i},{i,h}}),4)", independentset.defaultInstance);
   } 

   [Fact]
   public void INDEPENDENTSET_Custom_Instantiation() {
    INDEPENDENTSET independentset = new INDEPENDENTSET("(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),2)");
    IndependentSetGraph graph = independentset.independentSetAsGraph;
    Assert.Equal(independentset.instance, graph.ToString());
    Assert.Equal("(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),2)", independentset.instance);
   }

   [Theory] //Tests independent set verifier with a few certificates

   [InlineData("(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),2)", "{1,3}", true)]
   [InlineData("(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),2)", "{1,4}", false)]
   [InlineData("(({a,b,c,d,e,f,g,h,i},{{a,b},{b,a},{b,c},{c,a},{a,c},{c,b},{a,d},{d,a},{d,e},{e,a},{a,e},{e,d},{a,f},{f,a},{f,g},{g,a},{a,g},{g,f},{a,h},{h,a},{h,i},{i,a},{a,i},{i,h}}),4)", "{b,d,f,h}", true)]
   [InlineData("(({a,b,c,d,e,f,g,h,i},{{a,b},{b,a},{b,c},{c,a},{a,c},{c,b},{a,d},{d,a},{d,e},{e,a},{a,e},{e,d},{a,f},{f,a},{f,g},{g,a},{a,g},{g,f},{a,h},{h,a},{h,i},{i,a},{a,i},{i,h}}),4)", "{c,i,g,e}", true)]
   [InlineData("(({a,b,c,d,e,f,g,h,i},{{a,b},{b,a},{b,c},{c,a},{a,c},{c,b},{a,d},{d,a},{d,e},{e,a},{a,e},{e,d},{a,f},{f,a},{f,g},{g,a},{a,g},{g,f},{a,h},{h,a},{h,i},{i,a},{a,i},{i,h}}),4)", "{b,c,f,h}", false)]
    public void INDEPENDENTSET_verifier(string instance, string certificate, bool expected) {
        INDEPENDENTSET independentset = new INDEPENDENTSET(instance);
        IndependentSetVerifier verifier = new IndependentSetVerifier();
        bool result = verifier.verify(independentset, certificate);
        Assert.Equal(expected, result);

    }


    [Theory] //tests solver
    [InlineData("(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),2)", "{1,3}")]
    [InlineData("(({a,b,c,d,e,f,g,h,i},{{a,b},{b,a},{b,c},{c,a},{a,c},{c,b},{a,d},{d,a},{d,e},{e,a},{a,e},{e,d},{a,f},{f,a},{f,g},{g,a},{a,g},{g,f},{a,h},{h,a},{h,i},{i,a},{a,i},{i,h}}),4)", "{b,d,f,h}")]
    public void INDEPENDENTSET_solver(string instance, string certificate) {
        INDEPENDENTSET independentset = new INDEPENDENTSET(instance);
        IndependentSetBruteForce solver = independentset.defaultSolver;
        string solvedString = solver.solve(independentset);
        Assert.Equal(certificate, solvedString);
    }

    [Theory] //tests reduction to clique
    [InlineData("(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),2)", "(({1,2,3,4},{{1,3}}),2)")]
    [InlineData("(({a,b,c,d,e,f,g,h,i},{{a,b},{b,a},{b,c},{c,a},{a,c},{c,b},{a,d},{d,a},{d,e},{e,a},{a,e},{e,d},{a,f},{f,a},{f,g},{g,a},{a,g},{g,f},{a,h},{h,a},{h,i},{i,a},{a,i},{i,h}}),4)", "(({a,b,c,d,e,f,g,h,i},{{b,d},{b,e},{b,f},{b,g},{b,h},{b,i},{c,d},{c,e},{c,f},{c,g},{c,h},{c,i},{d,f},{d,g},{d,h},{d,i},{e,f},{e,g},{e,h},{e,i},{f,h},{f,i},{g,h},{g,i}}),4)")]
    public void INDPENDENTSET_to_clique(string instance, string result) {
        INDEPENDENTSET independentset = new INDEPENDENTSET(instance);
        CliqueReduction reduction = new CliqueReduction(independentset);
        Assert.Equal(result, reduction.reductionTo.instance);
    }
}




