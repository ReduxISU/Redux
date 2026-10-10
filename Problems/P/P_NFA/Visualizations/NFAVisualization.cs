using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.JSON_Objects;
using API.Problems.P.P_NFA;
using API.Interfaces.JSON_Objects.Graphs;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.P.P_NFA.Solvers;
using API.Interfaces.Graphs;

namespace API.Problems.P.P_NFA.Visualizations;

class NFAVisualization : IVisualization<NFA, API_GraphJSON>, IAutomatonVisualization<NFA> {
    public string visualizationName { get; } = "Non-Deterministic Finite Automata Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Non-deterministic Finite Automata";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphLaTeX;
    public ISolver solver { get; } = new NFASolver();

    // Implemented explicitly so it is not a public member (and so not part of /info).
    // This visualization draws no step frames, but it declares the shape its solver records so that
    // the steps are not dropped as a mismatch.
    Type? IVisualization.StepShape => typeof(ActiveStates);

    // The Automaton picture (format=frames only; /info keeps GraphLaTeX), with the NFA's runs for the path picker. The
    // list format keeps getting the typed steps it got before the picture existed (it draws none of them).
    AutomatonSpec IAutomatonVisualization<NFA>.Describe(NFA problem) => Spec(problem);
    bool IFramesVisualization.ListReadsTypedSteps() => true;

    /// <summary>The NFA as the Automaton picture describes it (ε-transitions have the symbol "ε").</summary>
    internal static AutomatonSpec Spec(NFA problem) => new(AutomatonKind.Nfa,
        problem.nodes, problem.alphabet.Select(c => c.ToString()).ToList(),
        problem.edges.Select(e => new AutomatonTransition(e.From, e.Symbol.ToString(), e.To)).ToList(),
        problem.startState, problem.acceptStates, problem.inputString);

    // --- Methods Including Constructors ---
    public NFAVisualization() { }

    API_GraphJSON IVisualization<NFA, API_GraphJSON>.visualize(NFA instance) {
        return instance.graph.ToAPIGraph();
    }

    API_JSON IVisualization<NFA>.SolvedVisualization(NFA instance, string solution) {
        return instance.graph.ToAPIGraph();
    }
}