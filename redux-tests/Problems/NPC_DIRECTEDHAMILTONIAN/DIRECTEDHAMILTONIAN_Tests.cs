using Xunit;
using API.Problems.NPComplete.NPC_DIRECTEDHAMILTONIAN;
using API.Problems.NPComplete.NPC_DIRECTEDHAMILTONIAN.Solvers;
using API.Problems.NPComplete.NPC_DIRECTEDHAMILTONIAN.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class DIRECTEDHAMILTONIAN_Tests {
    [Fact]
    public void DirectedHamiltonian_Default_Instantiation() {
        DIRECTEDHAMILTONIAN problem = new DIRECTEDHAMILTONIAN();
        Assert.Equal("({1,2,3,4,5},{(2,1),(1,3),(2,3),(3,5),(4,2),(5,4)})", problem.instance);
        Assert.Equal(problem.defaultInstance, problem.instance);
    }

    [Fact]
    public void DirectedHamiltonian_Custom_Instantiation() {
        string instance = "({1,2,3},{(1,2),(2,3),(3,1)})";
        DIRECTEDHAMILTONIAN problem = new DIRECTEDHAMILTONIAN(instance);
        Assert.Equal(instance, problem.instance);
    }

    // ----- Solver + Verifier round trip ----- //

    [Fact]
    public void DirectedHamiltonianBruteForce_Default_Instance_Verifies() {
        DIRECTEDHAMILTONIAN problem = new DIRECTEDHAMILTONIAN();
        string certificate = new DirectedHamiltonianBruteForce().solve(problem);
        Assert.True(new DirectedHamiltonianVerifier().verify(problem, certificate));
    }

    // ----- Regression: closing-edge check ----- //

    // Genuine Hamiltonian cycle 1->2->3->1, plus an extra edge 3->2 that a buggy
    // verifier (one that never checks the edge from the last certificate node back
    // to the first) could be fooled by.
    private const string ClosureExploitInstance = "({1,2,3},{(1,2),(2,3),(3,1),(3,2)})";

    [Fact]
    public void DirectedHamiltonianVerifier_RejectsCertificate_ThatNeverClosesBackToStart() {
        // Visits 1,2,3,2 via valid edges 1->2, 2->3, 3->2 -- but never returns to
        // node 1, so this is not actually a Hamiltonian cycle. A verifier that only
        // checks consecutive pairs in the certificate (and never the wraparound edge)
        // would incorrectly accept this, because every node still gets covered.
        DIRECTEDHAMILTONIAN problem = new DIRECTEDHAMILTONIAN(ClosureExploitInstance);
        Assert.False(new DirectedHamiltonianVerifier().verify(problem, "{1,2,3,2}"));
    }

    [Fact]
    public void DirectedHamiltonianVerifier_AcceptsGenuineCycle_WithTrailingRepeat() {
        // Solver convention: repeat the starting node at the end.
        DIRECTEDHAMILTONIAN problem = new DIRECTEDHAMILTONIAN(ClosureExploitInstance);
        Assert.True(new DirectedHamiltonianVerifier().verify(problem, "{1,2,3,1}"));
    }

    [Fact]
    public void DirectedHamiltonianVerifier_AcceptsGenuineCycle_WithoutTrailingRepeat() {
        // Same cycle, without the solver's trailing-repeat convention -- the fix's
        // wraparound logic must accept both forms.
        DIRECTEDHAMILTONIAN problem = new DIRECTEDHAMILTONIAN(ClosureExploitInstance);
        Assert.True(new DirectedHamiltonianVerifier().verify(problem, "{1,2,3}"));
    }

    // ----- Verifier: coverage check ----- //

    [Fact]
    public void DirectedHamiltonianVerifier_RejectsCertificate_ThatOmitsANode() {
        // Default instance {1,2,3,4,5} with edges (2,1),(1,3),(2,3),(3,5),(4,2),(5,4)
        // contains a genuine 4-cycle 2->3->5->4->2 that never touches node 1. Every
        // edge walked is valid and closes correctly, so only the coverage check
        // (node "1" is never removed from `check`) can catch this.
        DIRECTEDHAMILTONIAN problem = new DIRECTEDHAMILTONIAN();
        Assert.False(new DirectedHamiltonianVerifier().verify(problem, "{2,3,5,4}"));
    }

    // ----- Verifier: broken edge check ----- //

    [Fact]
    public void DirectedHamiltonianVerifier_RejectsCertificate_WithNonAdjacentEdge() {
        // Default instance edges: (2,1),(1,3),(2,3),(3,5),(4,2),(5,4).
        // {4,2,3,1,5,4}: 4->2 and 2->3 are valid, but 3->1 does not exist
        // (only 1->3 does) -- a broken edge in the middle of the walk, not at
        // the closing wraparound.
        DIRECTEDHAMILTONIAN problem = new DIRECTEDHAMILTONIAN();
        Assert.False(new DirectedHamiltonianVerifier().verify(problem, "{4,2,3,1,5,4}"));
    }
}
