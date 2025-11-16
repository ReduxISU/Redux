using API.Interfaces;

namespace API.Problems.NPComplete.NPC_DEUTSCH.Solvers;
class DeutschClassicalSolver : ISolver<DEUTSCH> {

    // --- Fields ---
    public string solverName {get;} = "Deutsch Problem Classical Solver";
    public string solverDefinition {get;} = "This is a classical solver for the Deutsch Problem";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Jason L. Wright" };

    // --- Methods Including Constructors ---
    public DeutschClassicalSolver() {}

    public string solve(DEUTSCH problem) {
        if (problem.Func(false) == problem.Func(true))
            return "constant";
        return "balanced";
    }
}
