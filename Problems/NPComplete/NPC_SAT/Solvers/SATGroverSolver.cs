using API.Interfaces;
using API.Tools;
using System.Text.Json;
using System.Text.RegularExpressions;

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
    public string complexity { get; } = "O(sqrt(2^v)) oracle queries, where v is the number of boolean variables in the formula";

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

    // No answer: not "No solution exists", which would say the formula has none.
    private const string NoAnswer = "{}";

    // The quantum service (ReduxISU/quantumsolver, sat_quantum.py) numbers the formula's variables by sorting their names
    // (every run of letters, digits and '_', which are exactly SAT's variable names) in code-point order, and sends
    // answer_bitstring with character i for variable i. That is read back as SAT's certificate, "(x1:True,x2:False)" (#832).
    // With no bit string to read (the service could not be reached, or did not send one) there is no answer, "{}".
    public string solve(SAT problem) {
        string response;
        try {
            var requestBody = new JSON_Sat_Problem(problem.instance);

            // Create the API client
            var client = new QuantumServerAPI();

            // Make the API call to the quantum endpoint
            response = client.PostAsync("/sat-quantum", requestBody).Result;
        } catch (Exception) {
            return NoAnswer;
        }

        try {
            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("qasm", out JsonElement circuitElement) && circuitElement.ValueKind == JsonValueKind.String) {
                problem.circuit = circuitElement.GetString() ?? "";
            }

            if (root.TryGetProperty("answer_bitstring", out JsonElement bitsElement) && bitsElement.ValueKind == JsonValueKind.String) {
                return Assignment(problem.instance, bitsElement.GetString() ?? "") ?? NoAnswer;
            }
        } catch (JsonException) {
            // Not JSON: no answer, as below.
        }
        return NoAnswer;
    }

    /// <summary>
    /// The variables of <paramref name="formula"/> in the quantum service's order: every distinct name, sorted by code point
    /// (Python's <c>sorted(set(re.findall(r"[A-Za-z0-9_]+", expr)))</c>).
    /// </summary>
    public static List<string> ServiceVariableOrder(string formula) =>
        Regex.Matches(formula, "[A-Za-z0-9_]+").Select(m => m.Value).Distinct().OrderBy(v => v, StringComparer.Ordinal).ToList();

    /// <summary>
    /// <paramref name="bits"/> (character i for variable i of <see cref="ServiceVariableOrder"/>) as SAT's certificate, or null
    /// when it is not one bit per variable.
    /// </summary>
    public static string? Assignment(string formula, string bits) {
        var variables = ServiceVariableOrder(formula);
        if (variables.Count == 0 || bits.Length != variables.Count || bits.Any(c => c is not ('0' or '1'))) return null;
        return "(" + string.Join(",", variables.Select((v, i) => $"{v}:{(bits[i] == '1' ? "True" : "False")}")) + ")";
    }

}
