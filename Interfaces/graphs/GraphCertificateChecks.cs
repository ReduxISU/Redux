namespace API.Interfaces.Graphs;

/// <summary>
/// Shared checks for verifiers whose certificate is a set of edges of an undirected graph.
/// </summary>
static class GraphCertificateChecks {
    private static (string, string) Canonical(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);

    /// <summary>
    /// True when every certificate edge is an edge of the graph (either orientation) and no
    /// edge is repeated (either orientation).
    /// </summary>
    public static bool EdgesExistAndDistinct(IEnumerable<(string, string)> graphEdges, IEnumerable<(string, string)> certificateEdges) {
        HashSet<(string, string)> existing = graphEdges.Select(e => Canonical(e.Item1, e.Item2)).ToHashSet();
        HashSet<(string, string)> seen = new HashSet<(string, string)>();
        foreach ((string a, string b) in certificateEdges) {
            (string, string) key = Canonical(a, b);
            if (!existing.Contains(key) || !seen.Add(key)) {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// True when there is a partition of the vertices into S and V\S whose set of crossing
    /// edges is exactly the certificate edges. Equivalently, the parity constraints
    /// "endpoints differ iff the edge is in the certificate" are consistent over every graph
    /// edge. Components (and isolated vertices) are free to flip independently, which the
    /// union-find with parity handles without enumerating colorings.
    /// </summary>
    public static bool IsExactCut(IEnumerable<string> nodes, IEnumerable<(string, string)> graphEdges, IEnumerable<(string, string)> certificateEdges) {
        HashSet<(string, string)> inCut = certificateEdges.Select(e => Canonical(e.Item1, e.Item2)).ToHashSet();
        Dictionary<string, string> parent = new Dictionary<string, string>();
        Dictionary<string, int> parity = new Dictionary<string, int>(); // parity of a node relative to its parent

        foreach (string n in nodes) {
            parent.TryAdd(n, n);
            parity.TryAdd(n, 0);
        }

        (string root, int par) Find(string x) {
            int total = 0;
            string cur = x;
            List<string> path = new List<string>();
            while (parent[cur] != cur) {
                path.Add(cur);
                total ^= parity[cur];
                cur = parent[cur];
            }
            // Path compression: point every visited node straight at the root.
            int running = total;
            foreach (string p in path) {
                int own = parity[p];
                parent[p] = cur;
                parity[p] = running;
                running ^= own;
            }
            return (cur, total);
        }

        foreach ((string a, string b) in graphEdges) {
            parent.TryAdd(a, a);
            parity.TryAdd(a, 0);
            parent.TryAdd(b, b);
            parity.TryAdd(b, 0);
            int required = inCut.Contains(Canonical(a, b)) ? 1 : 0;
            (string ra, int pa) = Find(a);
            (string rb, int pb) = Find(b);
            if (ra == rb) {
                if ((pa ^ pb) != required) {
                    return false;
                }
            } else {
                parent[ra] = rb;
                parity[ra] = pa ^ pb ^ required;
            }
        }
        return true;
    }
}
