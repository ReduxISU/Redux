using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_DFA;
using API.Interfaces.JSON_Objects.Graphs;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_DFA.Solvers;

namespace API.Problems.NPComplete.NPC_DFA.Visualizations;

class DFAVisualization : IVisualization<DFA>
{
    public string visualizationName { get; } = "Determinite Finite Automata Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Deterministic Finite Automata";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    public string visualizationType { get; } = "Graph LaTeX";
    public ISolver solver { get; } = new DFASolver();

    // --- Methods Including Constructors ---
    public DFAVisualization() {}
    API_JSON IVisualization<DFA>.visualize(DFA instance)
    {
        return instance.graph.ToAPIGraph();
    }

    API_JSON IVisualization<DFA>.SolvedVisualization(DFA instance, string solution)
    {
        List<string> solutionList = GraphParser.parseNodeListWithStringFunctions(solution);
        API_GraphJSON apiGraph = instance.graph.ToAPIGraph();

        for (int i = 0; i < apiGraph.nodes.Count; i++)
        {
            if (solutionList.Contains(apiGraph.nodes[i].name))
            {
                apiGraph.nodes[i].color = "green";
            }
            else { apiGraph.nodes[i].color = "white"; }
        }

        for (int i = 0; i < apiGraph.links.Count; i++)
        {
            var edge = apiGraph.links[i];

            if (solutionList.Contains(edge.source) &&
                solutionList.Contains(edge.target))
            {
                apiGraph.links[i].color = "green";
            }
            else
            {
                apiGraph.links[i].color = "black";
            }
        }

        return apiGraph;
        
        /*
        string[] acceptPath = solution.Split(',');

        API_GraphJSON apiGraph = instance.graph.ToAPIGraph();

        // If Accepted on Start State (Empty String) //
        if (acceptPath.Length == 1) { apiGraph.nodes[0].color = "green"; }
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
                    link.color = "green";
                }

                if (node != null)
                {
                    node.color = "green";
                }
            }
            return apiGraph;
        }
        // If Was Not Accepted //
        else { return null; }
        */
    }
}