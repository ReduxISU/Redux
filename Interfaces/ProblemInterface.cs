using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;

namespace API.Interfaces;

interface IProblem {
    string problemName { get; }

    string formalDefinition { get; }
    string problemDefinition { get; }
    string source { get; }
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

    ISolver defaultSolver { get; }
    IVerifier defaultVerifier { get; }
    IVisualization defaultVisualization { get; }
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
