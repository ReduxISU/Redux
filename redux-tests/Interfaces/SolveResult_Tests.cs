using System.Text.Json;
using API.Interfaces;
using API.Problems.NPComplete.NPC_PRIMEFACTOR.Solvers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class SolveResult_Tests {
    [Fact]
    public void SolveDetailed_Defaults_To_Unclassified_With_Raw_Output_For_Solvers_Not_Yet_Updated() {
        ISolver solver = new PrimeFactorSolver();
        SolveResult result = solver.solveDetailed("12");
        Assert.Equal(SolveStatus.Unclassified, result.status);
        Assert.Equal("(2,2,3)", result.certificate);
        Assert.Equal("", result.message);
    }

    [Fact]
    public void SolveResult_Serializes_Status_As_A_Name_And_Null_Certificate_As_Null() {
        // The shape an endpoint or MCP tool will see: the status by name, never its number,
        // and no certificate at all (rather than "{}") when there is none.
        string json = JsonSerializer.Serialize(SolveResult.NoSolution("No run accepts the input."));
        Assert.Equal("{\"status\":\"NoSolution\",\"certificate\":null,\"message\":\"No run accepts the input.\"}", json);
    }
}
