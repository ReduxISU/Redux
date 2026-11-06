using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;
using API.Tools;
using System.Text.Json;

namespace API.Problems.NPComplete.NPC_DEUTSCH.Solvers;

/// <summary>
/// Deutsch solver that uses an external quantum computing API.
/// Supports both ISU AWS server and local server configurations.
/// </summary>
// The steps Deutsch's Algorithm records, for the Quantum Circuit picture:
//   - an Update per column of Deutsch's circuit, in order: the circuit the server runs, drawn and simulated here (the
//     server is never asked for these). Applying gates has no verdict;
//   - an Accept with what the server measured, x0 = 0 for constant or 1 for balanced, or a Reject when its answer is
//     neither;
//   - the Done, with the verifier's verdict.
// With no answer from the server there is no answer at all: Ask throws SolverUnavailableException (HTTP 503
// solver_unavailable). Incremental: the circuit is applied one column at a time, and then read.
class DeutschQuantumSolver : ISolver<DEUTSCH, QuantumProgress>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "Deutsch's Algorithm";
    public string solverDefinition { get; } = "This solver constructs a quantum circuit for f(x) and then uses phase kickback to determine whether the oracle is constant or balanced with a single invocation of a quantum simulator (qiskit)";
    public string source { get; } = "https://arxiv.org/abs/quant-ph/9708016";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Grant Gardner" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Delegates to the external quantum-simulator service.
    public SolverType solverType { get; } = SolverType.Quantum;
    // solverDefinition itself states this determines constant/balanced "with a single
    // invocation of a quantum simulator" -- one oracle query via phase kickback.
    public MathematicalFunction timeComplexity { get; } = new("1", " oracle queries");

    // --- Constructors ---

    /// <summary>
    /// Creates a new DeutschQuantumSolver
    /// </summary>
    public DeutschQuantumSolver() {
    }

    // --- Methods ---

    // How the steps show the measurement (the answer read, null when the server's reply is not one): read off the end of
    // the whole circuit.
    private static RunSteps<string?, QuantumProgress> NewSteps(StepRecorder<QuantumProgress> rec, int columns, long? outcome,
        List<KeyValuePair<string, long>> counts) => new(rec,
        // What every picture shows: the whole circuit applied, the bit measured, how often the server saw each outcome,
        // and the answer.
        partial: answer => new QuantumProgress(columns, outcome: outcome, counts: counts, result: answer),
        // The circuit's last column, where the answer is read (nothing is read from a reply that is not an answer).
        focus: answer => answer != null && columns > 0 ? [QuantumCircuits.ColumnId(columns - 1)] : []);

    public string solve(DEUTSCH problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // Asks the quantum server to run Deutsch's circuit: one oracle call, with phase kickback putting f(0) ⊕ f(1) on x0.
    public string Solve(DEUTSCH problem, StepRecorder<QuantumProgress> rec) {
        // The circuit is built and simulated only for the steps: a plain solve never builds it.
        var spec = QuantumSteps.Recording(rec) ? QuantumCircuits.Deutsch(problem.funcValues) : null;
        if (spec != null) QuantumSteps.Columns(rec, spec);
        var counts = new List<KeyValuePair<string, long>>();
        string answer = Ask(problem, counts);
        if (spec != null) {
            // x0 reads 0 for constant and 1 for balanced.
            long? outcome = answer switch { "balanced" => 1, "constant" => 0, _ => null };
            var steps = NewSteps(rec, spec.Columns.Count, outcome, counts);
            if (outcome is long x0)
                steps.Accept(answer, $"The quantum server measured x0 = {x0}{QuantumSteps.Shots(counts, x0.ToString())}, so f is {answer}. One oracle call.");
            else
                steps.Reject(null, $"The quantum server gave no answer: {answer}");

            var final = new QuantumProgress(spec.Columns.Count, outcome: outcome, counts: counts, result: answer);
            if (QuantumSteps.Verdict(() => problem.defaultVerifier.verify(problem, answer)))
                steps.Done(final, true, $"f is {answer}, found with one call to the oracle.");
            else if (outcome == null)
                steps.Done(final, false, $"No answer: the quantum server did not give one.");
            else
                steps.Done(final, false, $"The answer {answer} is not correct.");
        }
        return answer;
    }

    // The server's answer; its counts, when it sends them, go into counts. On any failure of the quantum service this
    // throws SolverUnavailableException (HTTP 503 solver_unavailable), never an error text in place of the answer.
    private string Ask(DEUTSCH problem, List<KeyValuePair<string, long>> counts) {
        // Get the function values directly from the problem instance
        bool[] requestBody = problem.funcValues;

        return QuantumServerAPI.Ask(this, problem.problemName, client => {
            // Make the API call to the quantum endpoint
            string response = client.PostAsync("/deutsch-quantum", requestBody).Result;

            // Parse the JSON response and extract just the answer
            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            counts.AddRange(QuantumSteps.Counts(root));
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("answer", out JsonElement answerElement)
                && answerElement.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(answerElement.GetString())) {
                return answerElement.GetString()!;
            }

            // No answer in the response: not an answer to pass on.
            throw QuantumServerAPI.MissingAnswer("answer", response);
        });
    }
}
