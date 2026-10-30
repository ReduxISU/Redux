using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects.Graphs;

namespace API.Problems.NPComplete.NPC_UNRESTRICTEDLONGESTPATH.Visualizations;

class UnrestrictedLongestPathVisualization : IVisualization<UNRESTRICTEDLONGESTPATH, API_GraphJSON> {
    public string visualizationName { get; } = "Unrestricted Longest Path Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for unrestricted longest path problem";
    public string source { get; } = "Monien, B. (1985). How to find long paths efficiently. North-Holland Mathematics Studies, 239–254.";
    public string sourceLink { get; } = "https://doi.org/10.1016/s0304-0208(08)73110-4";
    public string[] contributors { get; } = { "Alice Giola", "Sadman Hasan", "Javier Rodriguez Lopez" };

    public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;
    // The solver whose steps this visualization shows. Defaults to the problem's default solver.
    // Replace it with a specific solver if you prefer, e.g. `new MyBruteForce()`. This must stay
    // expression-bodied (=>): the problem's constructor builds its default visualization, so an
    // initializer (=) here would recurse forever.
    public ISolver solver => new UNRESTRICTEDLONGESTPATH().defaultSolver;

    public string sourceFile { get; } = SourceFile.Path();

    // --- Methods Including Constructors ---
    public UnrestrictedLongestPathVisualization() {

    }
    public API_GraphJSON visualize(UNRESTRICTEDLONGESTPATH instance) {
        return instance.graph.ToAPIGraph();
    }

    public API_JSON SolvedVisualization(UNRESTRICTEDLONGESTPATH instance, string solution) {
        API_GraphJSON apiGraph = instance.graph.ToAPIGraph();
        if (string.IsNullOrWhiteSpace(solution) || solution.Trim() == "{}")
            return apiGraph;

        List<string> solutionList = GraphParser.parseNodeListWithStringFunctions(solution)
            .Where(node => !string.IsNullOrWhiteSpace(node))
            .ToList();
        if (solutionList.Count == 0)
            return apiGraph;

        // Highlight nodes that are part of the solution path.
        if (solutionList.Count > 0) {
            string firstNode = solutionList[0];
            string lastNode = solutionList[solutionList.Count - 1];

            foreach (var node in apiGraph.nodes) {
                if (solutionList.Contains(node.name)) {
                    if (node.name == firstNode || node.name == lastNode) {
                        node.color = "Red";
                    } else {
                        node.color = "Solution";
                    }
                }

            }
        }

        // Highlight edges that are part of the solution path.
        for (int i = 0; i < solutionList.Count - 1; i++) {
            string from = solutionList[i];
            string to = solutionList[i + 1];

            foreach (var link in apiGraph.links) {
                if ((link.source == from && link.target == to)
                    || (link.source == to && link.target == from)) {
                    link.color = "Solution";
                }
            }
        }

        return apiGraph;
    }
}