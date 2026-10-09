using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_DOMINATINGSET.Solvers;

class DominatingSetNaiveInclusionExclusion : ISolver<DOMINATINGSET, NodeSet> {
    // --- Fields ---
    public string solverName { get; } = "Naive Inclusion-Exclusion Backtracking Dominating Set Solver";
    public string solverDefinition { get; } =
        "Exhaustively enumerates every subset of vertices via a binary include/exclude recursion:"
        + " at each vertex, branches into one call that includes it in the candidate set D and one"
        + " that excludes it. No pruning is applied while branching -- the full recursion tree is"
        + " built out to all n vertices before a candidate is checked. Once a subset is complete,"
        + " it is accepted if its size is <= K and it dominates every vertex (every vertex is either"
        + " in D or adjacent to a vertex in D). Returns the first such subset found.";
    public string source { get; } =
    "Fomin, F. V., & Kratsch, D. (2010). Exact Exponential Algorithms. Springer Science & Business Media.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://doi.org/10.1007/978-3-642-16533-7";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Every vertex branches into include/exclude, giving 2^n leaves;
    // each leaf does an O(n + m) domination check.
    public string complexity { get; } = "O(2^n * (n + m)), n = |nodes|, m = |edges|";

    // --- Methods Including Constructors ---
    public DominatingSetNaiveInclusionExclusion() { }

    public string solve(DOMINATINGSET problem) => Solve(problem, StepRecorder<NodeSet>.Off);

    // Steps: include a vertex, back it out, leave it out, and the verdict on each complete subset. Recorder state
    // lives only in this call.
    public string Solve(DOMINATINGSET problem, StepRecorder<NodeSet> rec) {
        int n = problem.nodes.Count;
        int K = problem.K;

        if (n == 0) {
            const string emptyCert = "{}";
            bool emptyOk = problem.defaultVerifier.verify(problem, emptyCert);
            rec.Done(new NodeSet([]), emptyOk, "There are no vertices.");
            return emptyOk ? emptyCert : "{}";
        }

        var indexOf = new Dictionary<string, int>(n);
        for (int i = 0; i < n; i++)
            indexOf[problem.nodes[i]] = i;

        var adj = new List<int>[n];
        for (int i = 0; i < n; i++)
            adj[i] = new List<int>();

        foreach (var edge in problem.edges) {
            int u = indexOf[edge.Key], v = indexOf[edge.Value];
            if (u == v) continue;
            adj[u].Add(v);
            adj[v].Add(u);
        }

        var chosen = new List<int>();
        List<int> solution = new List<int>();

        bool found = Branch(0, n, K, adj, chosen, ref solution, problem, rec);

        if (!found) {
            rec.Done(new NodeSet([]), false, $"No dominating set of size {K} or less exists.");
            return "{}";
        }

        string cert = "{" + string.Join(",", solution.Select(i => problem.nodes[i])) + "}";
        bool verified = problem.defaultVerifier.verify(problem, cert);
        rec.Done(verified ? new NodeSet(solution.Select(i => problem.nodes[i])) : new NodeSet([]), verified,
            verified ? $"{cert} dominates every vertex, with {solution.Count} of at most {K} allowed." : "The set found did not verify.");
        return verified ? cert : "{}";
    }

    // Naive include/exclude recursion over vertex indices [i, n).
    private bool Branch(int i, int n, int K, List<int>[] adj, List<int> chosen, ref List<int> solution,
        DOMINATINGSET problem, StepRecorder<NodeSet> rec) {
        string[] Names() => chosen.Select(c => problem.nodes[c]).ToArray();

        if (chosen.Count > K) {
            rec.Reject(() => new NodeSet(Names()), () => $"{GraphSubsetFrames.Braces(Names())} has more than K = {K} vertices. Stop here.");
            return false;
        }

        if (i == n) {
            if (Dominates(chosen, n, adj)) {
                rec.Accept(() => new NodeSet(Names()), () => $"Try {GraphSubsetFrames.Braces(Names())}. It dominates every vertex.", Names);
                solution = new List<int>(chosen);
                return true;
            }
            rec.Reject(() => new NodeSet(Names()),
                () => $"Try {GraphSubsetFrames.Braces(Names())}: {GraphSubsetFrames.Why(SubsetRule.DominatingSet, problem.nodes, problem.edges, Names())}.",
                Names);
            return false;
        }

        string vertex = problem.nodes[i];
        chosen.Add(i);
        rec.Accept(() => new NodeSet(Names()), () => $"Include {vertex}.", vertex);
        if (Branch(i + 1, n, K, adj, chosen, ref solution, problem, rec))
            return true;
        chosen.RemoveAt(chosen.Count - 1);
        rec.Backtrack(() => new NodeSet(Names()), () => $"Back out {vertex}.", vertex);

        rec.Try(() => new NodeSet(Names()), () => $"Leave {vertex} out.", vertex);
        if (Branch(i + 1, n, K, adj, chosen, ref solution, problem, rec))
            return true;

        return false;
    }

    private bool Dominates(List<int> chosen, int n, List<int>[] adj) {
        var dominated = new bool[n];
        foreach (int v in chosen) {
            dominated[v] = true;
            foreach (int u in adj[v])
                dominated[u] = true;
        }
        for (int v = 0; v < n; v++)
            if (!dominated[v])
                return false;
        return true;
    }
}