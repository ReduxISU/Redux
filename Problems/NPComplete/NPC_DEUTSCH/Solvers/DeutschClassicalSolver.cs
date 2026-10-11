using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_DEUTSCH.Solvers;

// Incremental: each oracle call adds to what the solver knows about f, and none is taken back.
class DeutschClassicalSolver : ISolver<DEUTSCH, QuantumProgress>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "Deutsch Classical";
    public string solverDefinition { get; } = "This is a classical solver for the Deutsch Problem which simply tries both inputs to f(x) and computes whether they are the same (constant) or different (balanced)";
    public string source { get; } = "Deutsch, David. 1985. Quantum theory, the Church-Turing principle and the universal quantum computer. Proc. R. Soc. Lond. A40097-117";
    public string sourceFile { get; } = SourceFile.Path();
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

    public string solve(DEUTSCH problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // Steps (the Quantum Circuit picture, which this solver never runs): a Try per oracle call, f(0) then f(1), with the
    // oracle as the focus; an Accept with the answer; Done with the verifier's verdict.
    public string Solve(DEUTSCH problem, StepRecorder<QuantumProgress> rec) {
        string? oracle = QuantumSteps.Recording(rec) ? QuantumCircuits.Deutsch(problem.funcValues).OracleGateId : null;
        string[] Focus() => oracle == null ? [] : [oracle];
        var queries = new List<OracleQuery>();
        bool f0 = problem.Func(false);
        queries.Add(new OracleQuery(0, f0 ? 1 : 0));
        rec.Try(() => new QuantumProgress(queries: queries), () => $"Call the oracle with x = 0: f(0) = {Bit(f0)}.", Focus);
        bool f1 = problem.Func(true);
        queries.Add(new OracleQuery(1, f1 ? 1 : 0));
        rec.Try(() => new QuantumProgress(queries: queries), () => $"Call the oracle with x = 1: f(1) = {Bit(f1)}.", Focus);

        string answer = f0 == f1 ? "constant" : "balanced";
        rec.Accept(() => new QuantumProgress(queries: queries, result: answer), () => (f0 == f1
            ? $"f(0) = f(1) = {Bit(f0)}, so f is constant."
            : $"f(0) = {Bit(f0)} and f(1) = {Bit(f1)} differ, so f is balanced.") + " That took 2 oracle calls; Deutsch's algorithm needs 1.");
        QuantumSteps.Done(rec, () => problem.defaultVerifier.verify(problem, answer), new QuantumProgress(queries: queries, result: answer),
            ok => ok ? $"f is {answer}." : $"The answer {answer} is not correct.");
        return answer;
    }

    private static string Bit(bool b) => b ? "1" : "0";
}
