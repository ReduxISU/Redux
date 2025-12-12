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
    public string solverName { get; } = "Deutsch quantum solver";
    public string solverDefinition { get; } = "This solver constructs a quantum circuit for f(x) and then uses phase kickback to determine whether the oracle is constant or balanced with a single invocation of a quantum simulator (qiskit)";
    public string source { get; } = "https://arxiv.org/abs/quant-ph/9708016";
    public string[] contributors {get;} = { "Grant Gardner" };

    // Configuration: Change this to switch between servers
    private readonly QuantumServerAPI.ServerEnvironment _serverEnvironment;

    // --- Constructors ---
    
    /// <summary>
    /// Creates a new DeutschQuantumSolver using the ISU AWS server by default
    /// </summary>
    public DeutschQuantumSolver() 
    {
        _serverEnvironment = QuantumServerAPI.ServerEnvironment.ISU_AWS;
    }

    /// <summary>
    /// Creates a new DeutschQuantumSolver with specified server environment
    /// </summary>
    /// <param name="environment">The server environment to use</param>
    public DeutschQuantumSolver(QuantumServerAPI.ServerEnvironment environment)
    {
        _serverEnvironment = environment;
    }

    // --- Methods ---
    
    public string solve(DEUTSCH problem)
    {
        try
        {
            // Get the function values directly from the problem instance
            bool[] requestBody = problem.funcValues;

            // Create the API client
            var client = new QuantumServerAPI(_serverEnvironment);

            // Make the API call to the quantum endpoint
            string response = client.PostAsync("/deutsch-quantum", requestBody).Result;

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
