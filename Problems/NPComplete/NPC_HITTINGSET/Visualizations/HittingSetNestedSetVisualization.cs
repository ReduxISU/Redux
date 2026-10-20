using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_HITTINGSET.Solvers;

namespace API.Problems.NPComplete.NPC_HITTINGSET.Visualizations;

/// <summary>
/// Hitting Set's nested-set picture (#926): every element a dot, every set a region around its own elements. It is served only by
/// <c>format=frames</c> (type <c>NestedSet</c>) and draws the same ids, states and steps as the Bipartite Graph, without the
/// membership edges. The list format, which old frontends read, is exactly <see cref="HittingSetDefaultVisualization"/>'s, so this
/// entry renders like the default Hitting Set picture until a frontend draws the nested sets.
/// </summary>
class HittingSetNestedSetVisualization : IVisualization<HITTINGSET, API_SET>, INestedSetVisualization<HITTINGSET> {
    public string visualizationName { get; } = "Hitting Set Nested Sets";
    public string visualizationDefinition { get; } = "The sets drawn as sets: every element of the universe is a dot, and every set is a region around its own elements, so overlaps and subsets show directly. With format=frames, the elements a solver picks and the sets they hit are marked, step by step.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.SetD3;
    public ISolver solver { get; } = new HittingSetBruteForce();

    private readonly HittingSetDefaultVisualization _listFormat = new();

    public API_SET visualize(HITTINGSET problem) => _listFormat.visualize(problem);

    public API_JSON SolvedVisualization(HITTINGSET problem, string solution) => _listFormat.SolvedVisualization(problem, solution);

    // --- Nested Set picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(NodeSet);

    BipartiteSpec INestedSetVisualization<HITTINGSET>.Describe(HITTINGSET problem) => HittingSetDefaultVisualization.Spec(problem);
}
