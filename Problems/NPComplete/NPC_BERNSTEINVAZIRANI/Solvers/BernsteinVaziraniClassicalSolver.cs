using API.Interfaces;
using SPADE;

namespace API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI.Solvers;
class BernsteinVaziraniClassicalSolver : ISolver<BERNSTEINVAZIRANI> {

    // --- Fields ---
    public string solverName {get;} = "Bernstein Vazirani Classical Solver";
    public string solverDefinition { get; } = "This is a classical verifier for the Bernstein-Vazirani problem which runs in O(n) time.";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Jason L. Wright" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Classical baseline paired with BernsteinVaziraniQuantumSolver; no
    // SolverType bucket fits so solverType stays Unclassified per user decision. O(n) classical
    // oracle queries (n = number of bits) -- polynomial, contrast with the quantum solver's O(1).
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // solve() loops i from NBits-1 down to 0, making exactly one Func() oracle query per bit.
    public string complexity { get; } = "O(n)";

    // --- Methods Including Constructors ---
    public BernsteinVaziraniClassicalSolver() {}

    public string solve(BERNSTEINVAZIRANI problem) {
        UtilCollection result = new("()");
        for (int i = problem.NBits - 1; i >= 0; i--)
            result.Add(new UtilCollection(problem.Func(1 << i) ? "1" : "0"));
        return result.ToString();
    }
}
