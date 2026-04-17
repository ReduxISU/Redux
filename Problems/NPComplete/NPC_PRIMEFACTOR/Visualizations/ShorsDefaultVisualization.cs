using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_PRIMEFACTOR;
using API.Problems.NPComplete.NPC_PRIMEFACTOR.Solvers;
using API.Tools;
using System.Text.Json;

class ShorsDefaultVisualization : IVisualization<PRIMEFACTOR>
{
    public string visualizationName { get; } = "Shor's Algorithm Quantum Visualization";
    public string visualizationDefinition { get; } = "Constructs a quantum circuit to represent Shor's algorithm for prime factorization and simulates the circuit to find the prime factors.";
    public string source { get; } = "https://arxiv.org/abs/quant-ph/9708016";
    public string[] contributors { get; } = { "Grant Gardner", "Jason L. Wright", "George Lake" };
    public string visualizationType { get; } = "Quantum Circuit";
    public ISolver solver { get; } = new PrimeFactorSolver();

    // --- Methods Including Constructors ---
    public ShorsDefaultVisualization()
    {

    }

    public API_JSON visualize(PRIMEFACTOR instance)
    {
        return new API_QUANTUMCIRCUIT
        {
            format = QuantumCircuitFormat.QASM,
            qasm = "",
            solution = ""
        };
    }

    public API_JSON SolvedVisualization(PRIMEFACTOR instance, string solution)
    {
        var qc = new API_QUANTUMCIRCUIT
        {
            solution = solution,
            format = QuantumCircuitFormat.QASM
        };

        try
        {
            // Parse the integer from the problem instance
            int numberToFactor = int.Parse(instance.instance);

            // Create the API client
            var client = new QuantumServerAPI(QuantumServerAPI.ServerEnvironment.ISU_AWS);

            // Make the API call to get the full response including QASM
            // The API expects just the raw number as the body
            string response = client.PostRawJsonAsync("/prime-factorization-quantum", numberToFactor.ToString()).Result;

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
