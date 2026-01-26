using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH;
using API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Solvers;
using API.Tools;
using System.Text.Json;

namespace API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Visualizations;

class UnstructuredSearchVisualization : IVisualization<UNSTRUCTUREDSEARCH>
{
    public string visualizationName { get; } = "Unstructured Search Quantum Circuit";
    public string visualizationDefinition { get; } = "This visualization builds a quantum circuit";
    public string source { get; } = "Brassard, G., Hoyer, P., Mosca, M., & Tapp, A. (2000), Quantum Amplitude Amplification and Estimation";
    public string[] contributors { get; } = { "Jason L. Wright", "Alex Svancara" };
    public string visualizationType { get; } = "Quantum Circuit";

    // --- Methods Including Constructors ---
    public UnstructuredSearchVisualization()
    {
    }
    public API_JSON visualize(UNSTRUCTUREDSEARCH instance)
    {
        var qc = new API_QUANTUMCIRCUIT();
        var solvers = new UnstructuredGroverSolver();
        qc.solution = solvers.solve(instance);
        qc.circuit = instance.circuit;
        return qc;
    }
}
