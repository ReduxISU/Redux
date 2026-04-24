using API.Interfaces;

namespace API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Solvers;
class LosslessDataCompressionSolver : ISolver<LOSSLESSDATACOMPRESSION> {

    // --- Fields ---
    public string solverName {get;} = "lossless data compression Solver";
    public string solverDefinition {get;} = "TODO";
    public string source {get;} = "TODO";
    public string[] contributors {get;} = { "TODO" };
    public bool timerHasExpired { get; set; }

    // --- Methods Including Constructors ---
    public LosslessDataCompressionSolver() {}

    public string solve(LOSSLESSDATACOMPRESSION problem){
        // TODO: implement lossless data compression Solver for LOSSLESSDATACOMPRESSION

        // make sure to check timerHasExpired occasionally, and return if it has
        if (timerHasExpired)
            return "timeout";

        return "{}";
    }
}
