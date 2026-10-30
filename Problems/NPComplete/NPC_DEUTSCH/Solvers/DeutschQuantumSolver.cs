using API.Interfaces;
using API.Tools;
using System.Text.Json;

namespace API.Problems.NPComplete.NPC_DEUTSCH.Solvers;

/// <summary>
/// Deutsch solver that uses an external quantum computing API.
/// Supports both ISU AWS server and local server configurations.
/// </summary>
class DeutschQuantumSolver : ISolver<DEUTSCH> {

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

    // On any failure of the quantum service this throws SolverUnavailableException (HTTP 503 solver_unavailable), never
    // an error text in place of the answer (#625).
    public string solve(DEUTSCH problem) {
        // Get the function values directly from the problem instance
        bool[] requestBody = problem.funcValues;

        return QuantumServerAPI.Ask(this, problem.problemName, client => {
            // Make the API call to the quantum endpoint
            string response = client.PostAsync("/deutsch-quantum", requestBody).Result;

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
