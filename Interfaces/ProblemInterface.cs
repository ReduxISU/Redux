using System.Text.Json.Serialization;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;

namespace API.Interfaces;

interface IProblem {
    string problemName { get; }
    // Link to information on the problem. [JsonIgnore] here only affects JSON written through an
    // IProblem-typed member (e.g. reductionFrom in /ProblemProvider/reduce), which never carried it;
    // /info serializes the concrete class, whose own property is unaffected.
    [JsonIgnore]
    string problemLink { get; }

    string formalDefinition { get; }
    string problemDefinition { get; }
    string inputDescription { get; }
    string outputDescription { get; }
    string source { get; }
    // Repo-relative path of the file declaring this class; see SourceFile.
    string sourceFile { get; }
    // Link to the formal citation in `source`. Optional; not every problem has one. A default
    // interface member is left out of /info, so a problem without one serializes as it always has.
    [JsonIgnore]
    string sourceLink { get => ""; }
    string wikiName { get; }
    string defaultInstance { get; }
    string instance { get; }

    // Format descriptors consumed by /ProblemProvider/info. Each should be a
    // short descriptive sentence with an embedded concrete example so a
    // caller (LLM, GUI, or human) can construct a valid instance/certificate
    // without reading the verifier source. Defaults to "" on problems that
    // haven't been backfilled yet.
    string instanceFormat { get => ""; }
    string certificateFormat { get => ""; }

    // Declared, not derived. The Problems/<Folder>/ layout is a filing convention and is
    // wrong for at least a dozen problems; this is the source of truth.
    ComplexityClass complexityClass { get => ComplexityClass.Unclassified; }

    // Declared, not derived. Subject-matter category (Garey & Johnson's taxonomy) for
    // filtering/discovery, independent of complexityClass.
    ProblemType problemType { get => ProblemType.Unclassified; }

    string[] contributors { get; }

    // A problem that implements IProblem<T, U, V> names its defaults itself. One that implements
    // plain IProblem gets them from [DefaultFor] on the solver/verifier/visualization classes, or a
    // placeholder for a kind with none, so a new problem compiles before those classes exist (#919).
    ISolver defaultSolver { get => ProblemDefaults.Solver(GetType()); }
    IVerifier defaultVerifier { get => ProblemDefaults.Verifier(GetType()); }
    IVisualization defaultVisualization { get => ProblemDefaults.Visualization(GetType()); }
}

interface IProblem<T, U, V> : IProblem where T : ISolver where U : IVerifier where V : IVisualization {
    new T defaultSolver { get; }
    ISolver IProblem.defaultSolver { get => defaultSolver; }
    new U defaultVerifier { get; }
    IVerifier IProblem.defaultVerifier { get => defaultVerifier; }
    new V defaultVisualization { get; }
    IVisualization IProblem.defaultVisualization { get => defaultVisualization; }
}

interface IGraphProblem : IProblem {
    Graph graph { get; }
}

interface IGraphProblem<T, U, V, W> : IProblem<T, U, V>, IGraphProblem where T : ISolver where U : IVerifier where V : IVisualization where W : Graph {
    new W graph { get; }
    Graph IGraphProblem.graph { get => graph; }
}
