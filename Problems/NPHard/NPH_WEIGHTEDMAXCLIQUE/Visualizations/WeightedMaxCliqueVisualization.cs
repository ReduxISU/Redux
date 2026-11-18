using System.Globalization;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Solvers;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Verifiers;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Visualizations;

[DefaultFor(typeof(WEIGHTEDMAXCLIQUE))]
internal sealed class WeightedMaxCliqueVisualization : IVisualization<WEIGHTEDMAXCLIQUE, API_GraphJSON> {
    public string visualizationName { get; } = "Weighted Maximum Clique Visualization";
    public string visualizationDefinition { get; } = "Displays vertex weights and highlights a verified feasible clique and its edges. Highlighting does not claim optimality.";
    public string source { get; } = "Redux clique graph visualization with vertex weights.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;
    public ISolver solver { get; } = new WeightedMaxCliqueBruteForceSolver();

    public API_GraphJSON visualize(WEIGHTEDMAXCLIQUE problem) {
        API_GraphJSON result = problem.graph.ToAPIGraph();
        foreach (var node in result.nodes)
            node.weight = problem.weights[node.name].ToString(CultureInfo.InvariantCulture);
        return result;
    }

    public API_JSON SolvedVisualization(WEIGHTEDMAXCLIQUE problem, string solution) {
        API_GraphJSON result = visualize(problem);
        IReadOnlyList<string> vertices;
        try {
            if (!new WeightedMaxCliqueSolutionVerifier().verify(problem, solution)) return result;
            vertices = WeightedMaxCliqueSolutionVerifier.ParseCertificate(problem, solution).Vertices;
        } catch (CertificateParseException) {
            // Match the weighted SAT convention: an unreadable answer draws the initial graph.
            return result;
        }

        var selected = vertices.ToHashSet(StringComparer.Ordinal);
        foreach (var node in result.nodes)
            node.color = selected.Contains(node.name) ? "Solution" : "Background";
        foreach (var edge in result.links)
            edge.color = selected.Contains(edge.source) && selected.Contains(edge.target) ? "Solution" : "Background";
        return result;
    }
}
