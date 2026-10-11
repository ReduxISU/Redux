using Xunit;
using API.Problems.NPComplete.NPC_SIMON;
using API.Problems.NPComplete.NPC_SIMON.Verifiers;
using API.Problems.NPComplete.NPC_SIMON.Solvers;

namespace redux_tests;
#pragma warning disable CS1591

public class SIMON_tests {
    [Fact]
    public void SIMON_Default_Instantiation() {
        SIMON jobSeq = new SIMON();
        Assert.Equal("(5, 6, 5, 6, 3, 2, 3, 2)", jobSeq.instance);
        Assert.Equal("(5, 6, 5, 6, 3, 2, 3, 2)", jobSeq.defaultInstance);
    }

    [Fact]
    public void SIMON_Custom_Instantiation() {
        string instance = "(5,1,3,2,3,2,5,1)";
        var problem = new SIMON(instance);
        Assert.Equal(instance, problem.instance);
    }

    [Theory] //Tests independent set verifier with a few certificates
    [InlineData("(5,6,5,6,3,2,3,2)", "010", true)]
    [InlineData("(5,1,3,2,3,2,5,1)", "000", false)]
    [InlineData("(5,1,3,2,3,2,5,1)", "001", false)]
    [InlineData("(5,1,3,2,3,2,5,1)", "010", false)]
    [InlineData("(5,1,3,2,3,2,5,1)", "011", false)]
    [InlineData("(5,1,3,2,3,2,5,1)", "100", false)]
    [InlineData("(5,1,3,2,3,2,5,1)", "101", false)]
    [InlineData("(5,1,3,2,3,2,5,1)", "110", true)]
    [InlineData("(5,1,3,2,3,2,5,1)", "111", false)]
    public void SIMON_verifier(string instance, string certificate, bool expected) {
        var problem = new SIMON(instance);
        var verifier = new SimonVerifier();
        bool result = verifier.verify(problem, certificate);
        Assert.Equal(expected, result);

    }


    [Theory] //tests solver
    [InlineData("(5,6,5,6,3,2,3,2)", "010")]
    [InlineData("(5,1,3,2,3,2,5,1)", "110")]
    public void JOBSEQ_solver(string instance, string certificate) {
        var problem = new SIMON(instance);
        var solver = problem.defaultSolver;
        string solvedString = solver.solve(problem);
        Assert.Equal(certificate, solvedString);
    }

    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void SIMON_Instance_Format_Described() {
        var problem = new SIMON();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("2^n", problem.instanceFormat);
    }

    [Fact]
    public void SIMON_Certificate_Format_Described() {
        var problem = new SIMON();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("binary string", problem.certificateFormat);
    }

    [Fact]
    public void SIMON_Certificate_Format_Example_Is_Actually_Valid() {
        // The "Example: 010" quoted in certificateFormat must be a real,
        // verifiable certificate for defaultInstance — not just descriptive prose.
        var problem = new SIMON();
        var verifier = new SimonVerifier();
        Assert.True(verifier.verify(problem, SimonVerifier.CertificateExample));
    }

    // ---- #810 ----

    [Theory]
    [InlineData("(1,2,3,4)", true)]   // one-to-one: s = 0, which the format lets the certificate leave empty
    [InlineData("(1,1,2,2)", false)]  // s = 1, not 0
    [InlineData("(7)", true)]         // a one-entry table: the solver's answer is ""
    public void SIMON_verifier_EmptyCertificate_MeansSZero(string instance, bool expected) {
        Assert.Equal(expected, new SimonVerifier().verify(new SIMON(instance), ""));
    }

    [Fact]
    public void SIMON_solver_OneEntryTable_AnswerIsVerified() {
        var problem = new SIMON("(7)");
        Assert.True(new SimonVerifier().verify(problem, new SimonSolver().solve(problem)));
    }

    [Theory]
    [InlineData("(1,2,3)")]
    [InlineData("(1,1,2,2,3,3)")]
    [InlineData("()")]
    public void SIMON_TableNotAPowerOfTwoLong_IsAParseError(string instance) {
        Assert.Throws<API.Interfaces.ProblemParseException>(() => API.Interfaces.ParseGuard.CreateProblem<SIMON>(instance));
    }
}
