using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Solvers;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Verifiers;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Visualizations;

/// <summary>
/// Weighted Max Clique as the Graph picture (format=frames, #1058): the graph with each node's weight beside it, the clique
/// the solver is building or trying, and its total weight in every frame (<c>selectedWeight</c>). It is opt-in
/// (<see cref="IOptInVisualization"/>), so Redux_GUI keeps seeing only <see cref="WeightedMaxCliqueVisualization"/>; its list
/// format is exactly that visualization's.
/// </summary>
internal sealed class WeightedMaxCliqueGraphVisualization : IVisualization<WEIGHTEDMAXCLIQUE, API_GraphJSON>,
    IGraphSubsetVisualization<WEIGHTEDMAXCLIQUE>, IOptInVisualization {
    public string visualizationName { get; } = "Weighted Maximum Clique Graph";
    public string visualizationDefinition { get; } = "The graph with each node's weight beside it. Each step shows the clique the solver is building or trying and its total weight, and the last shows the heaviest clique found.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;
    public ISolver solver { get; } = new WeightedMaxCliqueBruteForceSolver();

    private readonly WeightedMaxCliqueVisualization _listFormat = new();

    public API_GraphJSON visualize(WEIGHTEDMAXCLIQUE problem) => _listFormat.visualize(problem);

    public API_JSON SolvedVisualization(WEIGHTEDMAXCLIQUE problem, string solution) => _listFormat.SolvedVisualization(problem, solution);

    // --- Graph picture (format=frames only) ---

    // What the problem is: its graph, the clique rule, and a weight on every node. There is no K: the clique is as heavy as it can be.
    GraphSubsetSpec IGraphSubsetVisualization<WEIGHTEDMAXCLIQUE>.Describe(WEIGHTEDMAXCLIQUE problem) =>
        new(Nodes: problem.nodes, Edges: problem.edges, K: null, Rule: SubsetRule.Clique) { NodeWeights = problem.weights };

    // A Weighted Max Clique answer is "({a,b,c},12)": the clique and its weight. The picture draws the clique.
    NodeSet IGraphSubsetVisualization<WEIGHTEDMAXCLIQUE>.ParseAnswer(WEIGHTEDMAXCLIQUE problem, string answer) {
        try {
            return new NodeSet(WeightedMaxCliqueSolutionVerifier.ParseCertificate(problem, answer).Vertices);
        } catch (CertificateParseException) {
            return new NodeSet([]);
        }
    }
}
