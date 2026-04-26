using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using System.Linq;
using System.Collections.Generic;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers;

class GraphColoringGreedy : ISolver<GRAPHCOLORING> {
 
    public string solverName {get;} = "Graph Coloring Greedy";
    public string solverDefinition {get;} = "A greedy algorithm that iterates through each vertex and assigns the smallest color not used by any of its neighbors.";
    public string source {get;} = "Dasgupta, S, Papadimitriou, C, & Vazirani, U. (2006). Algorithms. McGraw-Hill. Chapter 9.2";
    public string[] contributors {get;} = { "Pramesh Shah" };
    public bool timerHasExpired { get; set; }

    // Constructor 
    public GraphColoringGreedy() { }

    // Builds certificate string in format {{a,d},{b},{c}}
    private string BuildCertificate(Dictionary<string, int> colorAssignment, List<string> nodes, int numColors)
    {
        List<string> colorGroups = new List<string>();
        for (int i = 0; i < numColors; i++)
        {
            List<string> group = nodes.Where(node => colorAssignment[node] == i).ToList();
            colorGroups.Add("{" + string.Join(",", group) + "}");
        }
        return "{" + string.Join(",", colorGroups) + "}";
    }

    public string solve(GRAPHCOLORING gColor)
    {
        List<string> nodes = gColor.nodes;
        List<KeyValuePair<string, string>> edges = gColor.edges;

        // Guard, empty graph
        if (nodes == null || nodes.Count == 0)
            return "{}";

        // Build adjacency list
        Dictionary<string, List<string>> adjacency = new Dictionary<string, List<string>>();
        foreach (string node in nodes)
            adjacency[node] = new List<string>();

        foreach (KeyValuePair<string, string> edge in edges) {
            adjacency[edge.Key].Add(edge.Value);
            adjacency[edge.Value].Add(edge.Key);
        }

        // For each vertex assign smallest color not used by neighbors
        Dictionary<string, int> colorAssignment = new Dictionary<string, int>();
        foreach (string node in nodes)
        {
            HashSet<int> usedColors = new HashSet<int>();
            foreach (string neighbor in adjacency[node])
                if (colorAssignment.ContainsKey(neighbor))
                    usedColors.Add(colorAssignment[neighbor]);

            int color = 0;
            while (usedColors.Contains(color))
                color++;

            colorAssignment[node] = color;
        }

        int numColors = colorAssignment.Values.Max() + 1;

        // Guard, greedy used more colors than K allows
        if (numColors > gColor.K)
            return "{}";

        return BuildCertificate(colorAssignment, nodes, numColors);
    }
}