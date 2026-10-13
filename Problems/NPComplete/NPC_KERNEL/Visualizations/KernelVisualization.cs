using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_KERNEL;

namespace API.Problems.NPComplete.NPC_KERNEL.Visualizations;

class KernelVisualization : IVisualization<KERNEL>
{
    public string visualizationName { get; } = "Kernel Visualization";
    public string visualizationDefinition { get; } = "TODO";
    public string source { get; } = "";
    public string sourceLink {get;} = "TODO";
    public string[] contributors { get; } = { "TODO" };
    // Pick the member matching the API_JSON shape you return below. If no renderer fits,
    // leave this as Unimplemented — the GUI shows an honest "no renderer" state instead of
    // a broken view. Do NOT add a new member without adding a renderer in
    // Redux_GUI/components/Visualization/svgs/Visualizations.js (CI enforces this).
    public VisualizationType visualizationType { get; } = VisualizationType.Unimplemented;
    public ISolver solver { get; } = null; //TODO fill in solver to use for this visualization

    // --- Methods Including Constructors ---
    public KernelVisualization()
    {

    }
    public API_JSON visualize(KERNEL instance)
    {
        //TODO: implement visualization
        
        return new API_empty();
    }

    public API_JSON SolvedVisualization(KERNEL instance, string solution)
    {
        //TODO: implement SolvedVisualization (remove method if not implemented)

        return new API_empty();
    }
}