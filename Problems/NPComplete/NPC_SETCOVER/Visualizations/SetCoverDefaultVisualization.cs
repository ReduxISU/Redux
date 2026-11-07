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
    public string[] contributors { get; } = { "Andrija Sevaljevic", "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.SetD3;
    public ISolver solver { get; } = new SetCoverBruteForce();

    // --- Methods Including Constructors ---
    public SetCoverDefaultVisualization() {

    }
    // The pieces get the set problems' piece ids (SetPieces: e:{x}, S{j}), the ids the gadgets of reductions into Set Cover name.
    public API_SET visualize(SETCOVER setcover) {
        return new API_SET(new UtilCollection(setcover.instance)).UseBipartiteIds(Spec(setcover).Sets);
    }
    public API_JSON SolvedVisualization(SETCOVER setcover, string solution) {
        return visualize(setcover);
    }

    // --- Bipartite Graph picture (format=frames only; the list-format methods above are unchanged) ---
    Type? IVisualization.StepShape => typeof(ChosenSets);

    BipartiteSpec IBipartiteVisualization<SETCOVER>.Describe(SETCOVER setCover) => Spec(setCover);

    /// <summary>Set Cover as a Bipartite Graph: pick sets, every element at least once, at most K sets. Solvers use it for captions.</summary>
    internal static BipartiteSpec Spec(SETCOVER setCover) =>
        new(setCover.universal, setCover.subsets, setCover.K, BipartitePick.Sets, BipartiteRule.AtLeastOnce);
}