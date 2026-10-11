using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_DEUTSCHJOZSA;
using API.Problems.NPComplete.NPC_DEUTSCHJOZSA.Solvers;
using API.Tools;
using System.Text.Json;

/// <summary>
/// The Quantum Circuit picture (format=frames) comes from the instance's circuit and the chosen solver's steps; the list
/// format below is unchanged.
/// </summary>
class DeutschJozsaDefaultVisualization : IVisualization<DEUTSCHJOZSA, API_QUANTUMCIRCUIT>, IQuantumCircuitVisualization<DEUTSCHJOZSA> {
    public string visualizationName { get; } = "Deutsch-Jozsa Quantum Circuit (Q)";
    public string visualizationDefinition { get; } = "Requests the QASM for the Deutsch-Jozsa circuit (data qubits plus ancilla), showing the oracle and measurements that separate constant vs. balanced functions in one query for Q.js.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Courtney Bodily", "Andreas Kramer", "Rakesh Itani", "Grant Gardner" };
    public VisualizationType visualizationType { get; } = VisualizationType.QuantumCircuitQjs;
    public ISolver solver { get; } = new DeutschJozsaClassicalSolver();

    // The Quantum Circuit picture (format=frames only). Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(QuantumProgress);
    QuantumCircuitSpec IQuantumCircuitVisualization<DEUTSCHJOZSA>.Describe(DEUTSCHJOZSA problem) => QuantumCircuits.DeutschJozsa(problem.w);

    // --- Methods Including Constructors ---
    public DeutschJozsaDefaultVisualization() {

    }
    public API_QUANTUMCIRCUIT visualize(DEUTSCHJOZSA instance) {
        return new API_QUANTUMCIRCUIT {
            format = QuantumCircuitFormat.QASM,
            qasm = "",
            solution = ""
        };

    }

    public API_JSON SolvedVisualization(DEUTSCHJOZSA instance, string solution) {
        var qc = new API_QUANTUMCIRCUIT {
            solution = solution,
            format = QuantumCircuitFormat.QASM
        };

        try {
            // Convert the list of integers to boolean array for the API
            bool[] requestBody = instance.w.Select(val => val != 0).ToArray();

            // Create the API client
            var client = new QuantumServerAPI();

            // Make the API call to get the full response including QASM
            string response = client.PostAsync("/deutsch-jozsa-quantum", requestBody).Result;

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
