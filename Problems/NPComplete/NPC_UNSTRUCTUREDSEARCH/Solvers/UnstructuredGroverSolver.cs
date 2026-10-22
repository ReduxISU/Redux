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
    public MathematicalFunction timeComplexity { get; } = new("sqrt(2^n)", " oracle queries");

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
    // measured when it is marked (a Reject when it is not, #847), and Done with the verdict. With no answer from the server
    // there is no answer at all: SolveAsSat throws SolverUnavailableException (HTTP 503 solver_unavailable), never the
    // server's error in place of an item (#625; it was the answer before, #838).
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
                ok => ok ? $"Item {answer} is marked, found with {Rounds(QuantumCircuits.GroverRounds(problem.funcValues.Count, problem.funcValues.Count(v => v != 0)))} of Grover's algorithm."
                    : problem.funcValues.All(v => v == 0) ? $"Nothing is marked, so item {answer} is not either: the answer should be no solution."
                    : Missed(problem.funcValues, spec, answer));
        }
        return answer;
    }

    /// <summary>
    /// Done's caption when the server measured an unmarked item: how often a run of the circuit drawn finds a marked item,
    /// from simulating it (#906). Never "usually": with 2 of 4 marked it is 50%, and with 3 of 4 one round overshoots to
    /// 0%. Nothing is said about the odds when the circuit is too big to simulate.
    /// </summary>
    public static string Missed(IReadOnlyList<int> table, QuantumCircuitSpec spec, string answer) {
        string missed = $"Item {answer} is not marked: the measurement missed.";
        var d = new QuantumSimulation(spec).After(spec.Columns.Count);
        if (d == null) return missed;
        var marked = Enumerable.Range(0, table.Count).Where(i => table[i] != 0).ToList();
        string chance = QuantumCircuits.Percent(marked.Sum(i => d.Probabilities[i]));
        string rounds = Rounds(QuantumCircuits.GroverRounds(table.Count, marked.Count));
        return chance switch {
            "0%" => $"Item {answer} is not marked. With {marked.Count} of {QuantumCircuits.Count(table.Count)} items marked, the marked items end at 0% after {rounds} of Grover's algorithm, so every run misses.",
            "100%" => $"{missed} The circuit drawn here finds a marked item every time, so the server's run went differently.",
            _ => $"{missed} A run finds a marked item about {chance} of the time.",
        };
    }

    // "1 round", "2 rounds" (#902).
    private static string Rounds(int rounds) => rounds == 1 ? "1 round" : $"{rounds} rounds";

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

    // The bits the server measured, in its variable order (see Item). On any failure of the quantum service, including a
    // response with no bit string to read as an item (#538), this throws SolverUnavailableException (HTTP 503
    // solver_unavailable) rather than handing Solve something that is not a bit string (#625).
    private string SolveAsSat(UNSTRUCTUREDSEARCH problem, string problemInstance, List<KeyValuePair<string, long>> counts) {
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
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("answer_bitstring", out JsonElement answerElement) && answerElement.ValueKind == JsonValueKind.String
                && answerElement.GetString() is { Length: > 0 and <= 31 } bits && bits.All(c => c is '0' or '1')) {
                return bits;
            }

            // No bit string to read as an item: not an answer to pass on.
            throw QuantumServerAPI.MissingAnswer("answer_bitstring", response);
        });
    }

}
