using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION;

namespace API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Visualizations;

class LosslessDataCompressionVisualization : IVisualization<LOSSLESSDATACOMPRESSION>
{
    public string visualizationName { get; } = "lossless data compression Visualization";
    public string visualizationDefinition { get; } = "TODO";
    public string source { get; } = "";
    public string sourceLink {get;} = "TODO";
    public string[] contributors { get; } = { "TODO" };
    public string visualizationType { get; } = "TODO"; //either "Boolean Satisfiability" or "Graph D3" most likely
    public ISolver solver { get; } = null; //TODO fill in solver to use for this visualization

    // --- Methods Including Constructors ---
    public LosslessDataCompressionVisualization()
    {

    }
    public API_JSON visualize(LOSSLESSDATACOMPRESSION instance)
    {
        //TODO: implement visualization
        
        return new API_empty();
    }

    public API_JSON SolvedVisualization(LOSSLESSDATACOMPRESSION instance, string solution)
    {
        //TODO: implement SolvedVisualization (remove method if not implemented)

        return new API_empty();
    }
}