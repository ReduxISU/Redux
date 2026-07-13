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
    public string solverName {get;} = "Bernstein-Vazirani Quantum API Solver";
    public string solverDefinition {get;} = "Calls external quantum computing API to solve Bernstein-Vazirani's algorithm";
    public string source {get;} = "External API: towel.aws.cose.isu.edu:8080 or localhost:5000";
    public string[] contributors {get;} = { "Grant Gardner" };
    public bool timerHasExpired { get; set; }

    // --- Constructors ---

    /// <summary>
    /// Creates a new BernsteinVaziraniQuantumSolver
    /// </summary>
    public BernsteinVaziraniQuantumSolver()
    {
    }

    // --- Methods ---

    public string solve(BERNSTEINVAZIRANI problem)
    {
        try
        {
            // Get the function values as a boolean array
            bool[] requestBody = problem.funcValues.ToArray();

            // Create the API client
            var client = new QuantumServerAPI();

            // Make the API call to the quantum endpoint
            string response = client.PostAsync("/bernstein-vazirani-quantum", requestBody).Result;

            // Parse the JSON response and extract just the answer
            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("answer", out JsonElement answerElement))
            {
                string answer = answerElement.GetString() ?? "No answer found";
                // Quantum server returns a bare bit string ("101"); translate to certificate format
                UtilCollection result = new("()");
                foreach (char c in answer)
                    result.Add(new UtilCollection(c.ToString()));
                return result.ToString();
            }

            // If no answer field, return the whole response
            return response;
        }
        catch (Exception ex)
        {
            // Return error information in case of failure
            return $"{{\"error\": \"{ex.Message}\"}}";
        }
    }
}
