using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.P.P_DFA;
using API.Interfaces.JSON_Objects.Graphs;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.P.P_DFA.Solvers;

namespace API.Problems.P.P_DFA.Visualizations;

class DFAVisualization : IVisualization<DFA> {
    public string visualizationName { get; } = "Deterministic Finite Automata Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Deterministic Finite Automata";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphLaTeX;
    public ISolver solver { get; } = new DFASolver();

    // --- Methods Including Constructors ---
    public DFAVisualization() { }
    API_JSON IVisualization<DFA>.visualize(DFA instance) {
        return instance.graph.ToAPIGraph();
    }

    API_JSON IVisualization<DFA>.SolvedVisualization(DFA instance, string solution) {
        API_GraphJSON apiGraph = instance.graph.ToAPIGraph();

        var lastState = solution
            .Split(':')
            .Skip(1)
            .FirstOrDefault()?
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .LastOrDefault();

        for (int i = 0; i < apiGraph.nodes.Count; i++) {
            if (lastState != null && lastState == apiGraph.nodes[i].name) {
                apiGraph.nodes[i].color = "green";
            } else {
                apiGraph.nodes[i].color = "white";
            }
        }

        return apiGraph;
    }

    public List<API_JSON> StepsVisualization(DFA instance, List<Object> steps) {
        List<string> solutionList = steps.Cast<string>().ToList();
        List<API_GraphJSON> apiGraphs = Enumerable.Range(0, solutionList.Count)
                                                  .Select(_ => instance.graph.ToAPIGraph())
                                                  .ToList();

        for (int i = 0; i < apiGraphs.Count; i++) {
            API_GraphJSON apiGraph = apiGraphs[i];
            string currNode = solutionList[i];

            for (int j = 0; j < apiGraph.nodes.Count; j++) {
                if (apiGraph.nodes[j].name == currNode) {
                    apiGraph.nodes[j].color = "green";
                } else { apiGraph.nodes[j].color = "white"; }
            }

        }

        return apiGraphs.Cast<API_JSON>().ToList();

    }
}