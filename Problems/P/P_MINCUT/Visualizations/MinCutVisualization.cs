using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.P.P_MINCUT.Solvers;
using SPADE;

namespace API.Problems.P.P_MINCUT.Visualizations;

class MinCutVisualization : IVisualization<MINCUT>
{
    public string visualizationName { get; } = "Minimum Cut Visualization";
    public string visualizationDefinition { get; } = "Displays a weighted undirected graph and highlights the edges belonging to the minimum cut.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;
    public ISolver solver { get; } = new MinCutStoerWagner();

    public MinCutVisualization() { }

    public API_JSON visualize(MINCUT problem) => problem.graph.ToAPIGraph();

    public API_JSON SolvedVisualization(MINCUT problem, string solution)
    {
        if (string.IsNullOrWhiteSpace(solution) || solution.Trim() == "{}")
            return visualize(problem);

        API_GraphJSON apiGraph = problem.graph.ToAPIGraph();

        UtilCollection edgeList;
        try { edgeList = new UtilCollection(solution); }
        catch { return apiGraph; }

        HashSet<string> cutNodes = new();

        foreach (UtilCollection item in edgeList)
        {
            try
            {
                List<UtilCollection> endpoints = item[0].ToList();
                string src = endpoints[0].ToString();
                string dst = endpoints[1].ToString();

                var link = apiGraph.links.FirstOrDefault(l =>
                    (l.source == src && l.target == dst) || (l.source == dst && l.target == src));
                if (link != null)
                {
                    link.color = "Solution";
                    link.dashed = "True";
                }

                cutNodes.Add(src);
                cutNodes.Add(dst);
            }
            catch { }
        }

        foreach (var node in apiGraph.nodes)
            if (cutNodes.Contains(node.name))
                node.color = "Solution";

        return apiGraph;
    }
}
