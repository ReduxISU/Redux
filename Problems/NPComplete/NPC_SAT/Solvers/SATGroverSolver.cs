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

    // The quantum service (ReduxISU/quantumsolver, sat_quantum.py) numbers the formula's variables by sorting their names
    // (every run of letters, digits and '_', which are exactly SAT's variable names) in code-point order, and sends
    // answer_bitstring with character i for variable i. That is read back as SAT's certificate, "(x1:True,x2:False)" (#832).
    // With no bit string to read (the service is not configured, could not be reached, or did not send one bit per
    // variable) there is no answer, and this throws SolverUnavailableException (HTTP 503 solver_unavailable) rather than
    // return anything a verifier would take for a certificate (#625).
    public string solve(SAT problem) {
        var requestBody = new JSON_Sat_Problem(problem.instance);

        return QuantumServerAPI.Ask(this, problem.problemName, client => {
            // Make the API call to the quantum endpoint
            string response = client.PostAsync("/sat-quantum", requestBody).Result;

            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("qasm", out JsonElement circuitElement) && circuitElement.ValueKind == JsonValueKind.String) {
                problem.circuit = circuitElement.GetString() ?? "";
            }

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("answer_bitstring", out JsonElement bitsElement) && bitsElement.ValueKind == JsonValueKind.String) {
                string bits = bitsElement.GetString() ?? "";
                return Assignment(problem.instance, bits)
                    ?? throw new InvalidDataException($"its bit string '{bits}' is not one bit per variable of the formula");
            }

            // No bit string in the response: not an answer to pass on.
            throw QuantumServerAPI.MissingAnswer("answer_bitstring", response);
        });
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
