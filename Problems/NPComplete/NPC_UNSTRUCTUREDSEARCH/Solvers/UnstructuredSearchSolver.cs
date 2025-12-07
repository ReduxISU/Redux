using API.Interfaces;

namespace API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Solvers;
class UnstructuredSearchSolver : ISolver<UNSTRUCTUREDSEARCH> {

    // --- Fields ---
    public string solverName {get;} = "{SOLVER}";
    public string solverDefinition {get;} = "TODO";
    public string source {get;} = "TODO";
    public string[] contributors {get;} = { "Alex Svancara" };

    // --- Methods Including Constructors ---
    public UnstructuredSearchSolver() {}

    public string solve(UNSTRUCTUREDSEARCH problem){
        // TODO: implement {SOLVER} for {PROBLEM}
        return "{}";
    }
}
