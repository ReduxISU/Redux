using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.SearchTree;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_TSP.Solvers;

namespace API.Problems.NPComplete.NPC_TSP.Visualizations;

/// <summary>
/// Traveling Salesperson's search tree, a second picture of the branch and bound: partial tours as a tree, each node with
/// its lower bound, expanded cheapest-score first, cut when the bound can't beat the best tour so far. It is served only by
/// <c>format=frames</c> (type <c>SearchTree</c>). Its own solver is the branch and bound (the brute force does not branch).
/// The list format, which old frontends read, is exactly <see cref="TSPDefaultVisualization"/>'s for the same solver.
/// </summary>
class TSPSearchTreeVisualization : IVisualization<TSP>, ISearchTreeVisualization<TSP, Tour> {
    public string visualizationName { get; } = "Traveling Salesperson Search Tree";
    public string visualizationDefinition { get; } = "The choices of Traveling Salesperson Branch and Bound as a tree: each node extends a partial tour by one city and shows its lower bound, a red dashed node is an extension cut because its bound can't beat the best tour so far, and the numbers show the order in which queued paths were taken. With format=frames, the tree grows step by step and the best tour's path turns green.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;
    public ISolver solver { get; } = new TSPBranchAndBound();

    private readonly TSPDefaultVisualization _listFormat = new();

    public API_JSON visualize(TSP instance) => _listFormat.visualize(instance);

    public API_JSON SolvedVisualization(TSP instance, string solution) => _listFormat.SolvedVisualization(instance, solution);

    // --- Search Tree picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(Tour);

    SearchTreeSpec ISearchTreeVisualization<TSP, Tour>.Describe(TSP problem) => new("Traveling Salesperson");
}
