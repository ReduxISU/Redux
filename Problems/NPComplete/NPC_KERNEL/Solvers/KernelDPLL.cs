// Contributors: Prashant Pant and Bektur Akkabakov. LLM implementation.
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_KERNEL.Solvers;

class KernelDPLL : ISolver<KERNEL> {
    public string solverName { get; } = "Kernel DPLL";
    public string solverDefinition { get; } = "Encodes independence and absorption as CNF, propagates unit clauses to a fixed point, then branches on a literal in a shortest unresolved clause. Exact intelligent solver; returns a kernel as {v1,v2,...}, or {} if none exists (also the empty graph's kernel).";
    public string source { get; } = "M. Davis, G. Logemann, and D. Loveland, A machine program for theorem-proving, Communications of the ACM, vol. 5, no. 7, pp. 394-397, 1962, doi:10.1145/368273.368557. Kernel definition: V. Chvatal, Report CRM-300, 1973, https://users.encs.concordia.ca/~chvatal/kernel.html. The kernel-to-CNF encoding and branching choice are implementation adaptations.";
    public string sourceLink { get; } = "https://doi.org/10.1145/368273.368557";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Prashant Pant", "Bektur Akkabakov" };
    public SolverType solverType { get; } = SolverType.Backtracking | SolverType.Heuristic;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // O(2^n) search nodes; at most n assigning propagation passes plus a final
    // pass per node, scanning O(n + m) literals each. O(n)-length assignment
    // copies and collision-sensitive CNF construction fit within this upper bound.
    // Unit propagation and the branching heuristic do not guarantee polynomial time.
    public MathematicalFunction timeComplexity { get; } = new MathematicalFunction("2^n * (n + 1) * (n + m + 1)", ", n = |V|, m = |E|; worst-case upper bound");
    // O(n + m) CNF storage, shared across calls, and O(n) assignment arrays
    // along a recursion path of length O(n); excludes the input graph.
    public MathematicalFunction spaceComplexity { get; } = new MathematicalFunction("(n + 1)^2 + m", ", n = |V|, m = |E|; auxiliary space including recursive calls");
    private volatile bool expired;
    public bool timerHasExpired { get => expired; set => expired = value; }

    public string solve(KERNEL problem) {
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        int[] vertices = problem.vertices.ToArray();
        var index = new Dictionary<int, int>();
        var absorption = new List<int>[vertices.Length];
        for (int i = 0; i < vertices.Length; i++) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            index.Add(vertices[i], i + 1);
            absorption[i] = new List<int> { i + 1 };
        }
        var clauses = new List<int[]>();
        foreach (var edge in problem.edges) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            int u = index[edge.u], v = index[edge.v];
            // x_u => !x_v; a loop forbids x_u.
            clauses.Add(u == v ? new[] { -u } : new[] { -u, -v });
            // !x_u => OR(x_v : u -> v), equivalently x_u OR its successors.
            absorption[u - 1].Add(v);
        }
        foreach (var clause in absorption) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            // Duplicate arcs and loops must not disguise a logical unit clause.
            clauses.Add(clause.Distinct().ToArray());
        }
        int[] assignment = new int[vertices.Length + 1]; // 0 unset, 1 true, -1 false.
        bool found = Search(clauses, assignment);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        return found ? "{" + string.Join(",", vertices.Where((v, i) => assignment[i + 1] == 1)) + "}" : "{}";
    }

    private bool Search(List<int[]> clauses, int[] assignment) {
        while (true) {
            if (timerHasExpired) return false;
            bool changed = false;
            int branch = 0, shortest = int.MaxValue;
            foreach (int[] clause in clauses) {
                if (timerHasExpired) return false;
                bool satisfied = false;
                int unset = 0, candidate = 0;
                foreach (int literal in clause) {
                    if (timerHasExpired) return false;
                    int value = assignment[Math.Abs(literal)];
                    if (value == 0) { unset++; candidate = literal; } else if ((literal > 0 && value == 1) || (literal < 0 && value == -1)) {
                        satisfied = true;
                        break;
                    }
                }
                if (satisfied) continue;
                if (unset == 0) return false;
                if (unset == 1) {
                    assignment[Math.Abs(candidate)] = candidate > 0 ? 1 : -1;
                    changed = true;
                } else if (unset < shortest) { shortest = unset; branch = candidate; }
            }
            if (changed) continue;
            if (branch == 0) return true; // Unset variables may be false.
            int variable = Math.Abs(branch);
            int[] child = (int[])assignment.Clone();
            child[variable] = branch > 0 ? 1 : -1;
            if (Search(clauses, child)) {
                Array.Copy(child, assignment, assignment.Length);
                return true;
            }
            if (timerHasExpired) return false;
            child = (int[])assignment.Clone();
            child[variable] = branch > 0 ? -1 : 1;
            if (!Search(clauses, child)) return false;
            Array.Copy(child, assignment, assignment.Length);
            return true;
        }
    }
}
