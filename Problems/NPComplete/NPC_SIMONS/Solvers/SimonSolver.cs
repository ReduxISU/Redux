using API.Interfaces;

namespace API.Problems.NPComplete.NPC_SIMON.Solvers;

class SIMONSolver : ISolver<SIMON> {

    // --- Fields ---
    public string solverName {get;} = "ProblemSolver";
    public string solverDefinition {get;} = "TODO";
    public string source {get;} = "TODO";
    public string[] contributors {get;} = { "Eric Hill", "Paul Gilbreath", "Max Gruenwoldt", "Alex Svancara" };
    public bool timerHasExpired { get; set; }

    // --- Methods Including Constructors ---
    public SIMONSolver() {}

    public string solve(SIMON problem){
        // TODO: implement {SOLVER} for {PROBLEM}
        return "{}";
    }
}
