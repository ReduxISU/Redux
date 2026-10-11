using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Solvers;

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

    public string solve(UNSTRUCTUREDSEARCH problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // Steps (the Quantum Circuit picture, which this solver never runs): a Reject per item the oracle says is not marked,
    // with the oracle as the focus, then an Accept for the first marked item, and Done with the verifier's verdict.
    public string Solve(UNSTRUCTUREDSEARCH problem, StepRecorder<QuantumProgress> rec) {
        string? gate = QuantumSteps.Recording(rec) ? QuantumCircuits.UnstructuredSearch(problem.funcValues).OracleGateId : null;
        string[] Focus() => gate == null ? [] : [gate];
        int bits = QuantumCircuits.Log2(problem.funcValues.Count);
        string Item(int i) => bits > 0 ? $"item {i} ({QuantumCircuits.Bits(i, bits)})" : $"item {i}";
        var queries = new List<OracleQuery>();
        string Finish(string answer) {
            QuantumSteps.Done(rec, () => problem.defaultVerifier.verify(problem, answer), new QuantumProgress(queries: queries, result: answer),
                ok => answer == "no solution"
                    ? (ok ? $"No item of the {QuantumCircuits.Count(problem.funcValues.Count)} is marked: no solution." : "The answer no solution is not correct.")
                    : ok ? $"Item {answer} is marked, found after {QuantumCircuits.Count(queries.Count)} oracle {(queries.Count == 1 ? "call" : "calls")}." : $"The answer {answer} is not correct.");
            return answer;
        }

        // For a classical solution, we just need to loop over the values and
        // find the first non-zero value.
        for (int i = 0; i < problem.funcValues.Count; i++) {
            int x = i, fx = problem.funcValues[i];
            queries.Add(new OracleQuery(x, fx));
            if (fx != 0) {
                rec.Accept(() => new QuantumProgress(queries: queries, result: Convert.ToString(x)),
                    () => $"Call the oracle with {Item(x)}: f = {fx}, so it is marked.", Focus);
                return Finish(Convert.ToString(i));
            }
            rec.Reject(() => new QuantumProgress(queries: queries), () => $"Call the oracle with {Item(x)}: f = 0, not marked.", Focus);
        }
        return Finish("no solution");
    }
}
