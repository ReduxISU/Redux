using API.Interfaces;

namespace API.Problems.NPComplete.NPC_DEUTSCH.Solvers;
class DeutschSolver : ISolver<DEUTSCH> {

    // --- Fields ---
    public string solverName {get;} = "ProblemSolver";
    public string solverDefinition {get;} = "TODO";
    public string source {get;} = "TODO";
    public string[] contributors {get;} = { "Eric Hill", "Paul Gilbreath", "Max Gruenwoldt", "Alex Svancara" };

    // --- Methods Including Constructors ---
    public DeutschSolver() {}

    public string solve(DEUTSCH problem){
        // TODO: implement {SOLVER} for {PROBLEM}
        return "{}";
    }
}
