using API.Interfaces;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.JSON_Objects;
using SPADE;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_ARCSET.Visualizers;

class ArcSetDefaultVisualization : IVisualization<ARCSET> {

    // --- Fields ---
    public string visualizationName {get;} = "Arc Set Visualization";
    public string visualizationDefinition {get;} = "This is a default visualization for Arc Set";
    public string source {get;} = "";
    public string[] contributors {get;} = {"Russell Phillips"};
    public string visualizationType { get; } = "Graph D3";

    // --- Methods Including Constructors ---
    public ArcSetDefaultVisualization()
    {

    }

    private bool LinkInUtil(UtilCollection solutionEdges, API_Link link)
    {
        foreach (UtilCollection Edge in solutionEdges)
        {
            if (Edge[0].ToString() == link.source && Edge[1].ToString() == link.target) return true;
        }
        return false;
    }

    public API_JSON visualize(ARCSET arcset)
    {
        return arcset.graph.ToAPIGraph();
    }
    public API_JSON SolvedVisualization(ARCSET arcset, string solution)
    {
        UtilCollection solutionEdges = new(solution);

        API_GraphJSON apiGraph = arcset.graph.ToAPIGraph();
        for(int i=0;i<apiGraph.links.Count;i++){
            if(LinkInUtil(solutionEdges, apiGraph.links[i])){ 
               apiGraph.links[i].color = "Red"; 
            }
            else{apiGraph.links[i].color = "Background";}
        }
        return apiGraph;
    }
}