using Xunit;
using API.Problems.NPComplete.NPC_HAMILTONIAN;
using API.Problems.NPComplete.NPC_HAMILTONIAN.Verifiers;
using API.Problems.NPComplete.NPC_HAMILTONIAN.Solvers;
using API.Interfaces;
using SPADE;

namespace redux_tests;
#pragma warning disable CS1591

public class HAMILTONIAN_Tests {

    [Fact]
    public void HAMILTONIAN_Default_Instantiation() {
        HAMILTONIAN hamiltonian = new HAMILTONIAN();
        UtilCollectionGraph graph = hamiltonian.graph;
        Assert.Equal(hamiltonian.instance, graph.ToString());
        Assert.Equal(hamiltonian.defaultInstance, graph.ToString());
    }

    [Fact]
    public void HAMILTONIAN_Custom_Instantiation() {
        HAMILTONIAN hamiltonian = new HAMILTONIAN("({1,2,3,4},{{1,2},{2,3},{3,4},{4,1}})");
        UtilCollectionGraph graph = hamiltonian.graph;
        Assert.Equal(hamiltonian.instance, graph.ToString());
        Assert.Equal("({1,2,3,4},{{1,2},{2,3},{3,4},{4,1}})", hamiltonian.instance);
    }

    [Theory] // Tests Hamiltonian verifier with a few certificates
    // Default instance: ({1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}})
    [InlineData("({1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}})", "{1,3,5,4,2}", true)] // 1-3,3-5,5-4,4-2,2-1 all edges exist; visits every node once
    [InlineData("({1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}})", "{1,3,5,4,2,1}", true)] // same cycle with the starting node repeated at the end
    [InlineData("({1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}})", "{1,2,3,4,5}", false)] // 3-4 is not an edge
    [InlineData("({1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}})", "{1,3,5,4}", false)] // omits node 2 -- not every node visited
    public void HAMILTONIAN_verifier(string instance, string certificate, bool expected) {
        HAMILTONIAN hamiltonian = new HAMILTONIAN(instance);
        HamiltonianVerifier verifier = new HamiltonianVerifier();
        bool result = verifier.verify(hamiltonian, certificate);
        Assert.Equal(expected, result);
    }

    [Theory] // Tests the solver produces a cycle the verifier accepts
    [InlineData("({1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}})")]
    [InlineData("({1,2,3,4},{{1,2},{2,3},{3,4},{4,1}})")]
    public void HAMILTONIAN_solver(string instance) {
        HAMILTONIAN hamiltonian = new HAMILTONIAN(instance);
        HamiltonianBruteForce solver = hamiltonian.defaultSolver;
        string solvedString = solver.solve(hamiltonian);
        Assert.True(hamiltonian.defaultVerifier.verify(hamiltonian, solvedString));
    }
}
