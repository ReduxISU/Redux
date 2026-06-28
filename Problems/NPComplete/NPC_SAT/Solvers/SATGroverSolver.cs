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
class SATGroverSolver : ISolver<SAT>
{
    // --- Fields ---
    public string solverName { get; } = "SAT Solver using Grover's Quantum computing algorithm.";
    public string solverDefinition { get; } = "This solver builds the expression as a quantum circuit and then uses Grover's algorithm to probablisticly detemine a solution";
    public string source {get;} = "External API: towel.aws.cose.isu.edu:8080 or localhost:5000";
    public string[] contributors {get;} = { "Jason L. Wright" };
    public bool timerHasExpired { get; set; }

    // --- Constructors ---

    /// <summary>
    /// Creates a new BernsteinVaziraniQuantumSolver using the ISU AWS server by default
    /// </summary>
    public SATGroverSolver()
    {
    }

    public class JSON_Sat_Problem
    {
        public string boolexpr {get; set;}
        public JSON_Sat_Problem(string expr)
        {
            boolexpr = expr;
        }
    }

    // --- Methods ---

    public string solve(SAT problem)
    {
        try
        {
            var requestBody = new JSON_Sat_Problem(problem.instance);

            // Create the API client
            var client = new QuantumServerAPI();

            // Make the API call to the quantum endpoint
            string response = client.PostAsync("/sat-quantum", requestBody).Result;

            // Parse the JSON response and extract just the answer
            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("qasm", out JsonElement circuitElement))
            {
                problem.circuit = circuitElement.GetString() ?? "";
            }

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
