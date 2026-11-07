using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_SIMON.Solvers;

// The steps Simon Classical records, for the Quantum Circuit picture (which this solver never runs, so only its oracle is
// drawn as the focus):
//   - an Update per oracle call, x = 0, 1, 2, ...: asking the oracle has no verdict. A call whose output repeats an
//     earlier one says which s that pair suggests;
//   - an Accept with s, or a Reject when f breaks the promise (it is not 2-to-1, or its pairs differ by more than one s);
//   - the Done, with the verifier's verdict.
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
    public MathematicalFunction timeComplexity { get; } = new("2^n", " queries");

    // --- Methods Including Constructors ---
    public SimonSolver() { }

    // How the steps show the run: the input the step calls the oracle with (null: a step that calls nothing), and the
    // answer once there is one.
    private static RunSteps<(int? Call, string? Answer), QuantumProgress> NewSteps(StepRecorder<QuantumProgress> rec,
        List<OracleQuery> queries, string? oracle) => new(rec,
        // What every picture shows: the oracle calls made so far, and the answer.
        partial: s => new QuantumProgress(queries: queries, result: s.Answer),
        // The oracle gate, for a step that calls it.
        focus: s => s.Call != null && oracle != null ? [oracle] : []);

    public string solve(SIMON problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // Calls f on every input and groups the inputs by output. f is promised to be 2-to-1 with f(x) = f(x ⊕ s), so each
    // group is a pair {x, x ⊕ s}, and XORing a pair gives s. A one-to-one f has s = 0...0.
    public string Solve(SIMON problem, StepRecorder<QuantumProgress> rec) {
        int nbits = SIMON.PowerOfTwo(problem.funcValues.Length);
        // The oracle gate is looked up only for the steps: a plain solve never builds the circuit.
        string? gate = QuantumSteps.Recording(rec) ? QuantumCircuits.Simon(problem.funcValues).OracleGateId : null;
        var queries = new List<OracleQuery>();
        var steps = NewSteps(rec, queries, gate);

        var fmap = new Dictionary<int, HashSet<int>>();
        for (int s = 0; s < problem.funcValues.Length; s++) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            int y = problem.Func(s);
            if (!fmap.ContainsKey(y))
                fmap[y] = new HashSet<int>();
            fmap[y].Add(s);
            queries.Add(new OracleQuery(s, y));
            // The first input that gave this output: s itself, unless the output repeats.
            int earlier = fmap[y].First();
            if (s == earlier)
                steps.Update((s, null), $"Call the oracle with x = {QuantumSteps.Input(s, nbits)}: f({QuantumSteps.Input(s, nbits)}) = {y}.");
            else
                steps.Update((s, null), $"Call the oracle with x = {QuantumSteps.Input(s, nbits)}: f({QuantumSteps.Input(s, nbits)}) = {y}, the same as f({QuantumSteps.Input(earlier, nbits)}), so s could be {QuantumSteps.Input(s ^ earlier, nbits)}.");
        }

        string? answer = null;
        if (fmap.Count == problem.funcValues.Length) {
            // one to one mapping, secret is all zeros
            answer = string.Concat(Enumerable.Repeat("0", nbits));
            if (nbits == 0)
                steps.Accept((null, answer), $"f has a single input, so there is nothing to pair: s is empty.");
            else
                steps.Accept((null, answer), $"Every output appears once: f is one-to-one, so s = {answer}. That took {Words.Number(queries.Count)} oracle calls.");
        } else {
            var candidates = new HashSet<int>();
            foreach (var (y, ys) in fmap) {
                if (ys.Count != 2) {
                    steps.Reject((null, null), $"The output {y} comes from {Words.Count(ys.Count, "input")}, not 2: f is not 2-to-1, so no secret string fits.");
                    answer = "invalid func: not 2-to-1 mapping";
                    break;
                }
                int candidate = 0;
                foreach (var c in ys)
                    candidate ^= c;
                candidates.Add(candidate);
            }

            if (answer == null && candidates.Count > 1) {
                steps.Reject((null, null), $"The pairs with equal outputs differ by {string.Join(", ", candidates.Order().Select(c => QuantumSteps.Input(c, nbits)))}: no single s fits them all.");
                answer = "invalid func: no secret string";
            } else if (answer == null) {
                answer = Convert.ToString(candidates.First(), 2).PadLeft(nbits, '0');
                steps.Accept((null, answer), $"Every output comes from exactly two inputs, and each pair differs by {answer}: s = {answer}. That took {Words.Number(queries.Count)} oracle calls.");
            }
        }

        // The verdict is only for the steps: a plain solve never runs the verifier.
        if (QuantumSteps.Recording(rec)) {
            var final = new QuantumProgress(queries: queries, result: answer);
            bool ok = QuantumSteps.Verdict(() => problem.defaultVerifier.verify(problem, answer));
            if (ok && answer.Length == 0)
                steps.Done(final, true, $"s is empty: f has no input bits.");
            else if (ok)
                steps.Done(final, true, $"s = {answer}: f(x) = f(x ⊕ s) for every x.");
            else if (answer.StartsWith("invalid func"))
                steps.Done(final, false, $"No answer: f breaks Simon's promise.");
            else
                steps.Done(final, false, $"The answer {answer} is not correct.");
        }
        return answer;
    }
}
