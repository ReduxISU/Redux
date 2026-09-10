using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.P.P_NFA;
using API.Interfaces.JSON_Objects.Graphs;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.P.P_NFA.Solvers;

namespace API.Problems.P.P_NFA.Visualizations;

class NFAVisualization : IVisualization<NFA> {
    public string visualizationName { get; } = "Non-Deterministic Finite Automata Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Non-deterministic Finite Automata";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphLaTeX;
    public ISolver solver { get; } = new NFASolver();

    // --- Methods Including Constructors ---
    public NFAVisualization() { }

    API_JSON IVisualization<NFA>.visualize(NFA instance) {
        return instance.graph.ToAPIGraph();
    }

    API_JSON IVisualization<NFA>.SolvedVisualization(NFA instance, string solution) {
        return instance.graph.ToAPIGraph();
    }
}