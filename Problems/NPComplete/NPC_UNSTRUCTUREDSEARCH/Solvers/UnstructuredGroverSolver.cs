using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;
using API.Tools;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Solvers;

// The steps Grover's Algorithm records, for the Quantum Circuit picture:
//   - an Update per column of Grover's circuit, in order: the textbook circuit, drawn and simulated here (the quantum
//     server builds its own from the SAT expression and is never asked for these). Applying gates has no verdict;
//   - an Accept for the item the server measured when it is marked, or a Reject when it is not;
//   - the Done, with the verifier's verdict: how many rounds of Grover's algorithm found the item, or, for a miss, how
//     often a run of the circuit drawn here finds a marked item.
// With no answer from the server there is no answer at all: SolveAsSat throws SolverUnavailableException (HTTP 503
// solver_unavailable), never the server's error in place of an item. Incremental: the circuit is applied one column at a
// time, and then read.
class UnstructuredGroverSolver : ISolver<UNSTRUCTUREDSEARCH, QuantumProgress>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "Grover's Algorithm";
    public string solverDefinition { get; } = "This solver represents f(x) has a boolean circuit and then use Grover's algorithm to locate x such that f(x) = 1.";
    public string source { get; } = "Grover L.K.: A fast quantum mechanical algorithm for database search, Proceedings, 28th Annual ACM Symposium on the Theory of Computing, (May 1996) p. 212.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Jason L. Wright" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Delegates to the external quantum-simulator service.
    public SolverType solverType { get; } = SolverType.Quantum;
    // Grover's algorithm finds a marked item among N = 2^n candidates in O(sqrt(N)) oracle
    // queries; this solver reduces to a SAT instance and makes one call to the quantum
    // endpoint that runs that Grover search.
    public MathematicalFunction timeComplexity { get; } = new("sqrt(2^n)", " oracle queries");

    // --- Methods Including Constructors ---
    public UnstructuredGroverSolver() {
    }

    static public int PowerOfTwo(int n) {
        if (n > 0 && (n & (n - 1)) == 0)
            return (int)Math.Log2(n);
        throw new ArithmeticException("not a power of two");
    }

    // How the steps show the measurement (the item the server measured): read off the end of the whole circuit.
    private static RunSteps<int, QuantumProgress> NewSteps(StepRecorder<QuantumProgress> rec, int columns, int items,
        List<KeyValuePair<string, long>> counts) => new(rec,
        // What every picture shows: the whole circuit applied, the item measured (no outcome when it is not an item), and
        // how often the server saw each outcome.
        partial: item => new QuantumProgress(columns, outcome: item < items ? item : null, counts: counts, result: item.ToString()),
        // The circuit's last column, where the measurement is read.
        focus: _ => columns > 0 ? [QuantumCircuits.ColumnId(columns - 1)] : []);

    public string solve(UNSTRUCTUREDSEARCH problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // Writes f as a SAT expression (one AND of the input bits per marked item, ORed together) and asks the quantum server
    // to run Grover's search on it; the bits it measured name the item.
    public string Solve(UNSTRUCTUREDSEARCH problem, StepRecorder<QuantumProgress> rec) {
        // We have to convert the function values into a SAT problem
        int nbits = PowerOfTwo(problem.funcValues.Count);
        // The circuit is built and simulated only for the steps: a plain solve never builds it.
        var spec = QuantumSteps.Recording(rec) ? QuantumCircuits.UnstructuredSearch(problem.funcValues) : null;
        if (spec != null) QuantumSteps.Columns(rec, spec);
        var exprs = new List<string>();
        for (int i = 0; i < problem.funcValues.Count; i++) {
            if (problem.funcValues[i] != 0) {
                var expr = "(";
                var sep = "";
                for (int b = 0; b < nbits; b++) {
                    if ((i & (1 << b)) != 0)
                        expr += $"{sep}x{b}";
                    else
                        expr += $"{sep}!x{b}";
                    sep = " & ";
                }
                expr += ")";
                exprs.Add(expr);
            }
        }

        var satproblem = String.Join(" | ", exprs);
        var counts = new List<KeyValuePair<string, long>>();
        string measured = SolveAsSat(problem, satproblem, nbits, counts);
        int index = Item(satproblem, measured);
        string answer = index.ToString();
        if (spec != null) {
            int items = problem.funcValues.Count;
            var steps = NewSteps(rec, spec.Columns.Count, items, counts);
            // Green only for a marked item: a missed measurement is a Reject, not an answer.
            if (index < items && problem.funcValues[index] != 0)
                steps.Accept(index, $"The quantum server measured item {answer} ({QuantumCircuits.Bits(index, nbits)}){QuantumSteps.Shots(counts, measured)}.");
            else
                steps.Reject(index, $"The quantum server measured item {answer} ({QuantumCircuits.Bits(index, nbits)}){QuantumSteps.Shots(counts, measured)}, which is not marked.");

            var final = new QuantumProgress(spec.Columns.Count, outcome: index < items ? index : null, counts: counts, result: answer);
            var marked = Enumerable.Range(0, items).Where(i => problem.funcValues[i] != 0).ToList();
            int rounds = QuantumCircuits.GroverRounds(items, marked.Count);
            if (QuantumSteps.Verdict(() => problem.defaultVerifier.verify(problem, answer))) {
                steps.Done(final, true, $"Item {answer} is marked, found with {Words.Count(rounds, "round")} of Grover's algorithm.");
            } else if (marked.Count == 0) {
                steps.Done(final, false, $"Nothing is marked, so item {answer} is not either: the answer should be no solution.");
            } else {
                // How often a run of the circuit drawn here finds a marked item, from simulating it; null when the circuit is
                // too big to simulate. Never "usually": with 2 of 4 marked it is 50%, and with 3 of 4 one round overshoots to 0%.
                string? chance = new QuantumSimulation(spec).After(spec.Columns.Count) is { } end
                    ? QuantumCircuits.Percent(marked.Sum(i => end.Probabilities[i])) : null;
                if (chance == "0%")
                    steps.Done(final, false, $"Item {answer} is not marked. With {marked.Count} of {Words.Number(items)} items marked, the marked items end at 0% after {Words.Count(rounds, "round")} of Grover's algorithm, so every run misses.");
                else if (chance == "100%")
                    steps.Done(final, false, $"Item {answer} is not marked: the measurement missed. The circuit drawn here finds a marked item every time, so the server's run went differently.");
                else if (chance != null)
                    steps.Done(final, false, $"Item {answer} is not marked: the measurement missed. A run finds a marked item about {chance} of the time.");
                else
                    steps.Done(final, false, $"Item {answer} is not marked: the measurement missed.");
            }
        }
        return answer;
    }

    /// <summary>
    /// The variables of <paramref name="formula"/> in the quantum service's order: every distinct name, sorted by code point
    /// (sat_quantum.py's <c>sorted(set(re.findall(r"[A-Za-z0-9_]+", expr)))</c>). From 11 variables up this is not x0, x1,
    /// ...: x10 sorts before x2. The same as SATGroverSolver's.
    /// </summary>
    public static List<string> ServiceVariableOrder(string formula) =>
        Regex.Matches(formula, "[A-Za-z0-9_]+").Select(m => m.Value).Distinct().OrderBy(v => v, StringComparer.Ordinal).ToList();

    /// <summary>
    /// The item the service's <paramref name="bits"/> name: character i is variable i of
    /// <see cref="ServiceVariableOrder"/>, and variable xb is bit b of the item. With no variable named (nothing is marked, so
    /// the formula is empty), character i is bit i.
    /// </summary>
    public static int Item(string formula, string bits) {
        List<string> order = ServiceVariableOrder(formula);
        int item = 0;
        for (int i = 0; i < bits.Length; i++)
            if (bits[i] == '1')
                item |= 1 << (i < order.Count ? int.Parse(order[i][1..]) : i);
        return item;
    }

    public class JSON_Sat_Problem {
        public string boolexpr { get; set; }
        public JSON_Sat_Problem(string expr) {
            boolexpr = expr;
        }
    }

    // The bits the server measured, in its variable order (see Item). On any failure of the quantum service, including a
    // response with no `answer_bitstring` or one that is not `nbits` binary digits, this throws
    // SolverUnavailableException (HTTP 503 solver_unavailable) rather than handing Solve something that is not a bit string. The bit string is checked inside Ask, so a malformed one fails the same way as a missing one.
    private string SolveAsSat(UNSTRUCTUREDSEARCH problem, string problemInstance, int nbits, List<KeyValuePair<string, long>> counts) {
        var requestBody = new JSON_Sat_Problem(problemInstance);

        return QuantumServerAPI.Ask(this, problem.problemName, client => {
            // Make the API call to the quantum endpoint
            string response = client.PostAsync("/sat-quantum", requestBody).Result;

            // Parse the JSON response and extract just the answer
            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            counts.AddRange(QuantumSteps.Counts(root));
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("qasm", out JsonElement circuitElement) && circuitElement.ValueKind == JsonValueKind.String) {
                problem.circuit = circuitElement.GetString() ?? "";
            }
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("answer_bitstring", out JsonElement answerElement)
                && answerElement.ValueKind == JsonValueKind.String) {
                string bits = answerElement.GetString() ?? "";
                if (bits.Length == 0 || bits.Length != nbits || bits.Length > 31 || bits.Any(c => c != '0' && c != '1'))
                    throw new InvalidDataException($"its 'answer_bitstring' is not {nbits} binary digits: {response}");
                return bits;
            }

            // No bit string to read as an item: not an answer to pass on.
            throw QuantumServerAPI.MissingAnswer("answer_bitstring", response);
        });
    }

}
