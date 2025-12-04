using API.Interfaces;
using API.Tools;
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

    // Configuration: Change this to switch between servers
    private readonly QuantumServerAPI.ServerEnvironment _serverEnvironment;

    // --- Constructors ---

    /// <summary>
    /// Creates a new BernsteinVaziraniQuantumSolver using the ISU AWS server by default
    /// </summary>
    public BernsteinVaziraniQuantumSolver()
    {
        _serverEnvironment = QuantumServerAPI.ServerEnvironment.ISU_AWS;
    }

    /// <summary>
    /// Creates a new BernsteinVaziraniQuantumSolver with specified server environment
    /// </summary>
    /// <param name="environment">The server environment to use</param>
    public BernsteinVaziraniQuantumSolver(QuantumServerAPI.ServerEnvironment environment)
    {
        _serverEnvironment = environment;
    }

    // --- Methods ---

    public string solve(BERNSTEINVAZIRANI problem)
    {
        try
        {
            // Get the function values as a boolean array
            bool[] requestBody = problem.funcValues.ToArray();

            // Create the API client
            var client = new QuantumServerAPI(_serverEnvironment);

            // Make the API call to the quantum endpoint
            string response = client.PostAsync("/bernstein-vazirani-quantum", requestBody).Result;

            // Parse the JSON response and extract just the answer
            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("answer", out JsonElement answerElement))
            {
                return answerElement.GetString() ?? "No answer found";
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
