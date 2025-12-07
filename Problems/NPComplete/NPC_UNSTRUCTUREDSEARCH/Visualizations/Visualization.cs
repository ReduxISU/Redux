using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH;

namespace API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Visualizers;

class UnstructuredSearchVisualization : IVisualization<UNSTRUCTUREDSEARCH>
{
    public string visualizationName { get; } = "TODO";
    public string visualizationDefinition { get; } = "TODO";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Alex Svancara" };
    public string visualizationType { get; } = "TODO"; //either "Boolean Satisfiability" or "Graph D3" most likely

    // --- Methods Including Constructors ---
    public UnstructuredSearchVisualization()
    {

    }
    public API_JSON visualize(UNSTRUCTUREDSEARCH instance)
    {
        //TODO: implement visualization
        
        //if graph problem below should be fine
        // return UNSTRUCTUREDSEARCH.graph.ToAPIGraph();
        return {};
    }

    public API_JSON SolvedVisualization(UNSTRUCTUREDSEARCH instance, string solution)
    {
        //TODO: implement SolvedVisualization (remove method if not implemented)
    }
}