using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.SearchTree;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_HAMILTONIAN.Solvers;

namespace API.Problems.NPComplete.NPC_HAMILTONIAN.Visualizations;

/// <summary>
/// Hamiltonian Cycle's search tree, a second picture of the brute force backtracking search: each level walks the path to
/// one more node along an edge, a node with no edge to it from the node before is cut (and so is a whole tour whose
/// closing edge, or other hop, is missing), and a node every way on from which fails is given up. It is served only by
/// <c>format=frames</c> (type <c>SearchTree</c>). The list format, which old frontends read, is exactly
/// <see cref="HamiltonianDefaultVisualization"/>'s. The problem's default visualization is unchanged.
/// </summary>
class HamiltonianSearchTreeVisualization : IVisualization<HAMILTONIAN>, ISearchTreeVisualization<HAMILTONIAN, Tour>, IOptInVisualization {
    public string visualizationName { get; } = "Hamiltonian Search Tree";
    public string visualizationDefinition { get; } = "The choices of Hamiltonian Brute Force as a tree: the first level is the node the tour starts at, each level below walks to one more node (\"3\") along an edge, a red dashed node is a node with no edge to it from the node before (or a whole tour with a hop that is not an edge), and a solid red node is a node given up because every way on from it fails. With format=frames, the tree grows step by step and the path to the answer turns green.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;
    public ISolver solver { get; } = new HamiltonianBruteForce();

    private readonly HamiltonianDefaultVisualization _listFormat = new();

    public API_JSON visualize(HAMILTONIAN instance) => _listFormat.visualize(instance);

    public API_JSON SolvedVisualization(HAMILTONIAN instance, string solution) => _listFormat.SolvedVisualization(instance, solution);

    // --- Search Tree picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(Tour);

    SearchTreeSpec ISearchTreeVisualization<HAMILTONIAN, Tour>.Describe(HAMILTONIAN problem) => new("Hamiltonian");
}
