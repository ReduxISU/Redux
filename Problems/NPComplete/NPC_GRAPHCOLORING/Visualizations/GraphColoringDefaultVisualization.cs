using API.Interfaces;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.JSON_Objects;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.Visualizations;

class GraphColoringDefaultVisualization : IVisualization<GRAPHCOLORING> {

    // --- Fields ---
    public string visualizationName {get;} = "Graph Coloring Visualization";
    public string visualizationDefinition {get;} = "This is a default visualization for Graph Coloring";
    public string source {get;} = "";
    public string[] contributors {get;} = {"Andrija Sevaljevic", "Russell Phillips"};
    public string visualizationType { get; } = "Graph D3";

    // --- Methods Including Constructors ---
    public GraphColoringDefaultVisualization() {
        
    }
    public API_JSON visualize(GRAPHCOLORING GRAPHCOLORING)
    {
        return GRAPHCOLORING.graph.ToAPIGraph();
    }
    
    public API_JSON SolvedVisualization(GRAPHCOLORING GRAPHCOLORING, string solution)
    {
        string[] colors = {"Rose", "Solution", "Sand", "Green", "Cyan", "Wine", "Teal", "Olive"};
        List<string> solutionList = solution.Replace("{{","").Replace("}}","").Split("},{").ToList();
        API_GraphJSON apiGraph = GRAPHCOLORING.graph.ToAPIGraph();
        for(int i=0;i<apiGraph.nodes.Count;i++){
            int number = 0;
            foreach(var j in solutionList) {

            if(j.Split(',').Contains(apiGraph.nodes[i].name)){ //we set the nodes as either having a true or false flag which will indicate to the frontend whether to highlight.
                apiGraph.nodes[i].color = colors[number]; 
            }

            number += 1;
            number = number % 8;    
        
            }
        }

        for (int i = 0; i < apiGraph.links.Count; i++)
        {
            int number = 0;
            foreach (var j in solutionList)
            {

                foreach (var source in j.Split(','))
                {
                    foreach (var target in j.Split(','))
                    {
                        if (apiGraph.links[i].source == source && apiGraph.links[i].target == target)
                        {
                            apiGraph.links[i].color = colors[number];
                        }
                    }
                }

                number += 1;
                number = number % 8;
            }
        }

        return apiGraph;
    }
}