using API.Interfaces;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.JSON_Objects;

namespace API.DummyClasses;

class DummyVisualization : IVisualization<IProblem, API_empty> {

    // --- Fields ---
    public string visualizationName { get; } = "";
    public string visualizationDefinition { get; } = "This is a placeholder visualization class for problems with no visualization";
    public string source { get; } = " ";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "" };
    public VisualizationType visualizationType { get; } = VisualizationType.Unimplemented;
    public ISolver solver { get; } = new DummySolver();

    // --- Methods Including Constructors ---
    public DummyVisualization() {

    }
    public API_empty visualize(IProblem independentSet) {
        return new API_empty();
    }

    // Stands in for any problem, so there is nothing to parse the instance into (IProblem has no
    // constructor). Draw nothing without trying, instead of a parse error about IProblem.
    API_JSON IVisualization.visualize(string problem) {
        return new API_empty();
    }
    API_JSON IVisualization.SolvedVisualization(string problem, string solution) {
        return new API_empty();
    }
    List<API_JSON> IVisualization.StepsVisualization(string problem, List<Object> steps) {
        return new List<API_JSON>();
    }
}
