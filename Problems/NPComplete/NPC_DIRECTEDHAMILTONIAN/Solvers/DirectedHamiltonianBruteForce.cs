using System.Text;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using System.Linq;

namespace API.Problems.NPComplete.NPC_DIRECTEDHAMILTONIAN.Solvers;

class DirectedHamiltonianBruteForce : ISolver<DIRECTEDHAMILTONIAN> {

        // --- Fields ---
        public string solverName { get; } = "Directed Hamiltonian Path Brute Force Solver";
        public string solverDefinition { get; } = "This is a brute force solver for the NP-Complete Directed Hamiltonian Path problem";
        public string source { get; } = "";
        public string[] contributors { get; } = { "Andrija Sevaljevic" };
        public bool timerHasExpired { get; set; }
        // Declared, not derived. Does real (if minor) pruning, but tagged BruteForce per its class name
        // and because the pruning is incidental, not the algorithm's defining feature -- contrast with
        // the Backtracking cluster, where pruning/bounding IS the defining feature. Factorial: enumerates
        // permutations of vertices.
        public SolverType solverType { get; } = SolverType.BruteForce;
        public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Factorial;
        // PermuteWithPruning's edge check only trims a branch once an edge is missing; on a dense/complete
        // graph (the worst case) it never trips, so all n! permutations are still generated. Each yielded
        // permutation costs O(n) to build (CombinationToCertificate), and DirectedHamiltonianVerifier walks
        // it in O(n) steps, each doing an O(m) List<>.Contains edge lookup -- O(n*m) per verify call.
        public string complexity { get; } = "O(n! * n * m), n = |nodes|, m = |edges|";

        // --- Methods Including Constructors ---

        public DirectedHamiltonianBruteForce() { }

        private string CombinationToCertificate(List<string> combination) {
                var sb = new StringBuilder();
                sb.Append("{");
                foreach (var node in combination)
                        sb.Append(node).Append(",");
                sb.Append(combination[0]).Append("}");
                return sb.ToString();
        }

        private bool HasEdge(DIRECTEDHAMILTONIAN graph, string fromNode, string toNode) {
                return graph.edges.Any(e => e.Key == fromNode && e.Value == toNode);
        }

        private IEnumerable<List<string>> PermuteWithPruning(List<string> nodes, DIRECTEDHAMILTONIAN graph, int start = 0) {
                if (start == nodes.Count - 1) {
                        yield return new List<string>(nodes);
                }
                else {
                        for (int i = start; i < nodes.Count; i++) {
                                (nodes[start], nodes[i]) = (nodes[i], nodes[start]);

                                // Early pruning: check edge between last node in partial path and current node
                                if (start == 0 || HasEdge(graph, nodes[start - 1], nodes[start])) {
                                        foreach (var perm in PermuteWithPruning(nodes, graph, start + 1))
                                                yield return perm;
                                }

                                (nodes[start], nodes[i]) = (nodes[i], nodes[start]);
                        }
                }
        }

        public string solve(DIRECTEDHAMILTONIAN hamiltonian) {
                foreach (var combination in PermuteWithPruning(hamiltonian.nodes, hamiltonian)) {
                        string certificate = CombinationToCertificate(combination);
                        if (hamiltonian.defaultVerifier.verify(hamiltonian, certificate)) {
                                return certificate;
                        }
                }

                return "{}";
        }
}
