using Xunit;
using API.Problems.NPComplete.NPC_DIRECTEDHAMILTONIAN;
using API.Problems.NPComplete.NPC_DIRECTEDHAMILTONIAN.Verifiers;

namespace redux_tests;

#pragma warning disable CS1591

public class DIRECTEDHAMILTONIAN_Tests {
    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void DIRECTEDHAMILTONIAN_Instance_Format_Described() {
        DIRECTEDHAMILTONIAN problem = new DIRECTEDHAMILTONIAN();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("N,E", problem.instanceFormat);
    }

    [Fact]
    public void DIRECTEDHAMILTONIAN_Certificate_Format_Described() {
        DIRECTEDHAMILTONIAN problem = new DIRECTEDHAMILTONIAN();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("Hamiltonian cycle", problem.certificateFormat);
    }

    [Fact]
    public void DIRECTEDHAMILTONIAN_Certificate_Format_Example_Is_Actually_Valid() {
        // The example quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        DIRECTEDHAMILTONIAN problem = new DIRECTEDHAMILTONIAN();
        DirectedHamiltonianVerifier verifier = new DirectedHamiltonianVerifier();
        Assert.True(verifier.verify(problem, "{2,1,3,5,4,2}"));
    }
}
