using API.Interfaces;

namespace API.DummyClasses;

class DummySolver : ISolver<IProblem> {
    public string solverName => "";
    public string solverDefinition => "This is a placeholder solver class for DummyVisualization";
    public string source => "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "" };
    public bool timerHasExpired { get; set; }
    public string solve(IProblem problem) {
        return "";
    }

    // Stands in for any problem, so there is nothing to parse the instance into (IProblem has no
    // constructor). Answer "" without trying, instead of a parse error about IProblem.
    string ISolver.solve(string problem) {
        return "";
    }
    List<Object> ISolver.GetSteps(string instance) {
        return new List<Object>();
    }
}
