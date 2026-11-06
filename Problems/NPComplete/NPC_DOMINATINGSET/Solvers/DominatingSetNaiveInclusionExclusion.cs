using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_DOMINATINGSET.Solvers;

// The steps Naive Inclusion-Exclusion records. Despite the name it counts nothing: it decides, vertex by vertex, whether
// the vertex is in the set (the "include" branch) or not (the "exclude" branch), so its steps are those of a search:
//   - an Accept when it includes a vertex (keeps it in the set for this branch);
//   - a Backtrack when it backs that vertex out again;
//   - a Try when it leaves the vertex out (the other branch, which can still fail);
//   - for a finished set, a Reject naming the vertices it leaves undominated, or the Accept that ends the search;
//   - a Reject when the set has grown past K;
//   - the Done, with the dominating set or none.
// The set is built one decision at a time, so the solver is an IIncrementalSolver.
class DominatingSetNaiveInclusionExclusion : ISolver<DOMINATINGSET, NodeSet>, IIncrementalSolver {
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
    public MathematicalFunction timeComplexity { get; } = new("2^n * (n + m)", ", n = |nodes|, m = |edges|");

    // --- Methods Including Constructors ---
    public DominatingSetNaiveInclusionExclusion() { }

    // How the steps show the search: the vertex indices included so far, and the vertex the step decides on (-1: none).
    private static RunSteps<(List<int> Chosen, int Vertex), NodeSet> NewSteps(StepRecorder<NodeSet> rec, List<string> names) => new(rec,
        // What every picture shows: the set so far, and the vertex included, backed out or left out.
        partial: s => new NodeSet(s.Chosen.Select(c => names[c])),
        focus: s => s.Vertex < 0 ? [] : [names[s.Vertex]]);

    public string solve(DOMINATINGSET problem) => Solve(problem, StepRecorder<NodeSet>.Off);

    public string Solve(DOMINATINGSET problem, StepRecorder<NodeSet> rec) {
        var steps = NewSteps(rec, problem.nodes);
        int n = problem.nodes.Count;
        int K = problem.K;

        if (n == 0) {
            const string emptyCert = "{}";
            bool emptyOk = problem.defaultVerifier.verify(problem, emptyCert);
            steps.Done(new NodeSet([]), emptyOk, $"There are no vertices.");
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

        bool found = Branch(0, n, K, adj, chosen, ref solution, problem, steps);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;

        if (!found) {
            steps.Done(new NodeSet([]), false, $"No dominating set of size {K} or less exists.");
            return "{}";
        }

        string cert = "{" + string.Join(",", solution.Select(i => problem.nodes[i])) + "}";
        bool verified = problem.defaultVerifier.verify(problem, cert);
        if (verified)
            steps.Done(new NodeSet(solution.Select(i => problem.nodes[i])), true, $"{cert} dominates every vertex, with {solution.Count} of at most {K} allowed.");
        else
            steps.Done(new NodeSet([]), false, $"The set found did not verify.");
        return verified ? cert : "{}";
    }

    // Naive include/exclude recursion over vertex indices [i, n).
    private bool Branch(int i, int n, int K, List<int>[] adj, List<int> chosen, ref List<int> solution,
        DOMINATINGSET problem, RunSteps<(List<int> Chosen, int Vertex), NodeSet> steps) {
        // Unwind at the time limit; Solve() then returns the timed-out answer.
        if (timerHasExpired)
            return false;

        if (chosen.Count > K) {
            steps.Reject((chosen, -1), $"{GraphSubsetFrames.Braces(chosen.Select(c => problem.nodes[c]))} has more than K = {K} vertices. Stop here.");
            return false;
        }

        if (i == n) {
            if (Dominates(chosen, n, adj)) {
                steps.Accept((chosen, -1), $"Try {GraphSubsetFrames.Braces(chosen.Select(c => problem.nodes[c]))}. It dominates every vertex.",
                    focus: () => chosen.Select(c => problem.nodes[c]).ToArray());
                solution = new List<int>(chosen);
                return true;
            }
            // Dominates only says no, so what fails is looked up for a kept step. The set was built one decision at a time,
            // so the focus is what failed (the vertices left undominated), not the set.
            steps.Reject((chosen, -1),
                $"Try {GraphSubsetFrames.Braces(chosen.Select(c => problem.nodes[c]))}: {GraphSubsetFrames.Why(SubsetRule.DominatingSet, problem.nodes, problem.edges, chosen.Select(c => problem.nodes[c]))}.",
                focus: () => Undominated(chosen, n, adj).Select(v => problem.nodes[v]).ToArray());
            return false;
        }

        string vertex = problem.nodes[i];
        chosen.Add(i);
        // Include vertex i, and decide the rest; if that fails, back it out and decide the rest without it.
        steps.Accept((chosen, i), $"Include {vertex}.");
        if (Branch(i + 1, n, K, adj, chosen, ref solution, problem, steps))
            return true;
        chosen.RemoveAt(chosen.Count - 1);
        steps.Backtrack((chosen, i), $"Back out {vertex}.");

        steps.Try((chosen, i), $"Leave {vertex} out.");
        if (Branch(i + 1, n, K, adj, chosen, ref solution, problem, steps))
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

    // The vertices that neither are in 'chosen' nor have a neighbor in it, in index order.
    private static IEnumerable<int> Undominated(List<int> chosen, int n, List<int>[] adj) {
        var dominated = new bool[n];
        foreach (int v in chosen) {
            dominated[v] = true;
            foreach (int u in adj[v])
                dominated[u] = true;
        }
        return Enumerable.Range(0, n).Where(v => !dominated[v]);
    }
}