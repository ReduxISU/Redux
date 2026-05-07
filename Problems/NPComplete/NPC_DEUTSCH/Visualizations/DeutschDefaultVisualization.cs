using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_DEUTSCH;
using API.Problems.NPComplete.NPC_DEUTSCH.Solvers;
using API.Tools;
using System.Text.Json;

class DeutschDefaultVisualization : IVisualization<DEUTSCH>
{
    public string visualizationName { get; } = "Deutsch Quantum Circuit (Q)";
    public string visualizationDefinition { get; } = "Requests the QASM for the two-qubit Deutsch circuit, showing the oracle call and measurement that separates constant vs. balanced functions in one query for Q.js rendering.";

    public string source { get; } = "";
    public string[] contributors { get; } = { "Jason L. Wright", "Grant Gardner", "Courtney Bodily", "Andreas Kramer", "Rakesh Itani" };
    public string visualizationType { get; } = "Quantum Circuit Q.js";
    public ISolver solver { get; } = new DeutschClassicalSolver();

    // --- Methods Including Constructors ---
    public DeutschDefaultVisualization()
    {

    }
    public API_JSON visualize(DEUTSCH instance)
    {
        return new API_QUANTUMCIRCUIT
        {
            format = QuantumCircuitFormat.QASM,
            qasm = "",
            solution = ""
        };

    }

    public API_JSON SolvedVisualization(DEUTSCH instance, string solution)
    {
        var qc = new API_QUANTUMCIRCUIT
        {
            solution = solution,
            format = QuantumCircuitFormat.QASM
        };

        try
        {
            // Get the function values from the problem instance
            bool[] requestBody = instance.funcValues;

            // Create the API client
            var client = new QuantumServerAPI();

            // Make the API call to get the full response including QASM
            string response = client.PostAsync("/deutsch-quantum", requestBody).Result;

            // Parse the JSON response and extract the qasm field
            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("qasm", out JsonElement qasmElement))
            {
                qc.qasm = qasmElement.GetString() ?? "";
            }
        }
        catch (Exception)
        {
            // If API call fails, leave circuit empty
            qc.qasm = "";
        }

        return qc;
    }
}
