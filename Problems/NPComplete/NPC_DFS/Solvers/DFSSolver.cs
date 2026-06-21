using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.NPComplete.NPC_DFS;
using SPADE;

namespace API.Problems.NPComplete.NPC_DFS.Solvers;

class DFSSolver : ISolver<DFS>
{
    public string solverName { get; } = "Depth-First Search Algorithm";
    public string solverDefinition { get; } =
    "This solver implements depth-first search to find the first path from a source node to a target node.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Scott Barfuss" };
    public bool timerHasExpired { get; set; }

    public string solve(DFS problem)
    {
        TraversalResult traversal = Traverse(problem, () => timerHasExpired);
        if (traversal.Canceled)
            return "{}";

        return NodeListToCertificate(traversal.Path);
    }

    public List<Object> GetSteps(DFS problem)
    {
        return Traverse(problem, () => timerHasExpired).Steps.Cast<object>().ToList();
    }

    internal static TraversalResult Traverse(DFS problem, Func<bool>? shouldStop = null)
    {
        var adjacency = BuildAdjacency(problem.graph);
        var visited = new HashSet<string>();
        var currentPath = new List<string>();
        var solutionPath = new List<string>();
        var stepCertificates = new List<string>();
        bool canceled = false;

        bool Search(string current)
        {
            if (shouldStop?.Invoke() == true)
            {
                canceled = true;
                return false;
            }

            visited.Add(current);
            currentPath.Add(current);
            stepCertificates.Add(NodeListToCertificate(currentPath));

            if (current == problem.targetNode)
            {
                solutionPath = new List<string>(currentPath);
                return true;
            }

            if (adjacency.TryGetValue(current, out var neighbors))
            {
                foreach (string neighbor in neighbors)
                {
                    if (visited.Contains(neighbor))
                        continue;

                    if (Search(neighbor))
                        return true;

                    if (canceled)
                        return false;
                }
            }

            currentPath.RemoveAt(currentPath.Count - 1);
            if (currentPath.Count > 0)
                stepCertificates.Add(NodeListToCertificate(currentPath));
            return false;
        }

        Search(problem.sourceNode);
        return new TraversalResult(solutionPath, stepCertificates, canceled);
    }

    internal static Dictionary<string, List<string>> BuildAdjacency(UtilCollectionGraph graph)
    {
        var adjacency = new Dictionary<string, List<string>>();

        foreach (var node in graph.Nodes)
        {
            string nodeName = node.ToString()!;
            adjacency[nodeName] = new List<string>();
        }

        foreach (UtilCollection rawEdge in graph.Edges.ToList())
        {
            List<UtilCollection> cast = rawEdge.ToList();
            if (cast.Count == 0 || cast.Count > 2)
                throw new InvalidOperationException("DFS edges must be pairs.");

            string from = cast[0].ToString();
            string to = cast.Count == 1 ? cast[0].ToString() : cast[1].ToString();

            AddDirected(adjacency, from, to);
            if (!rawEdge.IsOrdered() && from != to)
                AddDirected(adjacency, to, from);
        }

        return adjacency;
    }

    private static void AddDirected(Dictionary<string, List<string>> adjacency, string from, string to)
    {
        if (!adjacency.TryGetValue(from, out var neighbors))
        {
            neighbors = new List<string>();
            adjacency[from] = neighbors;
        }

        neighbors.Add(to);

        if (!adjacency.ContainsKey(to))
            adjacency[to] = new List<string>();
    }

    internal static string NodeListToCertificate(List<string> nodes)
    {
        if (nodes == null || nodes.Count == 0)
            return "{}";

        return "{" + string.Join(",", nodes) + "}";
    }

    internal sealed record TraversalResult(List<string> Path, List<string> Steps, bool Canceled);
}
