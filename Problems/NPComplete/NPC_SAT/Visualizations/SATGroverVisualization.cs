using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT.Solvers;
using API.Tools;
using System.Text.Json;

class SATGroverVisualization : IVisualization<SAT>
{
    public string visualizationName { get; } = "SAT Gover Solver (Q)";
    public string visualizationDefinition { get; } = "This visualization builds a quantum circuit from the Boolean expression and then uses Grover's algorithm to find a bit string x such that f(x) = 1.";
    public string source { get; } = "Brassard, G., Hoyer, P., Mosca, M., & Tapp, A. (2000), Quantum Amplitude Amplification and Estimation";
    public string[] contributors { get; } = { "Courtney Bodily", "Andreas Kramer", "Rakesh Itani", "Grant Gardner", "Jason L. Wright" };
    public string visualizationType { get; } = "Quantum Circuit Q.js";
    public ISolver solver { get; } = new SATGroverSolver();

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
            var client = new QuantumServerAPI();

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
