using API.Interfaces;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_NODESET.Solvers;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_NODESET.Visualizations;

class NodeSetDefaultVisualization : IVisualization<NODESET, API_GraphJSON>, ILayeredVisualization<NODESET> {

    // --- Fields ---
    public string visualizationName { get; } = "Node Set Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Node Set";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;
    public ISolver solver { get; } = new NodeSetBruteForce();

    // The Layered Graph picture (format=frames only; /info keeps GraphD3). Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(NodeSet);
    LayeredSpec ILayeredVisualization<NODESET>.Describe(NODESET problem) => Spec(problem);

    /// <summary>The instance as the Layered Graph picture describes it.</summary>
    internal static LayeredSpec Spec(NODESET problem) => new(
        problem.nodes, problem.edges.Select(e => new LayeredEdgeInput(e.Key, e.Value)).ToList(), LayeredRule.FeedbackNodeSet) { K = problem.K };

    // --- Methods Including Constructors ---
    public NodeSetDefaultVisualization() {

    }
    public API_GraphJSON visualize(NODESET nodeSet) {
        return nodeSet.graph.ToAPIGraph();
    }

    public API_JSON SolvedVisualization(NODESET nodeSet, string solution) {
        List<string> solutionNodes = GraphParser.parseNodeListWithStringFunctions(solution);

        API_GraphJSON apiGraph = nodeSet.graph.ToAPIGraph();

        for (int i = 0; i < apiGraph.nodes.Count; i++)
            if (solutionNodes.Contains(apiGraph.nodes[i].name))
                apiGraph.nodes[i].color = "Solution";

        foreach (var node in solutionNodes)
            foreach (var link in apiGraph.links)
                if (link.source == node || link.target == node)
                    link.color = "Red";

        return apiGraph;
    }
}