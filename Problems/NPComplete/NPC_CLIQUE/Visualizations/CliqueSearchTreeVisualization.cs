using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.SearchTree;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_CLIQUE.Solvers;

namespace API.Problems.NPComplete.NPC_CLIQUE.Visualizations;

/// <summary>
/// Clique's search tree, a second picture of Carraghan-Pardalos branch and bound: each level adds one vertex joined to
/// every vertex added before it, a vertex the bound rules out (too few vertices could ever join it) is cut, and a vertex
/// no clique of size K grows from is backed out. Chiba-Nishizeki draws it too (with <c>solver=ChibaNishizeki</c>), its
/// first level the vertex it starts from. It is served only by <c>format=frames</c> (type <c>SearchTree</c>). The list
/// format, which old frontends read, is exactly <see cref="CliqueDefaultVisualization"/>'s. The problem's default
/// visualization is unchanged.
/// </summary>
class CliqueSearchTreeVisualization : IVisualization<CLIQUE>, ISearchTreeVisualization<CLIQUE, NodeSet>, IOptInVisualization {
    public string visualizationName { get; } = "Clique Search Tree";
    public string visualizationDefinition { get; } = "The choices of Clique Carraghan-Pardalos Branch and Bound as a tree: each level adds a vertex (\"3\") joined to every vertex added before it, a red dashed node is a vertex the bound rules out because too few vertices could ever join it, and a solid red node is a vertex backed out because no clique of size K grows from it. With format=frames, the tree grows step by step and the path to the answer turns green.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;
    public ISolver solver { get; } = new CarraghanPardalos();

    private readonly CliqueDefaultVisualization _listFormat = new();

    public API_JSON visualize(CLIQUE instance) => _listFormat.visualize(instance);

    public API_JSON SolvedVisualization(CLIQUE instance, string solution) => _listFormat.SolvedVisualization(instance, solution);

    // --- Search Tree picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(NodeSet);

    SearchTreeSpec ISearchTreeVisualization<CLIQUE, NodeSet>.Describe(CLIQUE problem) => new("Clique");
}
