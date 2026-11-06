using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;
using API.Tools;
using System.Text.Json;

namespace API.Problems.NPComplete.NPC_DEUTSCHJOZSA.Solvers;

/// <summary>
/// Deutsch-Jozsa solver that uses an external quantum computing API.
/// Supports both ISU AWS server and local server configurations.
/// </summary>
// The steps the Deutsch-Jozsa Algorithm records, for the Quantum Circuit picture:
//   - an Update per column of the Deutsch-Jozsa circuit, in order: the circuit the server runs, drawn and simulated here
//     (the server is never asked for these). Applying gates has no verdict;
//   - an Accept with what the server measured (all zeros: constant; anything else: balanced), or a Reject when its answer
//     is neither;
//   - the Done, with the verifier's verdict.
// With no answer from the server there is no answer at all: Ask throws SolverUnavailableException (HTTP 503
// solver_unavailable). Incremental: the circuit is applied one column at a time, and then read.
class DeutschJozsaQuantumSolver : ISolver<DEUTSCHJOZSA, QuantumProgress>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "Deutsch-Jozsa Algorithm";
    public string solverDefinition { get; } = "Calls external quantum computing API to solve Deutsch-Jozsa's algorithm";
    public string source { get; } = "External API: towel.aws.cose.isu.edu:8080 or localhost:5000";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Grant Gardner" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Delegates to the external quantum-simulator service.
    public SolverType solverType { get; } = SolverType.Quantum;
    // The Deutsch-Jozsa algorithm resolves constant-vs-balanced with exactly one query to
    // the oracle, regardless of n; this solver makes a single call to the external endpoint.
    public MathematicalFunction timeComplexity { get; } = new("1", " oracle queries");

    // --- Constructors ---

    /// <summary>
    /// Creates a new DeutschJozsaQuantumSolver
    /// </summary>
    public DeutschJozsaQuantumSolver() {
    }

    // --- Methods ---

    // How the steps show the measurement (the answer read, null when the server's reply is not one): read off the end of
    // the whole circuit.
    private static RunSteps<string?, QuantumProgress> NewSteps(StepRecorder<QuantumProgress> rec, int columns, long? outcome,
        List<KeyValuePair<string, long>> counts) => new(rec,
        // What every picture shows: the whole circuit applied, the bits measured, how often the server saw each outcome,
        // and the answer.
        partial: answer => new QuantumProgress(columns, outcome: outcome, counts: counts, result: answer),
        // The circuit's last column, where the answer is read (nothing is read from a reply that is not an answer).
        focus: answer => answer != null && columns > 0 ? [QuantumCircuits.ColumnId(columns - 1)] : []);

    public string solve(DEUTSCHJOZSA problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // Asks the quantum server to run the Deutsch-Jozsa circuit: one oracle call, after which the input register reads all
    // zeros exactly when f is constant.
    public string Solve(DEUTSCHJOZSA problem, StepRecorder<QuantumProgress> rec) {
        // The circuit is built and simulated only for the steps: a plain solve never builds it.
        var spec = QuantumSteps.Recording(rec) ? QuantumCircuits.DeutschJozsa(problem.w) : null;
        if (spec != null) QuantumSteps.Columns(rec, spec);
        var counts = new List<KeyValuePair<string, long>>();
        string answer = Ask(problem, counts);
        if (spec != null) {
            int n = problem.n;
            string zeros = QuantumCircuits.Bits(0, n);
            // Constant reads all zeros; balanced reads something else, which only the server's counts can say.
            long? outcome = answer == "constant" ? 0 : answer == "balanced" ? QuantumSteps.MostFrequent(counts, n) : null;
            var steps = NewSteps(rec, spec.Columns.Count, outcome, counts);
            if (answer == "constant")
                steps.Accept(answer, $"The quantum server measured all zeros ({zeros}){QuantumSteps.Shots(counts, zeros)}, so f is constant. One oracle call.");
            else if (answer == "balanced")
                steps.Accept(answer, $"The quantum server measured {(outcome is long y ? QuantumCircuits.Bits(y, n) : "an outcome")}, not all zeros, so f is balanced. One oracle call.");
            else
                steps.Reject(null, $"The quantum server gave no answer: {answer}");

            var final = new QuantumProgress(spec.Columns.Count, outcome: outcome, counts: counts, result: answer);
            if (QuantumSteps.Verdict(() => problem.defaultVerifier.verify(problem, answer)))
                steps.Done(final, true, $"f is {answer}, found with one call to the oracle.");
            else if (answer is "constant" or "balanced")
                steps.Done(final, false, $"The answer {answer} is not correct.");
            else
                steps.Done(final, false, $"No answer: the quantum server did not give one.");
        }
        return answer;
    }

    // The server's answer; its counts, when it sends them, go into counts. On any failure of the quantum service this
    // throws SolverUnavailableException (HTTP 503 solver_unavailable), never an error text in place of the answer.
    private string Ask(DEUTSCHJOZSA problem, List<KeyValuePair<string, long>> counts) {
        // Convert the list of integers to boolean array for the API
        bool[] requestBody = problem.w.Select(val => val != 0).ToArray();

        return QuantumServerAPI.Ask(this, problem.problemName, client => {
            // Make the API call to the quantum endpoint
            string response = client.PostAsync("/deutsch-jozsa-quantum", requestBody).Result;

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
