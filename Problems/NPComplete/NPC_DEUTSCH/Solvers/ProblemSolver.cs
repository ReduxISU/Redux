using API.Interfaces;

namespace API.Problems.NPComplete.NPC_DEUTSCH.Solvers;
class ProblemSolver : ISolver<DEUTSCH> {

    // --- Fields ---
    public string solverName {get;} = "{SOLVER}";
    public string solverDefinition {get;} = "TODO";
    public string source {get;} = "TODO";
    public string[] contributers {get;} = { "TODO" };

    // --- Methods Including Constructors ---
    public ProblemSolver() {}

    public string solve(DEUTSCH problem){
        // TODO: implement {SOLVER} for {PROBLEM}
        return "{}";
    }
}
