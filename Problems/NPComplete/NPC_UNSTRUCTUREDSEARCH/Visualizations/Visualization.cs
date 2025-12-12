using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH;
using API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Solvers;
using API.Tools;
using System.Text.Json;

namespace API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Visualizers;

class UnstructuredSearchVisualization : IVisualization<UNSTRUCTUREDSEARCH>
{
    public string visualizationName { get; } = "TODO";
    public string visualizationDefinition { get; } = "TODO";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Jason L. Wright", "Alex Svancara" };
    public string visualizationType { get; } = "TODO"; //either "Boolean Satisfiability" or "Graph D3" most likely

    // --- Methods Including Constructors ---
    public UnstructuredSearchVisualization()
    {
        Console.WriteLine("defult vis constructor");
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
