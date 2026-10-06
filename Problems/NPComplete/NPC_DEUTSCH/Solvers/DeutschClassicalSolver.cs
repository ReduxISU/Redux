using API.Interfaces;

namespace API.Problems.NPComplete.NPC_DEUTSCH.Solvers;

class DeutschClassicalSolver : ISolver<DEUTSCH> {

    // --- Fields ---
    public string solverName { get; } = "Deutsch Classical";
    public string solverDefinition { get; } = "This is a classical solver for the Deutsch Problem which simply tries both inputs to f(x) and computes whether they are the same (constant) or different (balanced)";
    public string source { get; } = "Deutsch, David. 1985. Quantum theory, the Church-Turing principle and the universal quantum computer. Proc. R. Soc. Lond. A40097-117";
    public string[] contributors { get; } = { "Jason L. Wright" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Classical baseline paired with DeutschQuantumSolver; no SolverType
    // bucket fits so solverType stays Unclassified per user decision. O(1) -- Deutsch's problem is
    // a trivial fixed 1-bit input, so even the classical solver is constant time.
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // solve() calls problem.Func() exactly twice (once for each of the two possible 1-bit inputs).
    public string complexity { get; } = "O(1)";

    // --- Methods Including Constructors ---
    public DeutschClassicalSolver() { }

    public string solve(DEUTSCH problem) {
        if (problem.Func(false) == problem.Func(true))
            return "constant";
        return "balanced";
    }
}
