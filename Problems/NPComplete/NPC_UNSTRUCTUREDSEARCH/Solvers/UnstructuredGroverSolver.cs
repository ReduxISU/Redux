using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;
using API.Tools;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Solvers;

// Incremental: the circuit is applied one column at a time, and then read.
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
    public string complexity { get; } = "O(sqrt(2^n)) oracle queries";

    // --- Methods Including Constructors ---
    public UnstructuredGroverSolver() {
    }

    static public int PowerOfTwo(int n) {
        if (n > 0 && (n & (n - 1)) == 0)
            return (int)Math.Log2(n);
        throw new ArithmeticException("not a power of two");
    }

    public string solve(UNSTRUCTUREDSEARCH problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // Steps: a Try per column of Grover's circuit for the table (the textbook circuit, drawn and simulated here; the server
    // builds its own from the SAT expression and is never asked for these), then an Accept with the item the server
    // measured when it is marked (a Reject when it is not, #847, or when the server gave no answer), and Done with the
    // verdict. With no answer, the answer is the server's error, as the other quantum solvers give it (#838).
    public string Solve(UNSTRUCTUREDSEARCH problem, StepRecorder<QuantumProgress> rec) {
        // We have to convert the function values into a SAT problem
        int nbits = PowerOfTwo(problem.funcValues.Count);
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
        (string? measured, string error, bool reached) = SolveAsSat(problem, satproblem, counts);
        if (measured == null) {
            if (spec != null) {
                int all = spec.Columns.Count;
                rec.Reject(() => new QuantumProgress(all, counts: counts), () => $"The quantum server gave no answer: {error}");
                QuantumSteps.Done(rec, () => problem.defaultVerifier.verify(problem, error), new QuantumProgress(all, counts: counts, result: error),
                    _ => reached ? "No answer: the quantum server did not give one." : "No answer: the quantum service could not be reached.");
            }
            return error;
        }
        int index = Item(satproblem, measured);
        string answer = index.ToString();
        if (spec != null) {
            int all = spec.Columns.Count;
            long? outcome = index < problem.funcValues.Count ? index : null;
            string[] read = all > 0 ? [QuantumCircuits.ColumnId(all - 1)] : [];
            string Measured() => $"The quantum server measured item {answer} ({QuantumCircuits.Bits(index, nbits)}){QuantumSteps.Shots(counts, measured)}";
            // Green only for a marked item: a missed measurement is a Reject, not an answer (#847).
            if (index < problem.funcValues.Count && problem.funcValues[index] != 0)
                rec.Accept(() => new QuantumProgress(all, outcome: outcome, counts: counts, result: answer), () => $"{Measured()}.", read);
            else
                rec.Reject(() => new QuantumProgress(all, outcome: outcome, counts: counts, result: answer), () => $"{Measured()}, which is not marked.", read);
            QuantumSteps.Done(rec, () => problem.defaultVerifier.verify(problem, answer), new QuantumProgress(all, outcome: outcome, counts: counts, result: answer),
                ok => ok ? $"Item {answer} is marked, found with {QuantumCircuits.GroverRounds(problem.funcValues.Count, problem.funcValues.Count(v => v != 0))} rounds of Grover's algorithm."
                    : problem.funcValues.All(v => v == 0) ? $"Nothing is marked, so item {answer} is not either: the answer should be no solution."
                    : $"Item {answer} is not marked: the measurement missed. Running the circuit again usually finds a marked item.");
        }
        return answer;
    }

    /// <summary>
    /// The variables of <paramref name="formula"/> in the quantum service's order: every distinct name, sorted by code point
    /// (sat_quantum.py's <c>sorted(set(re.findall(r"[A-Za-z0-9_]+", expr)))</c>). From 11 variables up this is not x0, x1,
    /// ...: x10 sorts before x2 (#883). The same as SATGroverSolver's on the #832 fix.
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

    // Text inside the error's JSON string.
    private static string Escape(string text) => JsonEncodedText.Encode(text, System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString();

    // The bits the server measured, in its variable order (see Item); or null, with the error to answer instead and whether the server was reached
    // at all (it answered, but with no bit string).
    private (string? Measured, string Error, bool Reached) SolveAsSat(UNSTRUCTUREDSEARCH problem, string problemInstance, List<KeyValuePair<string, long>> counts) {
        string response;
        try {
            var requestBody = new JSON_Sat_Problem(problemInstance);

            // Create the API client
            var client = new QuantumServerAPI();

            // Make the API call to the quantum endpoint
            response = client.PostAsync("/sat-quantum", requestBody).Result;
        } catch (Exception ex) {
            // Error information, as the other quantum solvers return it
            return (null, $"{{\"error\": \"{Escape(ex.Message)}\"}}", false);
        }

        try {
            // Parse the JSON response and extract just the answer
            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            counts.AddRange(QuantumSteps.Counts(root));
            if (root.TryGetProperty("qasm", out JsonElement circuitElement)) {
                problem.circuit = circuitElement.GetString() ?? "";
            }
            if (root.TryGetProperty("answer_bitstring", out JsonElement answerElement) && answerElement.ValueKind == JsonValueKind.String
                && answerElement.GetString() is { Length: > 0 and <= 31 } bits && bits.All(c => c is '0' or '1')) {
                return (bits, "", true);
            }
        } catch (JsonException) {
            // Not JSON: no answer, as below.
        }
        // The quantum service responded, but with no bit string to read as an item.
        return (null, $"{{\"error\": \"The quantum service gave no answer_bitstring: {Escape(response)}\"}}", true);
    }

}
