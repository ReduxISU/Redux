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

    // --- GraphColoringBruteForce ---

    [Fact]
    public void BruteForceSolver_KTooSmall_ReturnsEmpty() {
        // A complete graph on 4 nodes needs 4 colors; 2 is infeasible.
        GRAPHCOLORING problem = new GRAPHCOLORING(
            "(({a,b,c,d},{{a,b},{a,c},{a,d},{b,c},{b,d},{c,d}}),2)");
        GraphColoringBruteForce solver = new GraphColoringBruteForce();
        string certificate = solver.solve(problem);
        Assert.Equal("{}", certificate);
    }

    [Fact]
    public void BruteForceSolver_BipartiteGraph_TwoColorsSuffice() {
        GRAPHCOLORING problem = new GRAPHCOLORING(
            "(({a,b,c,d},{{a,c},{a,d},{b,c},{b,d}}),2)");
        GraphColoringBruteForce solver = new GraphColoringBruteForce();
        GraphColoringVerifier verifier = new GraphColoringVerifier();
        string certificate = solver.solve(problem);
        Assert.True(verifier.verify(problem, certificate), $"Solver output failed verifier for: {problem.instance}");
    }

    [Fact]
    public void BruteForceSolver_Triangle_NeedsThreeColors() {
        GRAPHCOLORING problem = new GRAPHCOLORING(
            "(({a,b,c},{{a,b},{b,c},{a,c}}),3)");
        GraphColoringBruteForce solver = new GraphColoringBruteForce();
        GraphColoringVerifier verifier = new GraphColoringVerifier();
        string certificate = solver.solve(problem);
        Assert.True(verifier.verify(problem, certificate), $"Solver output failed verifier for: {problem.instance}");
    }

    [Fact]
    public void BruteForceSolver_KGreaterThanNodeCount_ClampsToNodeCount() {
        // K=5 requested on a 2-node, edge-free graph: solve() clamps numColors down to
        // nodes.Count via the "if (gColor.K > gColor.nodes.Count)" guard, instead of searching
        // over 6-value digits that could never actually be used.
        GRAPHCOLORING problem = new GRAPHCOLORING("(({a,b},{}),5)");
        GraphColoringBruteForce solver = new GraphColoringBruteForce();
        GraphColoringVerifier verifier = new GraphColoringVerifier();
        string certificate = solver.solve(problem);
        Assert.True(verifier.verify(problem, certificate), $"Solver output failed verifier for: {problem.instance}");
    }

    [Fact]
    public void BruteForceSolver_EmptyGraph_ReturnsEmptyCertificate() {
        GRAPHCOLORING problem = new GRAPHCOLORING("(({},{}),1)");
        GraphColoringBruteForce solver = new GraphColoringBruteForce();
        string certificate = solver.solve(problem);
        Assert.Equal("{}", certificate);
    }

    [Fact(Skip = "BUG: GraphColoringBruteForce.solve() never even tries the trivial 1-coloring. Its " +
        "while-loop guard (\"binary.Count(n => n == numColors-1) < gColor.nodes.Count\") is already " +
        "false before the loop's first iteration whenever numColors==1, because every digit starts at " +
        "0 and numColors-1 is also 0 -- so all digits already equal numColors-1 at the outset. The loop " +
        "body (where BinaryToCertificate builds a certificate and the verifier is consulted) therefore " +
        "never runs at all, and solve() falls straight through to \"return {}\". This means any K=1 " +
        "instance -- however trivially 1-colorable, e.g. a single edge-free node -- incorrectly reports " +
        "no solution. See GraphColoringBruteForce.solve().")]
    public void BruteForceSolver_KEqualsOne_TriviallyColorableGraph_IncorrectlyReturnsEmpty() {
        GRAPHCOLORING problem = new GRAPHCOLORING("(({a},{}),1)");
        GraphColoringBruteForce solver = new GraphColoringBruteForce();
        GraphColoringVerifier verifier = new GraphColoringVerifier();

        string certificate = solver.solve(problem);

        Assert.True(verifier.verify(problem, certificate), $"Solver output failed verifier for: {problem.instance}");
    }
}