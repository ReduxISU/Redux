using API.Interfaces;
using API.Tools;
using SPADE;
using System.Text.Json;

namespace API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI.Solvers;

/// <summary>
/// Bernstein-Vazirani solver that uses an external quantum computing API.
/// Supports both ISU AWS server and local server configurations.
/// </summary>
class BernsteinVaziraniQuantumSolver : ISolver<BERNSTEINVAZIRANI> {

    // --- Fields ---
    public string solverName { get; } = "Bernstein-Vazirani Algorithm";
    public string solverDefinition { get; } = "Calls external quantum computing API to solve Bernstein-Vazirani's algorithm";
    public string source { get; } = "External API: towel.aws.cose.isu.edu:8080 or localhost:5000";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Grant Gardner" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Delegates to the external quantum-simulator service.
    public SolverType solverType { get; } = SolverType.Quantum;
    // The Bernstein-Vazirani algorithm recovers the full n-bit secret string with exactly
    // one query to the oracle (a single call to the external quantum endpoint here).
    public MathematicalFunction timeComplexity { get; } = new("1", " oracle queries");

    // --- Constructors ---

    /// <summary>
    /// Creates a new BernsteinVaziraniQuantumSolver
    /// </summary>
    public BernsteinVaziraniQuantumSolver() {
    }

    // --- Methods ---

    // On any failure of the quantum service this throws SolverUnavailableException (HTTP 503 solver_unavailable), never
    // an error text in place of the answer (#625).
    public string solve(BERNSTEINVAZIRANI problem) {
        // Get the function values as a boolean array
        bool[] requestBody = problem.funcValues.ToArray();

        return QuantumServerAPI.Ask(this, problem.problemName, client => {
            // Make the API call to the quantum endpoint
            string response = client.PostAsync("/bernstein-vazirani-quantum", requestBody).Result;

            // Parse the JSON response and extract just the answer
            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("answer", out JsonElement answerElement)
                && answerElement.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(answerElement.GetString())) {
                string answer = answerElement.GetString()!;
                // Anything but binary digits is not an answer: fail as a missing one does, not pass it on (#625).
                if (answer.Any(c => c != '0' && c != '1'))
                    throw new InvalidDataException($"its 'answer' is not a bit string: {response}");
                // Quantum server returns a bare bit string ("101"); translate to certificate format
                UtilCollection result = new("()");
                foreach (char c in answer)
                    result.Add(new UtilCollection(c.ToString()));
                return result.ToString();
            }

            // No answer in the response: not an answer to pass on.
            throw QuantumServerAPI.MissingAnswer("answer", response);
        });
    }
}
