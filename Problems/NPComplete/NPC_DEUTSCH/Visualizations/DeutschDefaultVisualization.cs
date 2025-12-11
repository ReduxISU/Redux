using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_DEUTSCH;
using API.Tools;
using System.Text.Json;

class DeutschDefaultVisualization : IVisualization<DEUTSCH>
{
    public string visualizationName { get; } = "Deutsch quantum visualization";
    public string visualizationDefinition { get; } = "Constructs a quantum circuit to represent the oracle and then simulates the circuit to find the solution.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Jason L. Wright", "Grant Gardner" };
    public string visualizationType { get; } = "Quantum Circuit";

    // --- Methods Including Constructors ---
    public DeutschDefaultVisualization()
    {

    }
    public API_JSON visualize(DEUTSCH instance)
    {
        return new API_QUANTUMCIRCUIT();

    }

    public API_JSON SolvedVisualization(DEUTSCH instance, string solution)
    {
        var qc = new API_QUANTUMCIRCUIT();
        qc.solution = solution;

        try
        {
            // Get the function values from the problem instance
            bool[] requestBody = instance.funcValues;

            // Create the API client
            var client = new QuantumServerAPI(QuantumServerAPI.ServerEnvironment.ISU_AWS);

            // Make the API call to get the full response including QASM
            string response = client.PostAsync("/deutsch-quantum", requestBody).Result;

            // Parse the JSON response and extract the qasm field
            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("qasm", out JsonElement qasmElement))
            {
                qc.circuit = qasmElement.GetString() ?? "";
            }
        }
        catch (Exception)
        {
            // If API call fails, leave circuit empty
            qc.circuit = "";
        }

        return qc;
    }
}
