using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_MAXCUT.Solvers;
class MaxCutSolver : ISolver<MAXCUT> {

    // --- Fields ---
    public string solverName {get;} = "Max Cut Brute Force Solver";
    public string solverDefinition {get;} = "Enumerates all 2^n partitions of the vertex set and returns the side S of the partition whose crossing edges have maximum total weight. Certificate format: {node1,node2,...} representing the S side.";
    public string source {get;} = "";
    public string[] contributors {get;} = {"Max Gruenwoldt", "Eric Hill"};
    public bool timerHasExpired { get; set; }

    public MaxCutSolver() { }

    private int cutWeight(MAXCUT maxcut, HashSet<string> S) {
        int total = 0;
        foreach (var edge in maxcut.edges) {
            bool srcInS = S.Contains(edge.source);
            bool dstInS = S.Contains(edge.destination);
            if (srcInS != dstInS)
                total += edge.weight;
        }
        return total;
    }

    public string solve(MAXCUT maxcut) {
        List<string> nodes = maxcut.nodes;
        int n = nodes.Count;
        if (n == 0) return "{}";

        int bestWeight = -1;
        HashSet<string> bestS = new();

        // Each bitmask assigns each node to S (bit=1) or T (bit=0).
        // Skip mask 0 (all in T) and (1<<n)-1 (all in S) — both give empty cuts.
        int limit = 1 << n;
        for (int mask = 1; mask < limit - 1; mask++) {
            HashSet<string> S = new();
            for (int i = 0; i < n; i++)
                if ((mask >> i & 1) == 1) S.Add(nodes[i]);

            int w = cutWeight(maxcut, S);
            if (w > bestWeight) {
                bestWeight = w;
                bestS = S;
            }
        }

        if (bestS.Count == 0) return "{}";
        return "{" + string.Join(",", bestS) + "}";
    }
}
