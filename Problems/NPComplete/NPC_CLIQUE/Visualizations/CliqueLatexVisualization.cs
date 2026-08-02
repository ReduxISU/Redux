using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_CLIQUE.Inherited;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_CLIQUE.Solvers;

namespace API.Problems.NPComplete.NPC_CLIQUE.Visualizations;

class CliqueLatexVisualization : IVisualization<CLIQUE>
{
    public string visualizationName { get; } = "Clique LaTeX Visualization";
    public string visualizationDefinition { get; } = "This is a visualization for Clique using the LaTeX visualization type";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphLaTeX;
    public ISolver solver { get; } = new CliqueBruteForce();

    // --- Methods Including Constructors ---
    public CliqueLatexVisualization()
    {

    }
    public API_JSON visualize(CLIQUE clique)
    {
        return clique.graph.ToAPIGraph();
    }
    public API_JSON SolvedVisualization(CLIQUE clique, string solution)
    {
        List<string> solutionList = GraphParser.parseNodeListWithStringFunctions(solution); //Note, this is just a convenience string to list function.
        API_GraphJSON apiGraph = clique.graph.ToAPIGraph();

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
    }
}