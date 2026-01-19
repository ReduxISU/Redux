using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT.Solvers;
using API.Tools;
using System.Text.Json;

class SATGroverVisualization : IVisualization<SAT>
{
    public string visualizationName { get; } = "Deutsch-Jozsa Quantum Circuit (Q)";
    public string visualizationDefinition { get; } = "Requests the QASM for the Deutsch-Jozsa circuit (data qubits plus ancilla), showing the oracle and measurements that separate constant vs. balanced functions in one query for Q.js.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Courtney Bodily", "Andreas Kramer", "Rakesh Itani", "Grant Gardner" };
    public string visualizationType { get; } = "Quantum Circuit Q.js";

    // --- Methods Including Constructors ---
    public SATGroverVisualization()
    {

    }
    public API_JSON visualize(SAT instance)
    {
        return new API_QUANTUMCIRCUIT
        {
            format = QuantumCircuitFormat.QASM,
            qasm = "",
            solution = ""
        };

    }

    public API_JSON SolvedVisualization(SAT instance, string solution)
    {
        var qc = new API_QUANTUMCIRCUIT
        {
            solution = solution,
            format = QuantumCircuitFormat.QASM
        };

        try
        {
            // Convert the list of integers to boolean array for the API
            var requestBody = new SATGroverSolver.JSON_Sat_Problem(instance.instance);

            // Create the API client
            var client = new QuantumServerAPI(QuantumServerAPI.ServerEnvironment.ISU_AWS);

            // Make the API call to get the full response including QASM
            string response = client.PostAsync("/sat-quantum", requestBody).Result;

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
