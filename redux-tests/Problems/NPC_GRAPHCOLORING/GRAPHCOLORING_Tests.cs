// GRAPHCOLORING_Tests.cs
// Saurav Bhusal and Pramesh Shah
// sauravbhusal@isu.edu, prameshshah@isu.edu

#pragma warning disable CS1591
using Xunit;
using API.Problems.NPComplete.NPC_GRAPHCOLORING;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.Verifiers;

namespace API.Tests.Problems.NPC_GRAPHCOLORING;

public class GRAPHCOLORING_Tests {

    // Test 1: Default Redux instance
    [Fact]
    public void GreedySolver_DefaultInstance_ReturnsValidCertificate() {
        GRAPHCOLORING problem = new GRAPHCOLORING();
        GraphColoringGreedy solver = new GraphColoringGreedy();
        GraphColoringVerifier verifier = new GraphColoringVerifier();
        string certificate = solver.solve(problem);
        Assert.True(verifier.verify(problem, certificate));
    }

    // Test 2: Single node
    [Fact]
    public void GreedySolver_SingleNode_ReturnsValidCertificate() {
        GRAPHCOLORING problem = new GRAPHCOLORING("(({a},{}),1)");
        GraphColoringGreedy solver = new GraphColoringGreedy();
        GraphColoringVerifier verifier = new GraphColoringVerifier();
        string certificate = solver.solve(problem);
        Assert.True(verifier.verify(problem, certificate));
    }

    // Test 3: K too small returns empty
    [Fact]
    public void GreedySolver_KTooSmall_ReturnsEmpty() {
        GRAPHCOLORING problem = new GRAPHCOLORING(
            "(({a,b,c,d},{{a,b},{a,c},{a,d},{b,c},{b,d},{c,d}}),2)");
        GraphColoringGreedy solver = new GraphColoringGreedy();
        string certificate = solver.solve(problem);
        Assert.Equal("{}", certificate);
    }

    // Test 4: Bipartite graph
    [Fact]
    public void GreedySolver_BipartiteGraph_UsesTwoColors() {
        GRAPHCOLORING problem = new GRAPHCOLORING(
            "(({a,b,c,d},{{a,c},{a,d},{b,c},{b,d}}),2)");
        GraphColoringGreedy solver = new GraphColoringGreedy();
        GraphColoringVerifier verifier = new GraphColoringVerifier();
        string certificate = solver.solve(problem);
        Assert.True(verifier.verify(problem, certificate));
    }

    // Test 5: No edges
    [Fact]
    public void GreedySolver_NoEdges_ReturnsValidCertificate() {
        GRAPHCOLORING problem = new GRAPHCOLORING("(({a,b,c},{}),1)");
        GraphColoringGreedy solver = new GraphColoringGreedy();
        GraphColoringVerifier verifier = new GraphColoringVerifier();
        string certificate = solver.solve(problem);
        Assert.True(verifier.verify(problem, certificate));
    }

    // Test 6: Triangle needs 3 colors
    [Fact]
    public void GreedySolver_Triangle_NeedsThreeColors() {
        GRAPHCOLORING problem = new GRAPHCOLORING(
            "(({a,b,c},{{a,b},{b,c},{a,c}}),3)");
        GraphColoringGreedy solver = new GraphColoringGreedy();
        GraphColoringVerifier verifier = new GraphColoringVerifier();
        string certificate = solver.solve(problem);
        Assert.True(verifier.verify(problem, certificate));
    }

    // Test 7: Two nodes one edge
    [Fact]
    public void GreedySolver_TwoNodes_ReturnsValidCertificate() {
        GRAPHCOLORING problem = new GRAPHCOLORING("(({a,b},{{a,b}}),2)");
        GraphColoringGreedy solver = new GraphColoringGreedy();
        GraphColoringVerifier verifier = new GraphColoringVerifier();
        string certificate = solver.solve(problem);
        Assert.True(verifier.verify(problem, certificate));
    }

    // Test 8: Complete graph K4
    [Fact]
    public void GreedySolver_CompleteGraphK4_ReturnsFourColors() {
        GRAPHCOLORING problem = new GRAPHCOLORING(
            "(({a,b,c,d},{{a,b},{a,c},{a,d},{b,c},{b,d},{c,d}}),4)");
        GraphColoringGreedy solver = new GraphColoringGreedy();
        GraphColoringVerifier verifier = new GraphColoringVerifier();
        string certificate = solver.solve(problem);
        Assert.True(verifier.verify(problem, certificate));
    }

    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void GRAPHCOLORING_Instance_Format_Described() {
        GRAPHCOLORING problem = new GRAPHCOLORING();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("N,E),K", problem.instanceFormat);
    }

    [Fact]
    public void GRAPHCOLORING_Certificate_Format_Described() {
        GRAPHCOLORING problem = new GRAPHCOLORING();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("color classes", problem.certificateFormat);
    }

    [Fact]
    public void GRAPHCOLORING_Certificate_Format_Example_Is_Actually_Valid() {
        // The example quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        GRAPHCOLORING problem = new GRAPHCOLORING();
        GraphColoringVerifier verifier = new GraphColoringVerifier();
        Assert.True(verifier.verify(problem, "{{a},{b,d,f,h},{c,e,g,i}}"));
    }
}