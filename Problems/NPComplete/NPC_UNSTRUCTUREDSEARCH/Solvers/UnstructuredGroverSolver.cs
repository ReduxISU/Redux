using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;
using API.Tools;
using System.Text.Json;

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
    // measured, and Done with the verdict. A server that gives no answer throws, as solve always has.
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
        string measured = SolveAsSat(problem, satproblem, counts);
        var solution = ReverseString(measured);
        string answer = Convert.ToInt32(solution, 2).ToString();
        if (spec != null) {
            int all = spec.Columns.Count, index = Convert.ToInt32(solution, 2);
            long? outcome = index < problem.funcValues.Count ? index : null;
            rec.Accept(() => new QuantumProgress(all, outcome: outcome, counts: counts, result: answer),
                () => $"The quantum server measured item {answer} ({QuantumCircuits.Bits(index, nbits)}){QuantumSteps.Shots(counts, measured)}.",
                all > 0 ? [QuantumCircuits.ColumnId(all - 1)] : []);
            QuantumSteps.Done(rec, () => problem.defaultVerifier.verify(problem, answer), new QuantumProgress(all, outcome: outcome, counts: counts, result: answer),
                ok => ok ? $"Item {answer} is marked, found with {QuantumCircuits.GroverRounds(problem.funcValues.Count, problem.funcValues.Count(v => v != 0))} rounds of Grover's algorithm."
                    : problem.funcValues.All(v => v == 0) ? $"Nothing is marked, so item {answer} is not either: the answer should be no solution."
                    : $"Item {answer} is not marked: the measurement missed. Running the circuit again usually finds a marked item.");
        }
        return answer;
    }

    public static string ReverseString(string s) {
        var a = s.ToArray();
        Array.Reverse(a);
        return new string(a);
    }

    public class JSON_Sat_Problem {
        public string boolexpr { get; set; }
        public JSON_Sat_Problem(string expr) {
            boolexpr = expr;
        }
    }

    private string SolveAsSat(UNSTRUCTUREDSEARCH problem, string problemInstance, List<KeyValuePair<string, long>> counts) {
        try {
            var requestBody = new JSON_Sat_Problem(problemInstance);

            // Create the API client
            var client = new QuantumServerAPI();

            // Make the API call to the quantum endpoint
            string response = client.PostAsync("/sat-quantum", requestBody).Result;

            // Parse the JSON response and extract just the answer
            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            counts.AddRange(QuantumSteps.Counts(root));
            if (root.TryGetProperty("qasm", out JsonElement circuitElement)) {
                problem.circuit = circuitElement.GetString() ?? "";
            }
            if (root.TryGetProperty("answer_bitstring", out JsonElement answerElement)) {
                return answerElement.GetString() ?? "No answer found";
            }

            // The quantum service responded, but its JSON has no `answer_bitstring` field.
            // Returning the raw response here would let the caller misinterpret it as
            // bitstring data (solve() reverses it and parses it as a base-2 integer),
            // producing a confusing FormatException instead of a clear error.
            throw new InvalidOperationException(
                $"Quantum service response is missing the expected 'answer_bitstring' field: {response}");
        } catch (Exception ex) when (ex is not InvalidOperationException) {
            // Wrap failures from the HTTP call/setup (network errors, timeouts, JSON parse
            // failures, etc.) in the same descriptive exception type instead of swallowing
            // them into an error-JSON string that would hit the same missing-field case above.
            throw new InvalidOperationException(
                $"Failed to obtain an answer from the quantum service: {ex.Message}", ex);
        }
    }

}
