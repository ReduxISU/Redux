using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;
using SPADE;

namespace API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI.Solvers;

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

    public string solve(BERNSTEINVAZIRANI problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // Steps (the Quantum Circuit picture, which this solver never runs): a Try per oracle call, one per bit of s from the
    // first, with the oracle as the focus; an Accept with s; Done with the verifier's verdict (which checks that f really is
    // s · x).
    public string Solve(BERNSTEINVAZIRANI problem, StepRecorder<QuantumProgress> rec) {
        int n = problem.NBits;
        string? gate = QuantumSteps.Recording(rec) ? QuantumCircuits.BernsteinVazirani(problem.funcValues).OracleGateId : null;
        string[] Focus() => gate == null ? [] : [gate];
        var queries = new List<OracleQuery>();
        var s = new System.Text.StringBuilder();
        UtilCollection result = new("()");
        for (int i = problem.NBits - 1; i >= 0; i--) {
            bool fx = problem.Func(1 << i);
            result.Add(new UtilCollection(fx ? "1" : "0"));
            queries.Add(new OracleQuery(1 << i, fx ? 1 : 0));
            s.Append(fx ? '1' : '0');
            int x = 1 << i, bit = s.Length;
            rec.Try(() => new QuantumProgress(queries: queries), () => {
                string input = QuantumSteps.Input(x, n);
                return $"Call the oracle with x = {input}: f({input}) = {(fx ? 1 : 0)}, so bit {bit} of s is {(fx ? 1 : 0)}.";
            }, Focus);
        }
        string answer = result.ToString();
        string bits = s.ToString();
        rec.Accept(() => new QuantumProgress(queries: queries, result: answer), () => n == 0
            ? "f has no input bits, so s is empty."
            : $"s = {bits}, after {n} oracle {(n == 1 ? "call" : "calls")}, one per bit; the Bernstein-Vazirani algorithm needs 1.");
        QuantumSteps.Done(rec, () => problem.defaultVerifier.verify(problem, answer), new QuantumProgress(queries: queries, result: answer),
            ok => ok ? $"s = {bits}: f(x) = s · x (mod 2) for every x."
                : $"s = {bits} does not fit every input: f(x) is not s · x for any s, so f breaks the promise.");
        return answer;
    }
}
