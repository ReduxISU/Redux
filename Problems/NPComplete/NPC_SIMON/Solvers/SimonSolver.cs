using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_SIMON.Solvers;

// Incremental: each oracle call adds to what the solver knows about f, and none is taken back.
class SimonSolver : ISolver<SIMON, QuantumProgress>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "Simon Classical";
    public string solverDefinition { get; } = "This solver classically solves Simon's Problem by analyzing the provided function values to determine the secret string.";
    public string source { get; } = "Simon, Daniel R. (1997-10-01). \"On the Power of Quantum Computation\". SIAM Journal on Computing. 26 (5): 1474–1483. doi:10.1137/S0097539796298637. ISSN 0097-5397";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Jason L. Wright", "Eric Hill", "Paul Gilbreath", "Max Grünwoldt", "Alex Svancara" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Classical algorithm for Simon's problem; no SolverType bucket fits so
    // solverType stays Unclassified per user decision. Natural parameter is oracle-bit-count n, not
    // the 2^n-length array scanned -- classically needs exponentially many oracle queries in n, the
    // entire pedagogical point of this problem.
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // solve() calls problem.Func(s) once for every s in [0, 2^n), building fmap over the
    // full function table before it can recover the secret string.
    public string complexity { get; } = "O(2^n) queries";

    // --- Methods Including Constructors ---
    public SimonSolver() { }

    public string solve(SIMON problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // Steps (the Quantum Circuit picture, which this solver never runs): a Try per oracle call, x = 0, 1, 2, ..., with the
    // oracle as the focus and a note when an output repeats; then an Accept with s, or a Reject when f breaks the promise;
    // and Done with the verifier's verdict.
    public string Solve(SIMON problem, StepRecorder<QuantumProgress> rec) {
        int nbits = SIMON.PowerOfTwo(problem.funcValues.Length);
        string? gate = QuantumSteps.Recording(rec) ? QuantumCircuits.Simon(problem.funcValues).OracleGateId : null;
        string[] Focus() => gate == null ? [] : [gate];
        string In(int x) => QuantumSteps.Input(x, nbits);
        var queries = new List<OracleQuery>();
        string Finish(string answer) {
            QuantumSteps.Done(rec, () => problem.defaultVerifier.verify(problem, answer), new QuantumProgress(queries: queries, result: answer),
                ok => ok ? (answer.Length == 0 ? "s is empty: f has no input bits." : $"s = {answer}: f(x) = f(x ⊕ s) for every x.")
                    : answer.StartsWith("invalid func") ? "No answer: f breaks Simon's promise." : $"The answer {answer} is not correct.");
            return answer;
        }

        var fmap = new Dictionary<int, HashSet<int>>();
        for (int s = 0; s < problem.funcValues.Length; s++) {
            int y = problem.Func(s);
            if (!fmap.ContainsKey(y))
                fmap[y] = new HashSet<int>();
            fmap[y].Add(s);
            queries.Add(new OracleQuery(s, y));
            int x = s, earlier = fmap[y].First();
            rec.Try(() => new QuantumProgress(queries: queries), () => x == earlier
                ? $"Call the oracle with x = {In(x)}: f({In(x)}) = {y}."
                : $"Call the oracle with x = {In(x)}: f({In(x)}) = {y}, the same as f({In(earlier)}), so s could be {In(x ^ earlier)}.", Focus);
        }

        if (fmap.Count == problem.funcValues.Length) {
            // one to one mapping, secret is all zeros
            string zeros = string.Concat(Enumerable.Repeat("0", nbits));
            rec.Accept(() => new QuantumProgress(queries: queries, result: zeros), () => nbits == 0
                ? "f has a single input, so there is nothing to pair: s is empty."
                : $"Every output appears once: f is one-to-one, so s = {zeros}. That took {QuantumCircuits.Count(queries.Count)} oracle calls.");
            return Finish(zeros);
        }

        var candidates = new HashSet<int>();
        foreach (var (y, ys) in fmap) {
            if (ys.Count != 2) {
                rec.Reject(() => new QuantumProgress(queries: queries),
                    () => $"The output {y} comes from {ys.Count} {(ys.Count == 1 ? "input" : "inputs")}, not 2: f is not 2-to-1, so no secret string fits.");
                return Finish("invalid func: not 2-to-1 mapping");
            }
            int candidate = 0;
            foreach (var c in ys)
                candidate ^= c;
            candidates.Add(candidate);
        }

        if (candidates.Count > 1) {
            rec.Reject(() => new QuantumProgress(queries: queries), () => $"The pairs with equal outputs differ by {string.Join(", ", candidates.Order().Select(In))}: no single s fits them all.");
            return Finish("invalid func: no secret string");
        }

        string answer = Convert.ToString(candidates.First(), 2).PadLeft(nbits, '0');
        rec.Accept(() => new QuantumProgress(queries: queries, result: answer),
            () => $"Every output comes from exactly two inputs, and each pair differs by {answer}: s = {answer}. That took {QuantumCircuits.Count(queries.Count)} oracle calls.");
        return Finish(answer);
    }
}
