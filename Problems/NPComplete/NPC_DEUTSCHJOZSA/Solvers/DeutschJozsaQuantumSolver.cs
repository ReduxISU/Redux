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
    public string solverName {get;} = "Deutsch-Jozsa Quantum API Solver";
    public string solverDefinition {get;} = "Calls external quantum computing API to solve Deutsch-Jozsa's algorithm";
    public string source {get;} = "External API: towel.aws.cose.isu.edu:8080 or localhost:5000";
    public string[] contributors {get;} = { "Grant Gardner" };
    public bool timerHasExpired { get; set; }

    // Configuration: Change this to switch between servers
    private readonly QuantumServerAPI.ServerEnvironment _serverEnvironment;

    // --- Constructors ---

    /// <summary>
    /// Creates a new DeutschJozsaQuantumSolver using the ISU AWS server by default
    /// </summary>
    public DeutschJozsaQuantumSolver()
    {
        _serverEnvironment = QuantumServerAPI.ServerEnvironment.ISU_AWS;
    }

    /// <summary>
    /// Creates a new DeutschJozsaQuantumSolver with specified server environment
    /// </summary>
    /// <param name="environment">The server environment to use</param>
    public DeutschJozsaQuantumSolver(QuantumServerAPI.ServerEnvironment environment)
    {
        _serverEnvironment = environment;
    }

    // --- Methods ---

    public string solve(DEUTSCHJOZSA problem)
    {
        try
        {
            // Convert the list of integers to boolean array for the API
            bool[] requestBody = problem.w.Select(val => val != 0).ToArray();

            // Create the API client
            var client = new QuantumServerAPI(_serverEnvironment);

            // Make the API call to the quantum endpoint
            string response = client.PostAsync("/deutsch-jozsa-quantum", requestBody).Result;

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
