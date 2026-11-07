using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System.Linq;
using static API.Interfaces.Graphs.GraphSubsetFrames;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.Solvers;

// The steps Vertex Cover Buss Kernelization records:
//   - an Accept for each node forced into the cover (it touches more edges than the budget left);
//   - a Reject when the forced nodes use up the budget, or too many edges are left for what remains;
//   - then, on the kernel, a Reject per candidate set that leaves an edge uncovered, and an Accept for the one that covers
//     every edge (each candidate shown with the forced nodes);
//   - the Done, with the cover or none.
class VertexCoverBussKernelization : ISolver<VERTEXCOVER, NodeSet> {

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
    public string source { get; } = "Buss, J. F., & Goldsmith, J. (1993). Nondeterminism within P. SIAM Journal on Computing, 22(3), 560-572.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://doi.org/10.1137/0222038";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Parameterized by K (Buss's kernel), then bruteForceOnKernel() tries every
    // subset of the kernel in size order without pruning, which is BruteForce rather than Backtracking.
    public SolverType solverType { get; } = SolverType.BruteForce | SolverType.Parameterized;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public MathematicalFunction timeComplexity { get; } = new("K * (n + m) + 2^(2K) * m", ", K = target cover size, n = |nodes|, m = |edges|");

    // --- Methods Including Constructors ---
    public VertexCoverBussKernelization() {

    }
    // How the steps show the cover so far: the forced nodes, and the kernel candidate tried with them (null: none yet).
    // A kernel candidate is in focus with the forced nodes; a forced node's step says its own focus.
    private static RunSteps<(List<string> Forced, List<string>? Kernel), NodeSet> NewSteps(StepRecorder<NodeSet> rec) => new(rec,
        // What every picture shows: the forced nodes and the kernel candidate.
        partial: s => new NodeSet(s.Kernel == null ? s.Forced : s.Forced.Concat(s.Kernel)),
        focus: s => s.Kernel == null ? [] : s.Forced.Concat(s.Kernel).ToArray());

    public string solve(VERTEXCOVER G) => Solve(G, StepRecorder<NodeSet>.Off);

    // A node that touches more than k edges is in every cover of size k (else all of its neighbours would be), so it is
    // forced in. Once none is left, k nodes cover at most k * k edges, so more edges than that means no cover; otherwise
    // the small graph that is left (the kernel) is brute-forced.
    public string Solve(VERTEXCOVER G, StepRecorder<NodeSet> rec) {
        var steps = NewSteps(rec);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
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
                    int budget = k;
                    k--;
                    edges = edges.Where(e => e.Key != kv.Key && e.Value != kv.Key).ToList();
                    changed = true;
                    steps.Accept((mandatory, null),
                        $"Take {kv.Key}: it touches {kv.Value} edges, more than the {budget} nodes still allowed, so every small enough cover must include it.",
                        focus: () => [kv.Key]);
                    break;
                }
            }

            if (k < 0) {
                steps.Reject((mandatory, null), $"The forced nodes already use more than K nodes.");
                steps.Done(new NodeSet([]), false, $"No vertex cover of size {G.K} or less exists.");
                return "{}";
            }
        }

        if (edges.Count > k * k) {
            steps.Reject((mandatory, null), $"{edges.Count} edges are left but {k} more nodes can cover at most {k * k} of them here.");
            steps.Done(new NodeSet([]), false, $"No vertex cover of size {G.K} or less exists.");
            return "{}";
        }

        var kernelNodes = edges.SelectMany(e => new[] { e.Key, e.Value }).Distinct().ToList();
        var kernelCover = bruteForceOnKernel(kernelNodes, edges, k, G, mandatory, steps);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        if (kernelCover == null) {
            steps.Done(new NodeSet([]), false, $"No vertex cover of size {G.K} or less exists.");
            return "{}";
        }

        mandatory.AddRange(kernelCover);
        steps.Done(new NodeSet(mandatory), true, $"{Words.Braces(mandatory)} covers every edge, within K = {G.K}.");
        return "{" + string.Join(",", mandatory) + "}";
    }

    private List<string>? bruteForceOnKernel(List<string> nodes, List<KeyValuePair<string, string>> edges, int k,
        VERTEXCOVER G, List<string> mandatory, RunSteps<(List<string> Forced, List<string>? Kernel), NodeSet> steps) {
        if (edges.Count == 0) {
            return new List<string>();
        }

        // Every set of kernel nodes, smallest first, up to the budget left.
        int limit = Math.Min(k, nodes.Count);
        for (int size = 0; size <= limit; size++) {
            foreach (var combo in combinations(nodes, size)) {
                if (timerHasExpired) return null;
                if (coversAll(combo, edges)) {
                    steps.Accept((mandatory, combo), $"Try {Words.Braces(mandatory.Concat(combo))}. It covers every edge.");
                    return combo;
                }
                // coversAll only says no, so the uncovered edge is looked up for a kept step.
                steps.Reject((mandatory, combo),
                    $"Try {Words.Braces(mandatory.Concat(combo))}: {Why(SubsetRule.VertexCover, G.nodes, G.edges, mandatory.Concat(combo))}.");
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