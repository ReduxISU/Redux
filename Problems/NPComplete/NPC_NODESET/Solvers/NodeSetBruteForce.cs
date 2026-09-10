using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_NODESET.Solvers;

class NodeSetBruteForce : ISolver<NODESET> {

    // --- Fields ---
    public string solverName { get; } = "Node Set Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for the Node Set problem";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Enumerates subsets of size 1..K (K <= nodes.Count, but K can
    // still be Theta(n), so the sum of C(n,i) terms is Theta(2^n) in the worst case); each
    // candidate costs O(K*m) to build the removed-edge set (toEdges) plus O(n^2 * m) to
    // check acyclicity via the reachability fixpoint in isACyclical.
    public string complexity { get; } = "O(2^n * n^2 * m), n = |nodes|, m = |edges|";

    public NodeSetBruteForce() {

    }
    private long factorial(long x) {
        long y = 1;
        for (long i = 1; i <= x; i++) {
            y *= i;
        }
        return y;
    }
    //Function below turns index list into certificate
    private string indexListToCertificate(List<int> indecies, List<string> nodes) {
        string certificate = "";
        foreach (int i in indecies) {
            certificate += nodes[i] + ",";
        }
        certificate = certificate.TrimEnd(',');
        return "{" + certificate + "}";
    }

    // helper function to go through possible combinations
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
    public string solve(NODESET nodeSet) {
        for (int i = 0; i < nodeSet.K; i++) {
            List<int> combination = new List<int>();
            for (int j = 0; j <= i; j++) {
                combination.Add(j);
            }
            long reps = factorial(nodeSet.nodes.Count) / (factorial(i + 1) * factorial(nodeSet.nodes.Count - i - 1));
            for (int k = 0; k < reps; k++) {
                string certificate = indexListToCertificate(combination, nodeSet.nodes);
                if (nodeSet.defaultVerifier.verify(nodeSet, certificate)) {
                    return certificate;
                }
                combination = nextComb(combination, nodeSet.nodes.Count);

            }
        }
        return "{}";
    }

}


