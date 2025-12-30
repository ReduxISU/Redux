using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_ACCEPTDFA;

namespace API.Problems.NPComplete.NPC_ACCEPTDFA.Visualizations;

public class DFAVisualization : IVisualization<DFA>
{
    public string visualizationName { get; } = "TODO";
    public string visualizationDefinition { get; } = "TODO";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    public string visualizationType { get; } = "TODO"; //either "Boolean Satisfiability" or "Graph D3" most likely
    

    // --- Methods Including Constructors ---
    public DFAVisualization()
    {
        
    }
    API_JSON IVisualization<DFA>.visualize(DFA instance)
    {
        return instance.graph.ToAPIGraph();
    }

    API_JSON IVisualization<DFA>.SolvedVisualization(DFA instance, string solution)
    {
        return instance.graph.ToAPIGraph();
    }
}