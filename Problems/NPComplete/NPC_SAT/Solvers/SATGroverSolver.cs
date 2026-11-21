using API.Interfaces;
using API.Tools;
using System.Text.Json;

namespace API.Problems.NPComplete.NPC_SAT.Solvers;

/// <summary>
/// SAT solver that uses an external quantum computing API which
/// in turn uses Grover's algorithm to produce a probablistic
/// solution to the instance.
/// </summary>
/// 
class SATGroverSolver : ISolver<SAT> {
    // --- Fields ---
    public string solverName { get; } = "Grover's Algorithm";
    public string solverDefinition { get; } = "This solver builds the expression as a quantum circuit and then uses Grover's algorithm to probablisticly detemine a solution";
    public string source { get; } = "External API: towel.aws.cose.isu.edu:8080 or localhost:5000";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Jason L. Wright" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Delegates to the external quantum-simulator service.
    public SolverType solverType { get; } = SolverType.Quantum;
    // Grover search over the 2^v possible assignments to the formula's v boolean variables
    // takes O(sqrt(2^v)) oracle queries; this solver ships the whole formula to the quantum
    // endpoint in a single call that runs that search.
    public MathematicalFunction timeComplexity { get; } = new("sqrt(2^v)", " oracle queries, where v is the number of boolean variables in the formula");

    // --- Constructors ---

    /// <summary>
    /// Creates a new BernsteinVaziraniQuantumSolver using the ISU AWS server by default
    /// </summary>
    public SATGroverSolver() {
    }

    public class JSON_Sat_Problem {
        public string boolexpr { get; set; }
        public JSON_Sat_Problem(string expr) {
            boolexpr = expr;
        }
    }

    // --- Methods ---

    // On any failure of the quantum service this throws SolverUnavailableException (HTTP 503 solver_unavailable), never
    // an error text in place of the answer (#625).
    public string solve(SAT problem) {
        var requestBody = new JSON_Sat_Problem(problem.instance);

        return QuantumServerAPI.Ask(this, problem.problemName, client => {
            // Make the API call to the quantum endpoint
            string response = client.PostAsync("/sat-quantum", requestBody).Result;

            // Parse the JSON response and extract just the answer
            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("qasm", out JsonElement circuitElement)) {
                problem.circuit = circuitElement.GetString() ?? "";
            }

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("answer", out JsonElement answerElement)
                && answerElement.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(answerElement.GetString())) {
                return answerElement.GetString()!;
            }

            // No answer in the response: not an answer to pass on.
            throw QuantumServerAPI.MissingAnswer("answer", response);
        });
    }

}
