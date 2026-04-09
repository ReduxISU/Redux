using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_DOMINATINGSET.Solvers;

namespace API.Problems.NPComplete.NPC_DOMINATINGSET.Visualizers;

class DominatingSetDefaultVisualization : IVisualization<DOMINATINGSET>
{
    public string visualizationName { get; } = "Dominating Set Visualization";
    public string visualizationDefinition { get; } =
        "This is a default visualization for dominating set";
    public string source { get; } =
        "Wendy Myrvold, CSC 425 Notes: Domination Algorithms, University of Victoria.";
    public string sourceLink { get; } =
        "https://webhome.cs.uvic.ca/~wendym/courses/425/14/notes/425_03_dom_alg.pdf";
    public string[] contributors { get; } = { "Quinton Smith" };
    public string visualizationType { get; } = "Graph D3";
    public ISolver solver { get; } = new DominatingSetSolver();

    // --- Methods Including Constructors ---
    public DominatingSetDefaultVisualization() { }

    public API_JSON visualize(DOMINATINGSET dominatingSet)
    {
        return dominatingSet.graph.ToAPIGraph();
    }

    public API_JSON SolvedVisualization(DOMINATINGSET dominatingSet, string solution)
    {
        List<string> solutionList = GraphParser.parseNodeListWithStringFunctions(solution);

        API_GraphJSON apiGraph = dominatingSet.graph.ToAPIGraph();

        for (int i = 0; i < apiGraph.nodes.Count; i++)
        {
            if (solutionList.Contains(apiGraph.nodes[i].name))
            {
                apiGraph.nodes[i].color = "Solution";
            }
            else
            {
                apiGraph.nodes[i].color = "Background";
            }
        }
        return apiGraph;
    }
}
