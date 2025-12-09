using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_DEUTSCHJOZSA;
using API.Tools;
using System.Text.Json;

class DeutschJozsaDefaultVisualization : IVisualization<DEUTSCHJOZSA>
{
    public string visualizationName { get; } = "Deutsch-Jozsa problem visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for the Deutsch-Jozsa problem";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Grant Gardner" };
    public string visualizationType { get; } = "Quantum Circuit";

    // --- Methods Including Constructors ---
    public DeutschJozsaDefaultVisualization()
    {

    }
    public API_JSON visualize(DEUTSCHJOZSA instance)
    {
        return new API_QUANTUMCIRCUIT();

    }

    public API_JSON SolvedVisualization(DEUTSCHJOZSA instance, string solution)
    {
        var qc = new API_QUANTUMCIRCUIT();
        qc.solution = solution;

        try
        {
            // Convert the list of integers to boolean array for the API
            bool[] requestBody = instance.w.Select(val => val != 0).ToArray();

            // Create the API client
            var client = new QuantumServerAPI(QuantumServerAPI.ServerEnvironment.ISU_AWS);

            // Make the API call to get the full response including QASM
            string response = client.PostAsync("/deutsch-jozsa-quantum", requestBody).Result;

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
