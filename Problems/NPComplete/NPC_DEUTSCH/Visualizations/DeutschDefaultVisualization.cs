using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_DEUTSCH;
using API.Problems.NPComplete.NPC_DEUTSCH.Solvers;
using API.Tools;
using System.Text.Json;

/// <summary>
/// The Quantum Circuit picture (format=frames) comes from the instance's circuit and the chosen solver's steps; the list
/// format below is unchanged.
/// </summary>
class DeutschDefaultVisualization : IVisualization<DEUTSCH, API_QUANTUMCIRCUIT>, IQuantumCircuitVisualization<DEUTSCH> {
    public string visualizationName { get; } = "Deutsch Quantum Circuit (Q)";
    public string visualizationDefinition { get; } = "Requests the QASM for the two-qubit Deutsch circuit, showing the oracle call and measurement that separates constant vs. balanced functions in one query for Q.js rendering.";

    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Jason L. Wright", "Grant Gardner", "Courtney Bodily", "Andreas Kramer", "Rakesh Itani" };
    public VisualizationType visualizationType { get; } = VisualizationType.QuantumCircuitQjs;
    public ISolver solver { get; } = new DeutschClassicalSolver();

    // The Quantum Circuit picture (format=frames only). Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(QuantumProgress);
    QuantumCircuitSpec IQuantumCircuitVisualization<DEUTSCH>.Describe(DEUTSCH problem) => QuantumCircuits.Deutsch(problem.funcValues);

    // --- Methods Including Constructors ---
    public DeutschDefaultVisualization() {

    }
    public API_QUANTUMCIRCUIT visualize(DEUTSCH instance) {
        return new API_QUANTUMCIRCUIT {
            format = QuantumCircuitFormat.QASM,
            qasm = "",
            solution = ""
        };

    }

    public API_JSON SolvedVisualization(DEUTSCH instance, string solution) {
        var qc = new API_QUANTUMCIRCUIT {
            solution = solution,
            format = QuantumCircuitFormat.QASM
        };

        try {
            // Get the function values from the problem instance
            bool[] requestBody = instance.funcValues;

            // Create the API client
            var client = new QuantumServerAPI();

            // Make the API call to get the full response including QASM
            string response = client.PostAsync("/deutsch-quantum", requestBody).Result;

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
