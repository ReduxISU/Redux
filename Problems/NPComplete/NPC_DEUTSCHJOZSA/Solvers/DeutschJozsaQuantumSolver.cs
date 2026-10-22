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
// Incremental: the circuit is applied one column at a time, and then read.
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

    public string solve(DEUTSCHJOZSA problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // Steps: a Try per column of the Deutsch-Jozsa circuit (the one the server runs, drawn and simulated here, never asked of
    // the server), then an Accept with what the server measured (a Reject when its answer is not one; with no answer at
    // all the solver throws SolverUnavailableException, #625), and Done with the verdict.
    public string Solve(DEUTSCHJOZSA problem, StepRecorder<QuantumProgress> rec) {
        var spec = QuantumSteps.Recording(rec) ? QuantumCircuits.DeutschJozsa(problem.w) : null;
        if (spec != null) QuantumSteps.Columns(rec, spec);
        var counts = new List<KeyValuePair<string, long>>();
        string answer = Ask(problem, counts);
        if (spec != null) {
            int all = spec.Columns.Count, n = problem.n;
            string zeros = QuantumCircuits.Bits(0, n);
            // Constant reads all zeros; balanced reads something else, which only the server's counts can say.
            long? outcome = answer == "constant" ? 0 : answer == "balanced" ? QuantumSteps.MostFrequent(counts, n) : null;
            string[] read = all > 0 ? [QuantumCircuits.ColumnId(all - 1)] : [];
            if (answer is "constant" or "balanced")
                rec.Accept(() => new QuantumProgress(all, outcome: outcome, counts: counts, result: answer), () => answer == "constant"
                    ? $"The quantum server measured all zeros ({zeros}){QuantumSteps.Shots(counts, zeros)}, so f is constant. One oracle call."
                    : $"The quantum server measured {(outcome is long y ? QuantumCircuits.Bits(y, n) : "an outcome")}, not all zeros, so f is balanced. One oracle call.",
                    read);
            else
                rec.Reject(() => new QuantumProgress(all, counts: counts), () => $"The quantum server gave no answer: {answer}");
            QuantumSteps.Done(rec, () => problem.defaultVerifier.verify(problem, answer), new QuantumProgress(all, outcome: outcome, counts: counts, result: answer),
                ok => ok ? $"f is {answer}, found with one call to the oracle."
                    : answer is "constant" or "balanced" ? $"The answer {answer} is not correct." : "No answer: the quantum server did not give one.");
        }
        return answer;
    }

    // The server's answer; its counts, when it sends them, go into counts. On any failure of the quantum service this
    // throws SolverUnavailableException (HTTP 503 solver_unavailable), never an error text in place of the answer (#625).
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
