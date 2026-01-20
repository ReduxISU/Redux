using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_DFA;
using API.Interfaces.JSON_Objects.Graphs;
using System.Text.Json;
//using API.Interfaces.graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_DFA.Visualizations;

public class DFAVisualization : IVisualization<DFA>
{
    public string visualizationName { get; } = "Determinite Finite Automata Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Deterministic Finite Automata";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    public string visualizationType { get; } = "LaTeX"; // Updated From Graph D3

    // --- Methods Including Constructors ---
    public DFAVisualization() {}
    API_JSON IVisualization<DFA>.visualize(DFA instance)
    {
        return instance.graph.ToAPIGraph();
    }

    API_JSON IVisualization<DFA>.SolvedVisualization(DFA instance, string solution)
    {
        string[] acceptPath = solution.Split(',');

        API_GraphJSON apiGraph = instance.graph.ToAPIGraph();

        // If Accepted on Start State (Empty String) //
        if (acceptPath.Length == 1) { apiGraph.nodes[0].color = "Solution"; }
        // If Accepted Outside Start State //
        if (acceptPath.Length > 1)
        {
            for (int i = 0; i < acceptPath.Length - 1; i++)
            {
                var from = acceptPath[i];
                var to = acceptPath[i + 1];

                var link = apiGraph.links.FirstOrDefault(l =>
                    l.source == from && l.target == to
                );
                var node = apiGraph.nodes.FirstOrDefault(n => n.name == acceptPath[i]);

                if (link != null)
                {
                    //link.color = "Solution";
                }

                if (node != null)
                {
                    node.color = "solution"; // "solution" = Accept State, "initial" = Start State
                }
            }
            return apiGraph;
        }
        // If Was Not Accepted //
        else { return null; }
    }
}