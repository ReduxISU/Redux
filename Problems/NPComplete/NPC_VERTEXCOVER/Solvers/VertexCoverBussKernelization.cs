using API.Interfaces;
using System.Linq;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.Solvers;

class VertexCoverBussKernelization : ISolver<VERTEXCOVER> {

    // --- Fields ---
    public string solverName { get; } = "Vertex Cover Buss Kernelization";
    public string solverDefinition { get; } = "Repeats the following reduction until no node's degree"
 + " exceeds the remaining budget k: finds a node with degree greater than k, forces it into the cover"
 + " (since any size-k cover must contain it), decrements k, and removes it along with its incident"
 + " edges. If k drops below zero during this process, reports failure. Once no more forced nodes"
 + " remain, checks whether the reduced edge set exceeds k^2 edges; if so, reports failure, since no"
 + " size-k cover can exist. Otherwise brute-forces all node subsets of the reduced graph up to size k,"
 + " returning the smallest that covers every remaining edge combined with the forced nodes, or failure"
 + " if none is found.";
    public string source { get; } = "Buss, S. R., & Goldsmith, J. (1993). Nondeterminism within P. SIAM Journal on Computing, 22(3), 560-572.";
    public string sourceLink { get; } = "https://doi.org/10.1137/0222038";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Parameterized;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public string complexity { get; } = "O(K * (n + m) + 2^(2K) * m), K = target cover size, n = |nodes|, m = |edges|";

    // --- Methods Including Constructors ---
    public VertexCoverBussKernelization() {

    }
    public string solve(VERTEXCOVER G) {
        var edges = new List<KeyValuePair<string, string>>(G.edges);
        var mandatory = new List<string>();
        int k = G.K;

        bool changed = true;
        while (changed) {
            changed = false;

            var degree = new Dictionary<string, int>();
            foreach (var e in edges) {
                if (!degree.ContainsKey(e.Key)) degree[e.Key] = 0;
                if (!degree.ContainsKey(e.Value)) degree[e.Value] = 0;
                degree[e.Key]++;
                degree[e.Value]++;
            }

            foreach (var kv in degree) {
                if (kv.Value > k) {
                    mandatory.Add(kv.Key);
                    k--;
                    edges = edges.Where(e => e.Key != kv.Key && e.Value != kv.Key).ToList();
                    changed = true;
                    break;
                }
            }

            if (k < 0) {
                return "{}";
            }
        }

        if (edges.Count > k * k) {
            return "{}";
        }

        var kernelNodes = edges.SelectMany(e => new[] { e.Key, e.Value }).Distinct().ToList();
        var kernelCover = bruteForceOnKernel(kernelNodes, edges, k);
        if (kernelCover == null) {
            return "{}";
        }

        mandatory.AddRange(kernelCover);
        return "{" + string.Join(",", mandatory) + "}";
    }

    private List<string>? bruteForceOnKernel(List<string> nodes, List<KeyValuePair<string, string>> edges, int k) {
        if (edges.Count == 0) {
            return new List<string>();
        }

        int limit = Math.Min(k, nodes.Count);
        for (int size = 0; size <= limit; size++) {
            foreach (var combo in combinations(nodes, size)) {
                if (coversAll(combo, edges)) {
                    return combo;
                }
            }
        }
        return null;
    }

    private IEnumerable<List<string>> combinations(List<string> list, int size) {
        if (size == 0) {
            yield return new List<string>();
            yield break;
        }
        for (int i = 0; i <= list.Count - size; i++) {
            foreach (var rest in combinations(list.Skip(i + 1).ToList(), size - 1)) {
                var combo = new List<string> { list[i] };
                combo.AddRange(rest);
                yield return combo;
            }
        }
    }

    private bool coversAll(List<string> cover, List<KeyValuePair<string, string>> edges) {
        var set = new HashSet<string>(cover);
        return edges.All(e => set.Contains(e.Key) || set.Contains(e.Value));
    }
}