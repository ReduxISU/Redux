using System.Text;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using System.Linq;

namespace API.Problems.NPComplete.NPC_HAMILTONIAN.Solvers
{
    class HamiltonianBruteForce : ISolver<HAMILTONIAN>
    {
        public string solverName { get; } = "Hamiltonian Path Brute Force Solver";
        public string solverDefinition { get; } = "This is a brute force solver for the NP-Complete Hamiltonian Path problem";
        public string source { get; } = "";
        public string[] contributors { get; } = { "Andrija Sevaljevic" };

        public HamiltonianBruteForce() { }

        private string CombinationToCertificate(List<string> combination)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            foreach (var node in combination)
                sb.Append(node).Append(",");
            sb.Append(combination[0]).Append("}");
            return sb.ToString();
        }

        // Undirected edge check
        private bool HasEdgeUndirected(HAMILTONIAN graph, string nodeA, string nodeB)
        {
            return graph.edges.Any(e =>
                (e.Key == nodeA && e.Value == nodeB) ||
                (e.Key == nodeB && e.Value == nodeA));
        }

        private IEnumerable<List<string>> PermuteWithPruning(List<string> nodes, HAMILTONIAN graph, int start = 0)
        {
            if (start == nodes.Count - 1)
            {
                yield return new List<string>(nodes);
            }
            else
            {
                for (int i = start; i < nodes.Count; i++)
                {
                    (nodes[start], nodes[i]) = (nodes[i], nodes[start]);

                    // Early pruning: check edge between last node in partial path and current node
                    if (start == 0 || HasEdgeUndirected(graph, nodes[start - 1], nodes[start]))
                    {
                        foreach (var perm in PermuteWithPruning(nodes, graph, start + 1))
                            yield return perm;
                    }

                    (nodes[start], nodes[i]) = (nodes[i], nodes[start]);
                }
            }
        }

        public string solve(HAMILTONIAN hamiltonian)
        {
            foreach (var combination in PermuteWithPruning(hamiltonian.nodes, hamiltonian))
            {
                string certificate = CombinationToCertificate(combination);
                if (hamiltonian.defaultVerifier.verify(hamiltonian, certificate))
                {
                    return certificate;
                }
            }

            return "{}";
        }
    }
}
