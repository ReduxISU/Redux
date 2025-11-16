using API.Interfaces;

namespace API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI.Solvers;
class BernsteinVaziraniSolver : ISolver<BERNSTEINVAZIRANI> {

    // --- Fields ---
    public string solverName {get;} = "ProblemSolver";
    public string solverDefinition {get;} = "TODO";
    public string source {get;} = "TODO";
    public string[] contributors {get;} = { "Eric Hill", "Paul Gilbreath", "Max Gruenwoldt", "Alex Svancara" };

    // --- Methods Including Constructors ---
    public BernsteinVaziraniSolver() {}

    public string solve(BERNSTEINVAZIRANI problem){
        // TODO: implement {SOLVER} for {PROBLEM}
        return "{}";
    }
}
