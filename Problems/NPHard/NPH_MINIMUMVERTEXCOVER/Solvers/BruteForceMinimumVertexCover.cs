using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Interfaces.Graphs.GraphParser;
using System.Linq;
using System.Numerics;
using static API.Interfaces.Graphs.GraphSubsetFrames;

namespace API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Solvers;

// The steps Minimum Vertex Cover Brute Force records: one per candidate set, smallest sizes first. A candidate is a Reject
// naming an edge it leaves uncovered, or the Accept that ends the search (the first cover found is a smallest one); then
// the Done. The recorder's cap hides the rest of a long run.
class BruteForceMinimumVertexCover : ISolver<MINIMUMVERTEXCOVER, NodeSet> {

    // --- Fields ---
    public string solverName { get; } = "Minimum Vertex Cover Brute Force";
    public string solverDefinition { get; } = "This solver tests every subset size, smallest first, and returns the first subset that covers all edges.";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public MathematicalFunction timeComplexity { get; } = new("2^n * n^2", ", n = |nodes|");

    // --- Methods Including Constructors ---
    public BruteForceMinimumVertexCover() {

    }

    // How the steps show a candidate: the indices of the nodes it picks.
    private static RunSteps<List<int>, NodeSet> NewSteps(StepRecorder<NodeSet> rec, List<string> nodes) => new(rec,
        // What every picture shows: the candidate's nodes, every one of them in focus.
        partial: combination => new NodeSet(combination.Select(i => nodes[i])),
        focus: combination => combination.Select(i => nodes[i]).ToArray());

    private BigInteger factorial(BigInteger x) {
        BigInteger y = 1;
        for (BigInteger i = 1; i <= x; i++) {
            y *= i;
        }
        return y;
    }
    private string indexListToCertificate(List<int> indecies, List<string> nodes) {
        return "{" + string.Join(",", indecies.Select(i => nodes[i])) + "}";
    }
    private List<int> nextComb(List<int> combination, int size) {
        for (int i = combination.Count - 1; i >= 0; i--) {
            if (combination[i] + 1 <= (i + size - combination.Count)) {
                combination[i] += 1;
                for (int j = i + 1; j < combination.Count; j++) {
                    combination[j] = combination[j - 1] + 1;
                }
                return combination;
            }
        }
        return combination;
    }
    /// <summary>
    /// Checks whether the given set of node indices covers every edge of G.
    /// </summary>
    private bool isCover(MINIMUMVERTEXCOVER G, List<int> combination) {
        var covered = new HashSet<string>(combination.Select(i => G.nodes[i]));
        foreach (var edge in G.edges) {
            if (!covered.Contains(edge.Key) && !covered.Contains(edge.Value)) {
                return false;
            }
        }
        return true;
    }
    /// <summary>
    /// Solves a MINIMUMVERTEXCOVER instance input.
    /// </summary>
    /// <param name="G"> G is an undirected graph instance string</param>
    /// <returns>
    ///  Smallest subset of nodes that covers all edges of G.
    /// </returns>
    public string solve(MINIMUMVERTEXCOVER G) => Solve(G, StepRecorder<NodeSet>.Off);

    // Tries every set of k nodes, for k = 1, 2, ..., n in turn. The first set that covers every edge is a smallest cover,
    // since every smaller set was tried first.
    public string Solve(MINIMUMVERTEXCOVER G, StepRecorder<NodeSet> rec) {
        var steps = NewSteps(rec, G.nodes);
        int n = G.nodes.Count;

        if (G.edges.Count == 0) {
            steps.Done(new NodeSet([]), true, $"There are no edges, so the empty set covers everything.");
            return "{}";
        }

        int tried = 0;
        for (int k = 1; k <= n; k++) {
            // The first set of k nodes: the first k node indices.
            List<int> combination = new List<int>();
            for (int i = 0; i < k; i++) {
                combination.Add(i);
            }
            BigInteger reps = factorial(n) / (factorial(k) * factorial(n - k));
            for (int i = 0; i < reps; i++) {
                if (timerHasExpired) return SolveRun.TimedOutAnswer;
                tried++;
                if (isCover(G, combination)) {
                    string[] cover = combination.Select(c => G.nodes[c]).ToArray();
                    steps.Accept(combination, $"Try {Words.Braces(cover)}. It covers every edge.");
                    steps.Done(new NodeSet(cover), true,
                        $"{Words.Braces(cover)} covers every edge. Smaller sets were all tried first, so {k} is the smallest possible, found after {tried} tries.");
                    return indexListToCertificate(combination, G.nodes);
                }
                // isCover only says no, so the uncovered edge is looked up for a kept step.
                steps.Reject(combination,
                    $"Try {Words.Braces(combination.Select(c => G.nodes[c]))}: {Why(SubsetRule.MinVertexCover, G.nodes, G.edges, combination.Select(c => G.nodes[c]))}.");
                combination = nextComb(combination, n);
            }
        }
        steps.Done(new NodeSet([]), false, $"Checked all {tried} candidate sets. None covers every edge.");
        return "{}";
    }
}
