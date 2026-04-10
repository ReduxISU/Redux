using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.P.P_DFA;
using API.Interfaces.JSON_Objects.Graphs;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.P.P_DFA.Visualizations;

public class DFAVisualization : IVisualization<DFA>
{
    public string visualizationName { get; } = "Determinite Finite Automata Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Deterministic Finite Automata";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    public string visualizationType { get; } = "Graph LaTeX";

    // --- Methods Including Constructors ---
    public DFAVisualization() {}
    API_JSON IVisualization<DFA>.visualize(DFA instance)
    {
        return instance.graph.ToAPIGraph();
    }

    API_JSON IVisualization<DFA>.SolvedVisualization(DFA instance, string solution)
    {
        return instance.graph.ToAPIGraph();
    }
}