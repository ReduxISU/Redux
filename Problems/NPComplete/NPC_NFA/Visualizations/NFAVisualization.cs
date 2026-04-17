using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_NFA;
using API.Interfaces.JSON_Objects.Graphs;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_NFA.Visualizations;

public class NFAVisualization : IVisualization<NFA>
{
    public string visualizationName { get; } = "Non-deterministic Finite Automata Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Non-deterministic Finite Automata";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    public string visualizationType { get; } = "Graph LaTeX";

    // --- Methods Including Constructors ---
    public NFAVisualization() { }

    API_JSON IVisualization<NFA>.visualize(NFA instance)
    {
        return instance.graph.ToAPIGraph();
    }

    API_JSON IVisualization<NFA>.SolvedVisualization(NFA instance, string solution)
    {
        return instance.graph.ToAPIGraph();
    }
}