using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers;

class GraphColoringBruteForce : ISolver<GRAPHCOLORING, Groups> {

    // --- Fields ---
    public string solverName { get; } = "Graph Coloring Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for the NP-Complete Graph Coloring problem";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
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
            } else if (binary[i] == K) {
                binary[i] = 0;
            }
        }
    }


    public string solve(GRAPHCOLORING gColor) => Solve(gColor, StepRecorder<Groups>.Off);

    // Steps: one per candidate coloring tried, in the order the solver tries them. A node whose digit is numColors (the
    // odometer's extra value) has no color in that candidate, so it is left out of the partial answer. Recorder state
    // lives only in this call.
    public string Solve(GRAPHCOLORING gColor, StepRecorder<Groups> rec) {

        int numColors = gColor.K;
        if (gColor.K > gColor.nodes.Count) numColors = gColor.nodes.Count();
        // No colors to give: nextBinary could never carry, so stop here. Only a graph with no nodes is colored, by "{}".
        if (numColors <= 0) {
            bool ok = false;
            try { ok = gColor.defaultVerifier.verify(gColor, "{}"); } catch { }
            rec.Done(new Groups([]), ok, gColor.nodes.Count == 0
                ? "The graph has no nodes, so there is nothing to color."
                : $"K = {gColor.K} allows no colors, so the {gColor.nodes.Count} {(gColor.nodes.Count == 1 ? "node" : "nodes")} can't be colored.");
            return "{}";
        }


        List<int> binary = new List<int>();
        foreach (var i in gColor.nodes) {
            binary.Add(0);
        }

        int tried = 0;
        Dictionary<string, int> Candidate() {
            var colors = new Dictionary<string, int>();
            for (int i = 0; i < binary.Count; i++)
                if (binary[i] < numColors) colors[gColor.nodes[i]] = binary[i];
            return colors;
        }

        // Use a do-while so the initial all-zero candidate is always evaluated at least
        // once. With a while-loop that checks the exit condition first, when
        // numColors == 1 the condition (all digits already equal numColors - 1, i.e. 0)
        // is true before the loop ever runs, so the only candidate that exists for K=1
        // (every node in color 0) was never tested. For numColors > 1 the condition is
        // guaranteed false on the initial all-zero binary (no digit can equal
        // numColors - 1 >= 1 yet), so this restructuring does not change behavior there.
        do {
            string certificate = BinaryToCertificate(binary, gColor.nodes, numColors);
            tried++;
            if (gColor.defaultVerifier.verify(gColor, certificate)) {
                rec.Accept(() => new Groups(Candidate()), () => $"Try {GraphGroupFrames.Classes(Candidate(), numColors)}. No two neighbours share a color.", Array.Empty<string>());
                rec.Done(new Groups(Candidate()), true, $"{GraphGroupFrames.Classes(Candidate(), numColors)} is a coloring with at most {numColors} colors, found after {tried} tries.");
                return certificate;
            }
            rec.Reject(() => new Groups(Candidate()),
                () => $"Try {GraphGroupFrames.Classes(Candidate(), numColors)}: {GraphGroupFrames.Why(GroupRule.Coloring, gColor.nodes, gColor.edges, Candidate())?.Reason ?? "the verifier rejects it"}.",
                () => GraphGroupFrames.Why(GroupRule.Coloring, gColor.nodes, gColor.edges, Candidate())?.Focus ?? Array.Empty<string>());
            nextBinary(binary, numColors);

        } while (binary.Count(n => n == (numColors - 1)) < gColor.nodes.Count);

        rec.Done(new Groups([]), false, $"Checked {tried} candidate colorings with {numColors} colors. None has every node colored with no clash.");
        return "{}";
    }
}