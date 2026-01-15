using API.Interfaces;

namespace API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI.Solvers;
class BernsteinVaziraniClassicalSolver : ISolver<BERNSTEINVAZIRANI> {

    // --- Fields ---
    public string solverName {get;} = "Bernstein Vazirani Classical Solver";
    public string solverDefinition { get; } = "This is a classical verifier for the Bernstein-Vazirani problem which runs in O(n) time.";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Jason L. Wright" };
    public bool timerHasExpired { get; set; }

    // --- Methods Including Constructors ---
    public BernsteinVaziraniClassicalSolver() {}

    public string solve(BERNSTEINVAZIRANI problem) {
        string result = "";
        for (int i = problem.NBits - 1; i >= 0; i--) {
            result += problem.Func(1 << i) ? "1" : "0";
        }
        return result;
    }
}
