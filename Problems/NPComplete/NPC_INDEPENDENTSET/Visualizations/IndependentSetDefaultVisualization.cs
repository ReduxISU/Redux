using API.Interfaces;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.JSON_Objects;

namespace API.Problems.NPComplete.NPC_INDEPENDENTSET.Visualizations;

class IndependentSetDefaultVisualization : IVisualization<INDEPENDENTSET> {

    // --- Fields ---
    public string visualizationName {get;} = "Independent Set Visualization";
    public string visualizationDefinition {get;} = "This is a default visualization for Independent Set";
    public string source {get;} = "";
    public string[] contributors {get;} = {"Russell Phillips"};
    public string visualizationType { get; } = "Graph D3";

    // --- Methods Including Constructors ---
    public IndependentSetDefaultVisualization() {
        
    }
    public API_JSON visualize(INDEPENDENTSET independentSet)
    {
        return independentSet.graph.ToAPIGraph();
    }
    
    public API_JSON SolvedVisualization(INDEPENDENTSET independentSet, string solution)
    {
        List<string> solutionNodes = GraphParser.parseNodeListWithStringFunctions(solution);

        API_GraphJSON apiGraph = independentSet.graph.ToAPIGraph();
        for(int i=0;i<apiGraph.nodes.Count;i++){
            if(solutionNodes.Contains(apiGraph.nodes[i].name)){ 
               apiGraph.nodes[i].color = "Solution"; 
            }
            else{apiGraph.nodes[i].color = "Background";}
        }
        return apiGraph;
    }
}