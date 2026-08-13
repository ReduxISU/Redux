using API.Interfaces;

namespace API.DummyClasses;

class DummySolver : ISolver<IProblem> {
    public string solverName => "";
    public string solverDefinition => "This is a placeholder solver class for DummyVisualization";
    public string source => "";
    public string[] contributors { get; } = { "" };
    public bool timerHasExpired { get; set; }
    public string solve(IProblem problem) {
        return "";
    }
}