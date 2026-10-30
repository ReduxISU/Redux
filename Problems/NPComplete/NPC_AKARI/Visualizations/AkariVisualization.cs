using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_AKARI;
using API.Problems.NPComplete.NPC_AKARI.Solvers;

namespace API.Problems.NPComplete.NPC_AKARI.Visualizations;

class AkariVisualization : IVisualization<AKARI> {
    public string visualizationName { get; } = "Akari Visualization";
    public string visualizationDefinition { get; } = "TODO";
    public string source { get; } = "";
    public string sourceLink { get; } = "TODO";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "TODO" };
    // Pick the member matching the API_JSON shape you return below. If no renderer fits,
    // leave this as Unimplemented — the GUI shows an honest "no renderer" state instead of
    // a broken view. Do NOT add a new member without adding a renderer in
    // Redux_GUI/components/Visualization/svgs/Visualizations.js (CI enforces this).
    public VisualizationType visualizationType { get; } = VisualizationType.Unimplemented;
    public ISolver solver { get; } = new AkariSolver();

    // --- Methods Including Constructors ---
    public AkariVisualization() {

    }
    public API_JSON visualize(AKARI instance) {
        //TODO: implement visualization

        return new API_empty();
    }

    public API_JSON SolvedVisualization(AKARI instance, string solution) {
        //TODO: implement SolvedVisualization (remove method if not implemented)

        return new API_empty();
    }
}