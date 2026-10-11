using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI;
using API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI.Solvers;
using API.Tools;
using System.Text.Json;

/// <summary>
/// The Quantum Circuit picture (format=frames) comes from the instance's circuit and the chosen solver's steps; the list
/// format below is unchanged.
/// </summary>
class BernsteinVaziraniDefaultVisualization : IVisualization<BERNSTEINVAZIRANI, API_QUANTUMCIRCUIT>, IQuantumCircuitVisualization<BERNSTEINVAZIRANI> {
    public string visualizationName { get; } = "Bernstein-Vazirani Quantum Circuit (Q)";
    public string visualizationDefinition { get; } = "Requests QASM for the Bernstein-Vazirani circuit (Hadamards, oracle, measure data qubits) to show how one query recovers the hidden bit string for Q.js rendering.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Courtney Bodily", "Andreas Kramer", "Rakesh Itani", "Grant Gardner" };
    public VisualizationType visualizationType { get; } = VisualizationType.QuantumCircuitQjs;
    public ISolver solver { get; } = new BernsteinVaziraniClassicalSolver();

    // The Quantum Circuit picture (format=frames only). Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(QuantumProgress);
    QuantumCircuitSpec IQuantumCircuitVisualization<BERNSTEINVAZIRANI>.Describe(BERNSTEINVAZIRANI problem) => QuantumCircuits.BernsteinVazirani(problem.funcValues);

    // --- Methods Including Constructors ---
    public BernsteinVaziraniDefaultVisualization() {

    }
    public API_QUANTUMCIRCUIT visualize(BERNSTEINVAZIRANI instance) {
        return new API_QUANTUMCIRCUIT {
            format = QuantumCircuitFormat.QASM,
            qasm = "",
            solution = ""
        };

    }

    public API_JSON SolvedVisualization(BERNSTEINVAZIRANI instance, string solution) {
        var qc = new API_QUANTUMCIRCUIT {
            solution = solution,
            format = QuantumCircuitFormat.QASM
        };

        try {
            // Get the function values as a boolean array
            bool[] requestBody = instance.funcValues.ToArray();

            // Create the API client
            var client = new QuantumServerAPI();

            // Make the API call to get the full response including QASM
            string response = client.PostAsync("/bernstein-vazirani-quantum", requestBody).Result;

            // Parse the JSON response and extract the qasm field
            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("qasm", out JsonElement qasmElement)) {
                qc.qasm = qasmElement.GetString() ?? "";
            }
        } catch (Exception) {
            // If API call fails, leave circuit empty
            qc.qasm = "";
        }

        return qc;
    }
}
