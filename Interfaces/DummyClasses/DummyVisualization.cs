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

    // The stand-in for problems that have no picture. Its problem type is the IProblem interface, which no instance
    // string can be parsed into, so the default string entry points (which parse first) threw for every problem
    // wired to it, e.g. visualizeReduction into SAT (#755). They answer "no picture" without parsing instead.
    API_JSON IVisualization.visualize(string problem) => new API_empty();
    API_JSON IVisualization.SolvedVisualization(string problem, string solution) => new API_empty();
    List<API_JSON> IVisualization.StepsVisualization(string problem, List<Object> steps) => new List<API_JSON>();
}