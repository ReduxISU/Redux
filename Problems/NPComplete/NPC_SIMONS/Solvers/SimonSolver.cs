using API.Interfaces;

namespace API.Problems.NPComplete.NPC_SIMON.Solvers;

class SimonSolver : ISolver<SIMON>
{

    // --- Fields ---
    public string solverName {get;} = "ProblemSolver";
    public string solverDefinition {get;} = "TODO";
    public string source {get;} = "TODO";
    public string[] contributors {get;} = { "Eric Hill", "Paul Gilbreath", "Max Gruenwoldt", "Alex Svancara" };

    // --- Methods Including Constructors ---
    public SimonSolver() { }

    public string solve(SIMON problem){
        // TODO: implement {SOLVER} for {PROBLEM}
        return "{}";
    }
}
