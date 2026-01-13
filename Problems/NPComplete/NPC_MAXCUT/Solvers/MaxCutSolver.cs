using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_MAXCUT.Solvers;
class MaxCutSolver : ISolver<MAXCUT> {

    // --- Fields ---
    public string solverName {get;} = "Max Cut Solver";
    public string solverDefinition {get;} = "TODO";
    public string source {get;} = "TODO";
    public string[] contributors {get;} = {"Max Gruenwoldt", "Eric Hill"};
    public bool timerHasExpired { get; set; }

    public MaxCutSolver() { }

    public string solve(MAXCUT maxcut){
        return "TODO";
    }
}

   