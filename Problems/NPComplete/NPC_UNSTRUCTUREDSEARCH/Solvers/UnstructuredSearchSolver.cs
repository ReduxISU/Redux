using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Solvers;

// The steps Unstructured Search Brute Force records, for the Quantum Circuit picture (which this solver never runs, so
// only its oracle is drawn as the focus):
//   - a Reject per item the oracle says is not marked;
//   - an Accept for the first marked item;
//   - the Done, with the verifier's verdict.
// Not incremental: each oracle call judges one whole candidate, the item it asks about.
class UnstructuredSearchSolver : ISolver<UNSTRUCTUREDSEARCH, QuantumProgress> {

    // --- Fields ---
    public string solverName { get; } = "Unstructured Search Brute Force";
    public string solverDefinition { get; } = "This solver simply loops through all possible x until one f(x) = 1.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Jason L. Wright", "Alex Svancara" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration. Natural parameter is oracle-bit-count n
    // (matches the paired UnstructuredGroverSolver's parameter), not the 2^n-length array scanned --
    // classically needs exponentially many oracle queries in n, the entire pedagogical point of this
    // problem (classical search vs. Grover's quadratic quantum speedup).
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // solve() linearly scans problem.funcValues (length 2^n) until it finds a 1, worst
    // case (or "no solution") requiring all 2^n oracle queries.
    public MathematicalFunction timeComplexity { get; } = new("2^n", " queries");

    // --- Methods Including Constructors ---
    public UnstructuredSearchSolver() { }

    // How the steps show the run: the item the step calls the oracle with (null: a step that calls nothing), and the
    // answer once there is one.
    private static RunSteps<(int? Call, string? Answer), QuantumProgress> NewSteps(StepRecorder<QuantumProgress> rec,
        List<OracleQuery> queries, string? oracle) => new(rec,
        // What every picture shows: the oracle calls made so far, and the answer.
        partial: s => new QuantumProgress(queries: queries, result: s.Answer),
        // The oracle gate, for a step that calls it.
        focus: s => s.Call != null && oracle != null ? [oracle] : []);

    public string solve(UNSTRUCTUREDSEARCH problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // Calls the oracle on each item in turn until one is marked (f = 1).
    public string Solve(UNSTRUCTUREDSEARCH problem, StepRecorder<QuantumProgress> rec) {
        // The oracle gate is looked up only for the steps: a plain solve never builds the circuit.
        string? gate = QuantumSteps.Recording(rec) ? QuantumCircuits.UnstructuredSearch(problem.funcValues).OracleGateId : null;
        int bits = QuantumCircuits.Log2(problem.funcValues.Count);
        var queries = new List<OracleQuery>();
        var steps = NewSteps(rec, queries, gate);

        // For a classical solution, we just need to loop over the values and
        // find the first non-zero value.
        string answer = "no solution";
        for (int i = 0; i < problem.funcValues.Count; i++) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            int fx = problem.funcValues[i];
            queries.Add(new OracleQuery(i, fx));
            // An item is named by its index, and by its bits when there are any.
            if (fx != 0) {
                answer = Convert.ToString(i);
                steps.Accept((i, answer), $"Call the oracle with item {i}{(bits > 0 ? $" ({QuantumCircuits.Bits(i, bits)})" : "")}: f = {fx}, so it is marked.");
                break;
            }
            steps.Reject((i, null), $"Call the oracle with item {i}{(bits > 0 ? $" ({QuantumCircuits.Bits(i, bits)})" : "")}: f = 0, not marked.");
        }

        // The verdict is only for the steps: a plain solve never runs the verifier.
        if (QuantumSteps.Recording(rec)) {
            var final = new QuantumProgress(queries: queries, result: answer);
            bool ok = QuantumSteps.Verdict(() => problem.defaultVerifier.verify(problem, answer));
            if (answer == "no solution" && ok)
                steps.Done(final, true, $"No item of the {Words.Number(problem.funcValues.Count)} is marked: no solution.");
            else if (answer == "no solution")
                steps.Done(final, false, $"The answer no solution is not correct.");
            else if (ok)
                steps.Done(final, true, $"Item {answer} is marked, found after {Words.Number(queries.Count)} oracle {(queries.Count == 1 ? "call" : "calls")}.");
            else
                steps.Done(final, false, $"The answer {answer} is not correct.");
        }
        return answer;
    }
}
