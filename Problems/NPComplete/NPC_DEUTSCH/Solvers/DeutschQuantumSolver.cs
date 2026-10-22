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
// Incremental: the circuit is applied one column at a time, and then read.
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

    public string solve(DEUTSCH problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // Steps: a Try per column of Deutsch's circuit (the one the server runs, drawn and simulated here, never asked of the
    // server), then an Accept with what the server measured (a Reject when its answer is not one; with no answer at all
    // the solver throws SolverUnavailableException, #625), and Done with the verdict.
    public string Solve(DEUTSCH problem, StepRecorder<QuantumProgress> rec) {
        var spec = QuantumSteps.Recording(rec) ? QuantumCircuits.Deutsch(problem.funcValues) : null;
        if (spec != null) QuantumSteps.Columns(rec, spec);
        var counts = new List<KeyValuePair<string, long>>();
        string answer = Ask(problem, counts);
        if (spec != null) {
            int all = spec.Columns.Count;
            long? outcome = answer switch { "balanced" => 1, "constant" => 0, _ => null };
            if (outcome is long x0)
                rec.Accept(() => new QuantumProgress(all, outcome: x0, counts: counts, result: answer),
                    () => $"The quantum server measured x0 = {x0}{QuantumSteps.Shots(counts, x0.ToString())}, so f is {answer}. One oracle call.",
                    QuantumCircuits.ColumnId(all - 1));
            else
                rec.Reject(() => new QuantumProgress(all, counts: counts), () => $"The quantum server gave no answer: {answer}");
            QuantumSteps.Done(rec, () => problem.defaultVerifier.verify(problem, answer), new QuantumProgress(all, outcome: outcome, counts: counts, result: answer),
                ok => ok ? $"f is {answer}, found with one call to the oracle." : outcome == null ? "No answer: the quantum server did not give one." : $"The answer {answer} is not correct.");
        }
        return answer;
    }

    // The server's answer; its counts, when it sends them, go into counts. On any failure of the quantum service this
    // throws SolverUnavailableException (HTTP 503 solver_unavailable), never an error text in place of the answer (#625).
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
