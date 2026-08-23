using Xunit;
using API.Problems.NPComplete.NPC_HAMILTONIAN;
using API.Problems.NPComplete.NPC_HAMILTONIAN.Verifiers;

namespace redux_tests;

#pragma warning disable CS1591

public class HAMILTONIAN_Tests
{
    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void HAMILTONIAN_Instance_Format_Described()
    {
        HAMILTONIAN problem = new HAMILTONIAN();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("N,E", problem.instanceFormat);
    }

    [Fact]
    public void HAMILTONIAN_Certificate_Format_Described()
    {
        HAMILTONIAN problem = new HAMILTONIAN();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("Hamiltonian cycle", problem.certificateFormat);
    }

    [Fact]
    public void HAMILTONIAN_Certificate_Format_Example_Is_Actually_Valid()
    {
        // The example quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        HAMILTONIAN problem = new HAMILTONIAN();
        HamiltonianVerifier verifier = new HamiltonianVerifier();
        Assert.True(verifier.verify(problem, "{1,2,4,5,3}"));
    }
}
