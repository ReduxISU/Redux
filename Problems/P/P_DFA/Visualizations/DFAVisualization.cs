using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.JSON_Objects;
using API.Problems.P.P_DFA;
using API.Interfaces.JSON_Objects.Graphs;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.P.P_DFA.Solvers;

namespace API.Problems.P.P_DFA.Visualizations;

class DFAVisualization : IVisualization<DFA, API_GraphJSON> {
    public string visualizationName { get; } = "Deterministic Finite Automata Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Deterministic Finite Automata";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphLaTeX;
    public ISolver solver { get; } = new DFASolver();

    // Implemented explicitly so it is not a public member (and so not part of /info).
    Type? IVisualization.StepShape => typeof(ActiveStates);

    // --- Methods Including Constructors ---
    public DFAVisualization() { }
    API_GraphJSON IVisualization<DFA, API_GraphJSON>.visualize(DFA instance) {
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
        // One frame per state the run entered: the start state and each transition taken (Try steps).
        List<ActiveStates> visited = steps.Cast<SolverStep<ActiveStates>>()
                                          .Where(s => s.Event == StepEvent.Try)
                                          .Select(s => s.Partial)
                                          .ToList();
        List<API_GraphJSON> apiGraphs = Enumerable.Range(0, visited.Count)
                                                  .Select(_ => instance.graph.ToAPIGraph())
                                                  .ToList();

        for (int i = 0; i < apiGraphs.Count; i++) {
            API_GraphJSON apiGraph = apiGraphs[i];
            string[] active = visited[i].States;

            for (int j = 0; j < apiGraph.nodes.Count; j++) {
                if (active.Contains(apiGraph.nodes[j].name)) {
                    apiGraph.nodes[j].color = "green";
                } else { apiGraph.nodes[j].color = "white"; }
            }

        }

        return apiGraphs.Cast<API_JSON>().ToList();

    }
}