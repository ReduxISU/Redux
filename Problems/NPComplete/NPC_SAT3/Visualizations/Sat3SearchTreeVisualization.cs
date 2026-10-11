using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.SearchTree;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_SAT3.Solvers;

/// <summary>
/// 3SAT's search tree, a second picture of the backtracking solver: each value it tries is a node under the state it
/// decides, queued, cut by a false clause, or the solution. It is served only by <c>format=frames</c> (type
/// <c>SearchTree</c>). The list format, which old frontends read, is exactly <see cref="Sat3DefaultVisualization"/>'s, so
/// this entry renders like the default 3SAT picture until a frontend draws the tree.
/// </summary>
class Sat3SearchTreeVisualization : IVisualization<SAT3>, ISearchTreeVisualization<SAT3, Assignment> {
    public string visualizationName { get; } = "3SAT Search Tree";
    public string visualizationDefinition { get; } = "The choices of 3SAT Backtracking as a tree: each level sets one variable, a red dashed node is a value that makes a clause false, and the numbers show the order in which queued branches were taken. With format=frames, the tree grows step by step and the path to the answer turns green.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.BooleanSatisfiability;
    public ISolver solver { get; } = new Sat3BacktrackingSolver();

    private readonly Sat3DefaultVisualization _listFormat = new();

    public API_JSON visualize(SAT3 instance) => _listFormat.visualize(instance);

    public API_JSON SolvedVisualization(SAT3 instance, string solution) => _listFormat.SolvedVisualization(instance, solution);

    // --- Search Tree picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(Assignment);

    SearchTreeSpec ISearchTreeVisualization<SAT3, Assignment>.Describe(SAT3 problem) => new("3SAT");
}
