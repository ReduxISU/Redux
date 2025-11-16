using API.Interfaces;

namespace API.Problems.NPComplete.NPC_DEUTSCHJOZSA.Solvers;
class DeutschJozsaSolver : ISolver<DEUTSCHJOZSA> {

    // --- Fields ---
    public string solverName {get;} = "ProblemSolver";
    public string solverDefinition {get;} = "TODO";
    public string source {get;} = "TODO";
    public string[] contributors {get;} = { "Eric Hill", "Paul Gilbreath", "Max Gruenwoldt", "Alex Svancara" };

    // --- Methods Including Constructors ---
    public DeutschJozsaSolver() {}

    public string solve(DEUTSCHJOZSA problem){
        // TODO: implement {SOLVER} for {PROBLEM}
        return "{}";
    }
}
