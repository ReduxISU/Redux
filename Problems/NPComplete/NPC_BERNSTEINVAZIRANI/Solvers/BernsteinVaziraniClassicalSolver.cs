using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;
using SPADE;

namespace API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI.Solvers;

// The steps Bernstein-Vazirani Classical records, for the Quantum Circuit picture (which this solver never runs, so only
// its oracle is drawn as the focus):
//   - an Update per oracle call, one per bit of s from the first: asking the oracle has no verdict;
//   - an Accept with s;
//   - the Done, with the verifier's verdict (which checks that f really is s · x).
// Incremental: each oracle call reads one more bit of s, and none is taken back.
class BernsteinVaziraniClassicalSolver : ISolver<BERNSTEINVAZIRANI, QuantumProgress>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "Bernstein-Vazirani Classical";
    public string solverDefinition { get; } = "This is a classical verifier for the Bernstein-Vazirani problem which runs in O(n) time.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Jason L. Wright" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Classical baseline paired with BernsteinVaziraniQuantumSolver; no
    // SolverType bucket fits so solverType stays Unclassified per user decision. O(n) classical
    // oracle queries (n = number of bits) -- polynomial, contrast with the quantum solver's O(1).
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // solve() loops i from NBits-1 down to 0, making exactly one Func() oracle query per bit.
    public MathematicalFunction timeComplexity { get; } = new("n", "");

    // --- Methods Including Constructors ---
    public BernsteinVaziraniClassicalSolver() { }

    // How the steps show the run: the input the step calls the oracle with (null: a step that calls nothing), and the
    // answer once there is one.
    private static RunSteps<(int? Call, string? Answer), QuantumProgress> NewSteps(StepRecorder<QuantumProgress> rec,
        List<OracleQuery> queries, string? oracle) => new(rec,
        // What every picture shows: the oracle calls made so far, and the answer.
        partial: s => new QuantumProgress(queries: queries, result: s.Answer),
        // The oracle gate, for a step that calls it.
        focus: s => s.Call != null && oracle != null ? [oracle] : []);

    public string solve(BERNSTEINVAZIRANI problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // f(x) = s · x (mod 2), so calling f on the input with only bit i set reads bit i of s: one call per bit.
    public string Solve(BERNSTEINVAZIRANI problem, StepRecorder<QuantumProgress> rec) {
        int n = problem.NBits;
        // The oracle gate is looked up only for the steps: a plain solve never builds the circuit.
        string? gate = QuantumSteps.Recording(rec) ? QuantumCircuits.BernsteinVazirani(problem.funcValues).OracleGateId : null;
        var queries = new List<OracleQuery>();
        var steps = NewSteps(rec, queries, gate);
        var s = new System.Text.StringBuilder();
        UtilCollection result = new("()");
        for (int i = problem.NBits - 1; i >= 0; i--) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            int x = 1 << i, fx = problem.Func(x) ? 1 : 0;
            result.Add(new UtilCollection(fx.ToString()));
            queries.Add(new OracleQuery(x, fx));
            s.Append(fx);
            steps.Update((x, null), $"Call the oracle with x = {QuantumSteps.Input(x, n)}: f({QuantumSteps.Input(x, n)}) = {fx}, so bit {s.Length} of s is {fx}.");
        }
        string answer = result.ToString();
        string bits = s.ToString();
        if (n == 0)
            steps.Accept((null, answer), $"f has no input bits, so s is empty.");
        else
            steps.Accept((null, answer), $"s = {bits}, after {n} oracle {(n == 1 ? "call" : "calls")}, one per bit; the Bernstein-Vazirani algorithm needs 1.");

        // The verdict is only for the steps: a plain solve never runs the verifier.
        if (QuantumSteps.Recording(rec)) {
            var final = new QuantumProgress(queries: queries, result: answer);
            if (QuantumSteps.Verdict(() => problem.defaultVerifier.verify(problem, answer)))
                steps.Done(final, true, $"s = {bits}: f(x) = s · x (mod 2) for every x.");
            else
                steps.Done(final, false, $"s = {bits} does not fit every input: f(x) is not s · x for any s, so f breaks the promise.");
        }
        return answer;
    }
}
