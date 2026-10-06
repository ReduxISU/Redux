using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_SUBSETPRODUCT;

namespace API.Problems.NPComplete.NPC_SUBSETPRODUCT.Visualizations;

class SubsetProductVisualization : IVisualization<SUBSETPRODUCT> {
    public string visualizationName { get; } = "Subset Product Visualization";
    public string visualizationDefinition { get; } = "TODO";
    public string source { get; } = "";
    public string sourceLink { get; } = "TODO";
    public string[] contributors { get; } = { "TODO" };
    // TODO: replace Unimplemented with the VisualizationType matching the API_JSON shape you
    // return below. A test in redux-tests/Metadata (VisualizationType_Tests, NoNewUndeclared)
    // fails until you do, on purpose. If no existing renderer fits, make the problem's default
    // visualization DummyVisualization instead of this class. Adding a NEW VisualizationType
    // member means updating Documentation/visualization-types.json (checked by
    // VisualizationType_Tests.ManifestMatchesEnum) and adding a renderer in the Redux_GUI repo
    // (checked there by `npm run check:visualizations`).
    public VisualizationType visualizationType { get; } = VisualizationType.Unimplemented;
    // The solver whose steps this visualization shows. Defaults to the problem's default solver.
    // Replace it with a specific solver if you prefer, e.g. `new MyBruteForce()`. This must stay
    // expression-bodied (=>): the problem's constructor builds its default visualization, so an
    // initializer (=) here would recurse forever.
    public ISolver solver => new SUBSETPRODUCT().defaultSolver;

    // --- Methods Including Constructors ---
    public SubsetProductVisualization() {

    }
    public API_JSON visualize(SUBSETPRODUCT instance) {
        //TODO: implement visualization

        return new API_empty();
    }

    public API_JSON SolvedVisualization(SUBSETPRODUCT instance, string solution) {
        //TODO: implement SolvedVisualization (remove method if not implemented)

        return new API_empty();
    }
}