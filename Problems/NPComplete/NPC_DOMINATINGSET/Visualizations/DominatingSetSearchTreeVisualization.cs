using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.SearchTree;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_DOMINATINGSET.Solvers;

namespace API.Problems.NPComplete.NPC_DOMINATINGSET.Visualizations;

/// <summary>
/// Dominating Set's search tree, a second picture of Forced-Vertex Branch-and-Reduce: a vertex with no neighbors is a
/// forced pick, the one child of the pick before it; otherwise each level picks one of the vertices that can dominate the
/// undominated vertex of highest degree, a pick past K or one that leaves no picks with vertices still undominated is
/// cut, and a pick no dominating set within K follows from is backed out. Naive Inclusion-Exclusion draws it too (with
/// <c>solver=DominatingSetNaiveInclusionExclusion</c>), each level deciding one vertex in or out. It is served only by
/// <c>format=frames</c> (type <c>SearchTree</c>). The list format, which old frontends read, is exactly
/// <see cref="DominatingSetDefaultVisualization"/>'s. The problem's default visualization is unchanged.
/// </summary>
class DominatingSetSearchTreeVisualization : IVisualization<DOMINATINGSET>, ISearchTreeVisualization<DOMINATINGSET, NodeSet>, IOptInVisualization {
    public string visualizationName { get; } = "Dominating Set Search Tree";
    public string visualizationDefinition { get; } = "The choices of Forced-Vertex Branch-and-Reduce as a tree: a vertex with no neighbors is a forced pick (\"3 (forced)\") with one child, each level after them picks a vertex (\"3\") that can dominate the undominated vertex of highest degree, a red dashed node is a pick past K or one that leaves no picks with vertices still undominated, and a solid red node is a pick backed out because no dominating set within K follows from it. With format=frames, the tree grows step by step and the path to the answer turns green.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;
    public ISolver solver { get; } = new DominatingSetForcedVertex();

    private readonly DominatingSetDefaultVisualization _listFormat = new();

    public API_JSON visualize(DOMINATINGSET instance) => _listFormat.visualize(instance);

    public API_JSON SolvedVisualization(DOMINATINGSET instance, string solution) => _listFormat.SolvedVisualization(instance, solution);

    // --- Search Tree picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(NodeSet);

    SearchTreeSpec ISearchTreeVisualization<DOMINATINGSET, NodeSet>.Describe(DOMINATINGSET problem) => new("Dominating Set");
}
