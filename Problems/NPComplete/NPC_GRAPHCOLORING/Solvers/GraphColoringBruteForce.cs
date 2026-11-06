using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers;

// The steps Graph Coloring Brute Force records: one per candidate coloring, in the order the odometer tries them. A
// candidate with two neighbours in one color (or a node with no color) is a Reject naming what fails it; the first
// coloring is the Accept that ends the search; then the Done. The recorder's cap hides the rest of a long run.
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
    public MathematicalFunction timeComplexity { get; } = new("(numColors+1)^n * n^2 * m", ", n = |nodes|, m = |edges|, numColors = min(K, n)");

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


    // How the steps show a candidate, given by its odometer digits: the coloring they make, and the nodes and edge that
    // show why it is not a coloring. The Accept says its own focus, none.
    private static RunSteps<List<int>, Groups> NewSteps(GRAPHCOLORING gColor, int numColors, StepRecorder<Groups> rec) => new(rec,
        // What every picture shows: the coloring, and what fails it.
        partial: binary => new Groups(Candidate(gColor.nodes, binary, numColors)),
        focus: binary => GraphGroupFrames.Why(GroupRule.Coloring, gColor.nodes, gColor.edges, Candidate(gColor.nodes, binary, numColors))?.Focus ?? []);

    // The coloring the digits make: node i gets color binary[i]. A node whose digit is numColors (the odometer's extra
    // value) has no color, so it is left out.
    private static Dictionary<string, int> Candidate(List<string> nodes, List<int> binary, int numColors) {
        var colors = new Dictionary<string, int>();
        for (int i = 0; i < binary.Count; i++)
            if (binary[i] < numColors) colors[nodes[i]] = binary[i];
        return colors;
    }

    public string solve(GRAPHCOLORING gColor) => Solve(gColor, StepRecorder<Groups>.Off);

    // Counts through every way to give each node a color number (an odometer, one digit per node) until one is a coloring.
    public string Solve(GRAPHCOLORING gColor, StepRecorder<Groups> rec) {

        int numColors = gColor.K;
        if (gColor.K > gColor.nodes.Count) numColors = gColor.nodes.Count();
        var steps = NewSteps(gColor, numColors, rec);
        // No colors to give: nextBinary could never carry, so stop here. Only a graph with no nodes is colored, by "{}".
        if (numColors <= 0) {
            bool ok = false;
            try { ok = gColor.defaultVerifier.verify(gColor, "{}"); } catch { }
            if (gColor.nodes.Count == 0)
                steps.Done(new Groups([]), ok, $"The graph has no nodes, so there is nothing to color.");
            else
                steps.Done(new Groups([]), ok, $"K = {gColor.K} allows no colors, so the {Words.Count(gColor.nodes.Count, "node")} can't be colored.");
            return "{}";
        }


        List<int> binary = new List<int>();
        foreach (var i in gColor.nodes) {
            binary.Add(0);
        }

        int tried = 0;

        // The verifier accepts a candidate only if every node has a color (no digit is numColors, the odometer's extra
        // value) and no edge joins two different nodes of the same color. Checking that on the digits first, and building
        // and verifying the certificate only for a candidate that passes, skips the string work for every other candidate
        // without changing which candidates are tried or which one is returned.
        var nodeIndex = new Dictionary<string, int>();
        for (int i = 0; i < gColor.nodes.Count; i++) nodeIndex.TryAdd(gColor.nodes[i], i);
        var clashes = gColor.edges
            .Where(e => !e.Key.Equals(e.Value) && nodeIndex.ContainsKey(e.Key) && nodeIndex.ContainsKey(e.Value))
            .Select(e => (nodeIndex[e.Key], nodeIndex[e.Value]))
            .ToArray();
        bool MightBeAColoring() {
            for (int i = 0; i < binary.Count; i++)
                if (binary[i] >= numColors) return false;
            foreach (var (u, v) in clashes)
                if (binary[u] == binary[v]) return false;
            return true;
        }

        // Use a do-while so the initial all-zero candidate is always evaluated at least
        // once. With a while-loop that checks the exit condition first, when
        // numColors == 1 the condition (all digits already equal numColors - 1, i.e. 0)
        // is true before the loop ever runs, so the only candidate that exists for K=1
        // (every node in color 0) was never tested. For numColors > 1 the condition is
        // guaranteed false on the initial all-zero binary (no digit can equal
        // numColors - 1 >= 1 yet), so this restructuring does not change behavior there.
        do {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // stop at the time limit
            tried++;
            if (MightBeAColoring()) {
                string certificate = BinaryToCertificate(binary, gColor.nodes, numColors);
                if (gColor.defaultVerifier.verify(gColor, certificate)) {
                    steps.Accept(binary, $"Try {GraphGroupFrames.Classes(Candidate(gColor.nodes, binary, numColors), numColors)}. No two neighbours share a color.", focus: () => []);
                    steps.Done(new Groups(Candidate(gColor.nodes, binary, numColors)), true,
                        $"{GraphGroupFrames.Classes(Candidate(gColor.nodes, binary, numColors), numColors)} is a coloring with at most {numColors} colors, found after {tried} tries.");
                    return certificate;
                }
            }
            // What fails the candidate is looked up for the step alone.
            steps.Reject(binary,
                $"Try {GraphGroupFrames.Classes(Candidate(gColor.nodes, binary, numColors), numColors)}: {GraphGroupFrames.Why(GroupRule.Coloring, gColor.nodes, gColor.edges, Candidate(gColor.nodes, binary, numColors))?.Reason ?? "the verifier rejects it"}.");
            nextBinary(binary, numColors);

        } while (binary.Count(n => n == (numColors - 1)) < gColor.nodes.Count);

        steps.Done(new Groups([]), false, $"Checked {tried} candidate colorings with {numColors} colors. None has every node colored with no clash.");
        return "{}";
    }
}