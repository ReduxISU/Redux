using API.Interfaces;
using API.Tools;
using System.Text.Json;

namespace API.Problems.NPComplete.NPC_DEUTSCHJOZSA.Solvers;

/// <summary>
/// Deutsch-Jozsa solver that uses an external quantum computing API.
/// Supports both ISU AWS server and local server configurations.
/// </summary>
class DeutschJozsaQuantumSolver : ISolver<DEUTSCHJOZSA> {

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

    // On any failure of the quantum service this throws SolverUnavailableException (HTTP 503 solver_unavailable), never
    // an error text in place of the answer (#625).
    public string solve(DEUTSCHJOZSA problem) {
        // Convert the list of integers to boolean array for the API
        bool[] requestBody = problem.w.Select(val => val != 0).ToArray();

        return QuantumServerAPI.Ask(this, problem.problemName, client => {
            // Make the API call to the quantum endpoint
            string response = client.PostAsync("/deutsch-jozsa-quantum", requestBody).Result;

            // Parse the JSON response and extract just the answer
            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("answer", out JsonElement answerElement)
                && answerElement.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(answerElement.GetString())) {
                return answerElement.GetString()!;
            }

            // No answer in the response: not an answer to pass on.
            throw QuantumServerAPI.MissingAnswer("answer", response);
        });
    }
}
