using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using SPADE;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_HITTINGSET.Solvers;
namespace API.Problems.NPComplete.NPC_HITTINGSET.Visualizations;

class HittingSetDefaultVisualization : IVisualization<HITTINGSET, API_SET>, IBipartiteVisualization<HITTINGSET> {
    public string visualizationName { get; } = "Hitting Set Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Hitting Set";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public VisualizationType visualizationType { get; } = VisualizationType.SetD3;
    public ISolver solver { get; } = new HittingSetBruteForce();

    // --- Methods Including Constructors ---
    public HittingSetDefaultVisualization() {

    }
    public API_SET visualize(HITTINGSET hittingSet) {
        return new API_SET(new UtilCollection(hittingSet.instance));
    }
    public API_JSON SolvedVisualization(HITTINGSET hittingSet, string solution) {
        //return new API_SET(new UtilCollection(hittingSet.instance));
        API_SET ec = new API_SET(new UtilCollection(hittingSet.instance));

        return new API_SET(new UtilCollection(hittingSet.instance));
    }

    // --- Bipartite Graph picture (format=frames only; the list-format methods above are unchanged) ---
    Type? IVisualization.StepShape => typeof(NodeSet);

    BipartiteSpec IBipartiteVisualization<HITTINGSET>.Describe(HITTINGSET hittingSet) => Spec(hittingSet);

    /// <summary>
    /// Hitting Set (as implemented: Exact Hitting Set, #698) as a Bipartite Graph: pick elements, every set exactly once.
    /// Solvers use it for captions.
    /// </summary>
    internal static BipartiteSpec Spec(HITTINGSET hittingSet) =>
        new(hittingSet.universalSet.ToList().Select(item => item.ToString()).ToList(),
            hittingSet.subSets.ToList().Select(set => (IReadOnlyList<string>)set.ToList().Select(item => item.ToString()).ToList()).ToList(),
            null, BipartitePick.Elements, BipartiteRule.ExactlyOnce);
}