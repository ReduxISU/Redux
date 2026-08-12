using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers;

class GraphColoringBruteForce : ISolver<GRAPHCOLORING> {

        // --- Fields ---
        public string solverName { get; } = "Graph Coloring Brute Force";
        public string solverDefinition { get; } = "This is a brute force solver for the NP-Complete Graph Coloring problem";
        public string source { get; } = "";
        public string[] contributors { get; } = { "Andrija Sevaljevic" };
        public bool timerHasExpired { get; set; }
        // Declared, not derived. Unpruned exhaustive enumeration.
        public SolverType solverType { get; } = SolverType.BruteForce;
        public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
        // Declared, not derived. SURPRISING: this is not a plain numColors^n coloring search -- nextBinary's
        // carry check ("binary[i] != K" where K=numColors) lets each digit range over numColors+1 values
        // (0..numColors) before carrying, so it's a base-(numColors+1) counter over n digits (same odometer
        // pattern as CliqueCoverBruteForce's nextBinary). The outer while's stopping condition (all digits
        // == numColors-1) fires close to, but before, the counter's natural rollover, so it still runs
        // Theta((numColors+1)^n) candidates in the worst case. Each candidate costs O(n^2 * m) to verify
        // (GraphColoringVerifier's worst case is one color group holding all n nodes: O(n^2) pairs x O(m)
        // edge lookup each). numColors is capped at nodes.Count (see the K > nodes.Count guard in solve()),
        // so worst case is Theta((n+1)^n) -- asymptotically worse than n!, i.e. worse than the existing
        // SolverComplexityBucket.Factorial tier; Exponential is kept only because no stronger bucket exists.
        public string complexity { get; } = "O((numColors+1)^n * n^2 * m), n = |nodes|, m = |edges|, numColors = min(K, n)";

        // --- Methods Including Constructors ---
        public GraphColoringBruteForce() {

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
                        }
                        else if (binary[i] == K) {
                                binary[i] = 0;
                        }
                }
        }


        public string solve(GRAPHCOLORING gColor) {

                int numColors = gColor.K;
                if (gColor.K > gColor.nodes.Count) numColors = gColor.nodes.Count();


                List<int> binary = new List<int>();
                foreach (var i in gColor.nodes) {
                        binary.Add(0);
                }

                while (binary.Count(n => n == (numColors - 1)) < gColor.nodes.Count) {
                        string certificate = BinaryToCertificate(binary, gColor.nodes, numColors);
                        if (gColor.defaultVerifier.verify(gColor, certificate)) {
                                return certificate;
                        }
                        nextBinary(binary, numColors);

                }

                return "{}";
        }
}
