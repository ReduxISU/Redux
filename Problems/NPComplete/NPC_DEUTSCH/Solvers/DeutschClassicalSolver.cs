using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_DEUTSCH.Solvers;

// The steps Deutsch Classical records, for the Quantum Circuit picture (which this solver never runs, so only its oracle
// is drawn as the focus):
//   - an Update per oracle call, f(0) then f(1): asking the oracle has no verdict;
//   - an Accept with the answer: constant when the two values agree, balanced when they differ;
//   - the Done, with the verifier's verdict.
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
    public MathematicalFunction timeComplexity { get; } = new("1", "");

    // --- Methods Including Constructors ---
    public DeutschClassicalSolver() { }

    // How the steps show the run: the input the step calls the oracle with (null: a step that calls nothing), and the
    // answer once there is one.
    private static RunSteps<(int? Call, string? Answer), QuantumProgress> NewSteps(StepRecorder<QuantumProgress> rec,
        List<OracleQuery> queries, string? oracle) => new(rec,
        // What every picture shows: the oracle calls made so far, and the answer.
        partial: s => new QuantumProgress(queries: queries, result: s.Answer),
        // The oracle gate, for a step that calls it.
        focus: s => s.Call != null && oracle != null ? [oracle] : []);

    public string solve(DEUTSCH problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // Classically, the only way to learn whether f is constant or balanced is to call it on both inputs and compare.
    public string Solve(DEUTSCH problem, StepRecorder<QuantumProgress> rec) {
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        // The oracle gate is looked up only for the steps: a plain solve never builds the circuit.
        string? oracle = QuantumSteps.Recording(rec) ? QuantumCircuits.Deutsch(problem.funcValues).OracleGateId : null;
        var queries = new List<OracleQuery>();
        var steps = NewSteps(rec, queries, oracle);

        int f0 = problem.Func(false) ? 1 : 0;
        queries.Add(new OracleQuery(0, f0));
        steps.Update((0, null), $"Call the oracle with x = 0: f(0) = {f0}.");
        int f1 = problem.Func(true) ? 1 : 0;
        queries.Add(new OracleQuery(1, f1));
        steps.Update((1, null), $"Call the oracle with x = 1: f(1) = {f1}.");

        string answer = f0 == f1 ? "constant" : "balanced";
        if (f0 == f1)
            steps.Accept((null, answer), $"f(0) = f(1) = {f0}, so f is constant. That took 2 oracle calls; Deutsch's algorithm needs 1.");
        else
            steps.Accept((null, answer), $"f(0) = {f0} and f(1) = {f1} differ, so f is balanced. That took 2 oracle calls; Deutsch's algorithm needs 1.");

        // The verdict is only for the steps: a plain solve never runs the verifier.
        if (QuantumSteps.Recording(rec)) {
            var final = new QuantumProgress(queries: queries, result: answer);
            if (QuantumSteps.Verdict(() => problem.defaultVerifier.verify(problem, answer)))
                steps.Done(final, true, $"f is {answer}.");
            else
                steps.Done(final, false, $"The answer {answer} is not correct.");
        }
        return answer;
    }
}
