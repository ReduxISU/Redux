using API.Interfaces;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.JSON_Objects;

namespace API.DummyClasses;

class DummyVisualization : IVisualization<IProblem> {

    // --- Fields ---
    public string visualizationName {get;} = "";
    public string visualizationDefinition {get;} = "This is a placeholder visualization class for problems with no visualization";
    public string source {get;} = " ";
    public string[] contributors {get;} = {""};
    public string visualizationType { get; } = "";

    // --- Methods Including Constructors ---
    public DummyVisualization() {
        
    }
    public API_JSON visualize(IProblem independentSet)
    {
        return new API_empty();
    }
}