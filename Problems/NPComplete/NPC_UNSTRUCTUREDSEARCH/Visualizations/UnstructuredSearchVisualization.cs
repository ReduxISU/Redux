using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH;
using API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Solvers;
using API.Tools;
using System.Text.Json;

namespace API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Visualizations;

/// <summary>
/// The Quantum Circuit picture (format=frames) comes from the instance's circuit and the chosen solver's steps; the list
/// format below is unchanged.
/// </summary>
class UnstructuredSearchVisualization : IVisualization<UNSTRUCTUREDSEARCH, API_QUANTUMCIRCUIT>, IQuantumCircuitVisualization<UNSTRUCTUREDSEARCH> {
    public string visualizationName { get; } = "Unstructured Search Quantum Circuit";
    public string visualizationDefinition { get; } = "This visualization builds a quantum circuit";
    public string source { get; } = "Brassard, G., Hoyer, P., Mosca, M., & Tapp, A. (2000), Quantum Amplitude Amplification and Estimation";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Jason L. Wright", "Alex Svancara", "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.QuantumCircuitQjs;
    public ISolver solver { get; } = new UnstructuredSearchSolver();

    // The Quantum Circuit picture (format=frames only). Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(QuantumProgress);
    QuantumCircuitSpec IQuantumCircuitVisualization<UNSTRUCTUREDSEARCH>.Describe(UNSTRUCTUREDSEARCH problem) => QuantumCircuits.UnstructuredSearch(problem.funcValues);

    // --- Methods Including Constructors ---
    public UnstructuredSearchVisualization() {
    }
    public API_QUANTUMCIRCUIT visualize(UNSTRUCTUREDSEARCH instance) {
        return new API_QUANTUMCIRCUIT {
            format = QuantumCircuitFormat.QASM,
            qasm = "",
            solution = ""
        };
    }
}
