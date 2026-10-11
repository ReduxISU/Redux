using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using SPADE;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SETCOVER.Solvers;
namespace API.Problems.NPComplete.NPC_SETCOVER.Visualizations;

class SetCoverDefaultVisualization : IVisualization<SETCOVER, API_SET>, IBipartiteVisualization<SETCOVER> {
    public string visualizationName { get; } = "Set Cover Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Set Cover";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public VisualizationType visualizationType { get; } = VisualizationType.SetD3;
    public ISolver solver { get; } = new SetCoverBruteForce();

    // --- Methods Including Constructors ---
    public SetCoverDefaultVisualization() {

    }
    public API_SET visualize(SETCOVER setcover) {
        return new API_SET(new UtilCollection(setcover.instance));
    }
    public API_JSON SolvedVisualization(SETCOVER setcover, string solution) {
        //return new API_SET(new UtilCollection(setcover.instance));
        API_SET sc = new API_SET(new UtilCollection(setcover.instance));

        return new API_SET(new UtilCollection(setcover.instance));
    }

    // --- Bipartite Graph picture (format=frames only; the list-format methods above are unchanged) ---
    Type? IVisualization.StepShape => typeof(ChosenSets);

    BipartiteSpec IBipartiteVisualization<SETCOVER>.Describe(SETCOVER setCover) => Spec(setCover);

    /// <summary>Set Cover as a Bipartite Graph: pick sets, every element at least once, at most K sets. Solvers use it for captions.</summary>
    internal static BipartiteSpec Spec(SETCOVER setCover) =>
        new(setCover.universal, setCover.subsets, setCover.K, BipartitePick.Sets, BipartiteRule.AtLeastOnce);
}