// Contributors: Prashant Pant and Bektur Akkabakov. LLM implementation.
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_KERNEL.Solvers;

class KernelBacktracking : ISolver<KERNEL> {
    public string solverName { get; } = "Kernel Backtracking";
    public string solverDefinition { get; } = "Includes or excludes each vertex, pruning adjacent selections and excluded vertices with no selected or undecided outgoing neighbor. Returns a kernel as {v1,v2,...}, or {} if none exists (also the empty graph's kernel).";
    public string source { get; } = "V. Chvatal, On the computational complexity of finding a kernel, Report CRM-300, Universite de Montreal, 1973; author's online account. Include/exclude search and feasibility pruning are an original adaptation of the kernel definition.";
    public string sourceLink { get; } = "https://users.encs.concordia.ca/~chvatal/kernel.html";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Prashant Pant", "Bektur Akkabakov" };
    public SolverType solverType { get; } = SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // At most O(2^n) search nodes, each scanning n vertices and m arcs. Dictionary
    // and HashSet operations can cost O(n) under collisions. With expected O(1)
    // hashing the time bound is O(2^n * (n + m + 1)); pruning helps in practice.
    public MathematicalFunction timeComplexity { get; } = new MathematicalFunction("2^n * (n + 1) * (n + m + 1)", ", n = |V|, m = |E|; worst case including hash collisions");
    // Each of O(n) active recursive calls retains an O(n) absorption set.
    public MathematicalFunction spaceComplexity { get; } = new MathematicalFunction("(n + 1)^2", ", n = |V|; auxiliary space including recursive calls");
    private volatile bool expired;
    public bool timerHasExpired { get => expired; set => expired = value; }

    public string solve(KERNEL problem) {
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        int[] vertices = problem.vertices.ToArray();
        var state = new Dictionary<int, int>();
        foreach (int vertex in vertices) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            state.Add(vertex, -1); // -1 undecided, 0 excluded, 1 included.
        }
        bool found = Search(problem, vertices, state, 0);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        return found ? "{" + string.Join(",", vertices.Where(v => state[v] == 1)) + "}" : "{}";
    }

    private bool Search(KERNEL problem, int[] vertices, Dictionary<int, int> state, int depth) {
        if (timerHasExpired) return false;
        var possibleAbsorption = new HashSet<int>();
        foreach (var edge in problem.edges) {
            if (timerHasExpired) return false;
            if (state[edge.u] == 1 && state[edge.v] == 1) return false;
            if (state[edge.v] != 0) possibleAbsorption.Add(edge.u);
        }
        foreach (int vertex in vertices) {
            if (timerHasExpired) return false;
            if (state[vertex] == 0 && !possibleAbsorption.Contains(vertex)) return false;
        }
        if (depth == vertices.Length) return true;
        int next = vertices[depth];
        state[next] = 1;
        if (Search(problem, vertices, state, depth + 1)) return true;
        if (timerHasExpired) return false;
        state[next] = 0;
        if (Search(problem, vertices, state, depth + 1)) return true;
        state[next] = -1;
        return false;
    }
}
