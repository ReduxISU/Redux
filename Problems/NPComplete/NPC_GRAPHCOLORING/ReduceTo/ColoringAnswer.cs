using API.Interfaces;
using SPADE;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo;

/// <summary>
/// Reads a Graph Coloring answer for the reductions out of Graph Coloring, which map it forward (#823). The answer lists the
/// color classes, <c>{{a},{b,d},{c}}</c>; the n-th class that holds a node is color n (0-based), so an empty class takes no
/// color.
/// </summary>
static class ColoringAnswer {
    /// <summary>
    /// Each node's color. Throws <see cref="ReductionInputException"/> (a 400) when the text is not a coloring of the graph
    /// with at most K colors that no edge has both ends of: there is then no answer of the target to map it to.
    /// </summary>
    public static Dictionary<string, int> Read(IReduction reduction, GRAPHCOLORING graph, string certificate) {
        List<List<string>> classes;
        try {
            classes = new UtilCollection(certificate).ToList().Select(c => c.ToList().Select(n => n.ToString()).ToList()).ToList();
        } catch (Exception ex) {
            throw new ReductionInputException(reduction, certificate, graph.certificateFormat, $"'{certificate}' is not a coloring like {{{{a}},{{b,c}}}}", ex);
        }
        var color = new Dictionary<string, int>(StringComparer.Ordinal);
        int used = 0;
        foreach (var cls in classes.Where(c => c.Count > 0)) {
            foreach (string node in cls) {
                if (!graph.nodes.Contains(node))
                    throw Refuse(reduction, graph, certificate, $"'{node}' is not a node of the graph");
                if (!color.TryAdd(node, used))
                    throw Refuse(reduction, graph, certificate, $"'{node}' is in two color classes");
            }
            used++;
        }
        if (used > graph.K)
            throw Refuse(reduction, graph, certificate, $"the coloring uses {used} colors, more than K = {graph.K}");
        foreach (string node in graph.nodes)
            if (!color.ContainsKey(node))
                throw Refuse(reduction, graph, certificate, $"node '{node}' has no color");
        foreach (var e in graph.edges)
            if (e.Key != e.Value && color[e.Key] == color[e.Value])
                throw Refuse(reduction, graph, certificate, $"the edge {{{e.Key},{e.Value}}} has both ends in one color class");
        return color;
    }

    private static ReductionInputException Refuse(IReduction reduction, GRAPHCOLORING graph, string certificate, string why) =>
        new(reduction, certificate, graph.certificateFormat, $"{why}, so it is not a coloring to map");
}
