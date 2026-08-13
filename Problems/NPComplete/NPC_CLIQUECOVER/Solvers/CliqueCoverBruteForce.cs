using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_CLIQUECOVER.Solvers;

class CliqueCoverBruteForce : ISolver<CLIQUECOVER> {

    // --- Fields ---
    public string solverName { get; } = "Clique Cover Brute Force Solver";
    public string solverDefinition { get; } = "This is a brute force solver for the NP-Complete Clique Cover problem";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. SURPRISING: this is not a plain 2^n subset search — `binary`
    // assigns each of the n nodes a label via nextBinary's odometer, and that odometer's
    // carry check ("binary[i] != K") lets each digit range over K+1 values (0..K) before
    // carrying, so the loop runs until every node reaches label K-1, a state reached after
    // Theta((K+1)^n) steps. Each candidate costs O(n^2 * m) to verify (CliqueCoverVerifier's
    // worst case is one group holding all n nodes: O(n^2) pairs x O(m) edge lookup each).
    // With K allowed up to n (see the K > nodes.Count guard in solve()), worst case is
    // Theta((n+1)^n) -- asymptotically worse than n! (n^n / n! -> e^n), i.e. worse than the
    // existing SolverComplexityBucket.Factorial tier; Exponential is kept only because no
    // stronger bucket value exists.
    public string complexity { get; } = "O((K+1)^n * n^2 * m), n = |nodes|, m = |edges|, K = target clique count";

    // --- Methods Including Constructors ---
    public CliqueCoverBruteForce() {

    }


    private string BinaryToCertificate(List<int> binary, List<string> S, int K) {
        string certificate = "{";

        for (int j = 0; j < K; j++) {
            for (int i = 0; i < binary.Count; i++) {
                if (binary[i] == j) {
                    certificate += S[i] + ",";
                }
            }
            certificate = certificate.TrimEnd(',');
            certificate += "},{";
        }

        certificate = certificate.TrimEnd('{');

        return "{" + certificate.TrimEnd(',') + "}";

    }


    private void nextBinary(List<int> binary, int K) {
        for (int i = 0; i < binary.Count; i++) {
            if (binary[i] != K) {
                binary[i] += 1;
                return;
            } else if (binary[i] == K) {
                binary[i] = 0;
            }
        }
    }


    public string solve(CLIQUECOVER clique) {

        if (clique.K > clique.nodes.Count) {
            return "{}";
        }

        List<int> binary = new List<int>();
        foreach (var i in clique.nodes) {
            binary.Add(0);
        }

        while (binary.Count(n => n == (clique.K - 1)) < clique.nodes.Count) {
            string certificate = BinaryToCertificate(binary, clique.nodes, clique.K);
            if (clique.defaultVerifier.verify(clique, certificate)) {
                return certificate;
            }
            nextBinary(binary, clique.K);

        }

        return "{}";
    }
}
