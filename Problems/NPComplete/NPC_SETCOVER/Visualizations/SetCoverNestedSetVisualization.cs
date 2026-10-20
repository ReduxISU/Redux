using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SETCOVER.Solvers;

namespace API.Problems.NPComplete.NPC_SETCOVER.Visualizations;

/// <summary>
/// Set Cover's nested-set picture (#926): every element a dot, every set a region around its own elements. It is served only by
/// <c>format=frames</c> (type <c>NestedSet</c>) and draws the same ids, states and steps as the Bipartite Graph, without the
/// membership edges. The list format, which old frontends read, is exactly <see cref="SetCoverDefaultVisualization"/>'s, so this
/// entry renders like the default Set Cover picture until a frontend draws the nested sets.
/// </summary>
class SetCoverNestedSetVisualization : IVisualization<SETCOVER, API_SET>, INestedSetVisualization<SETCOVER> {
    public string visualizationName { get; } = "Set Cover Nested Sets";
    public string visualizationDefinition { get; } = "The sets drawn as sets: every element of the universe is a dot, and every set is a region around its own elements, so overlaps and subsets show directly. With format=frames, the sets a solver picks and the elements they cover are marked, step by step.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.SetD3;
    public ISolver solver { get; } = new SetCoverBruteForce();

    private readonly SetCoverDefaultVisualization _listFormat = new();

    public API_SET visualize(SETCOVER problem) => _listFormat.visualize(problem);

    public API_JSON SolvedVisualization(SETCOVER problem, string solution) => _listFormat.SolvedVisualization(problem, solution);

    // --- Nested Set picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(ChosenSets);

    BipartiteSpec INestedSetVisualization<SETCOVER>.Describe(SETCOVER problem) => SetCoverDefaultVisualization.Spec(problem);
}
