using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_STEINERTREE.Solvers;

// The steps Steiner Tree Brute Force records: one per candidate edge set, in the order it tries them (every combination of
// each size, from one fewer than the terminals up to K or the edge count, whichever is smaller). A candidate is a Reject
// naming the terminals it doesn't reach (in focus), or saying it is in more than one piece, or the Accept that ends the
// search; then the Done. The recorder's cap hides the rest of a long run.
class SteinerTreeBruteForce : ISolver<STEINERTREE, EdgeSet> {

    // --- Fields ---
    public string solverName { get; } = "Steiner Tree Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for the NP-Complete Steiner Tree problem";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // The outer loop sums C(m, i) (m = |edges|) over i from |terminals|-1 to min(K, m) -- a partial sum of
    // binomial coefficients of m, which is bounded above by the sum of ALL of them, 2^m, regardless of
    // where K falls (and reaches that order whenever K is near m/2). So this enumerates edge subsets,
    // not node subsets or permutations. Each candidate costs O(i) to build (indexListToCertificate) and
    // SteinerTreeVerifier's IsConnected/terminal-coverage check costs O(i*t) (t = |terminals|, from the
    // per-edge-endpoint List.Contains/Remove scans against the terminals list), i <= m.
    public MathematicalFunction timeComplexity { get; } = new("2^m * m * t", ", m = |edges|, t = |terminals|");

    // --- Methods Including Constructors ---
    public SteinerTreeBruteForce() {

    }

    // Moves combination to the next combination of its size from 0..size-1, in lexicographic order. Returns false,
    // leaving it unchanged, when it is already the last one (an empty combination has no next one).
    private bool nextComb(List<int> combination, int size) {
        for (int i = combination.Count - 1; i >= 0; i--) {
            if (combination[i] + 1 <= (i + size - combination.Count)) {
                combination[i] += 1;
                for (int j = i + 1; j < combination.Count; j++) {
                    combination[j] = combination[j - 1] + 1;
                }
                return true;
            }
        }
        return false;
    }

    private string indexListToCertificate(List<int> binary, List<KeyValuePair<string, string>> edges) {
        string certificate = "";
        foreach (int i in binary) {
            certificate += "{" + edges[i].Key + ',' + edges[i].Value + "},";
        }
        return "{" + certificate.TrimEnd(',') + "}";
    }

    // The terminals no edge of the candidate touches, in the instance's order: the first thing the verifier checks.
    private static string[] Unreached(STEINERTREE steiner, IEnumerable<KeyValuePair<string, string>> candidate) {
        var touched = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in candidate) {
            touched.Add(e.Key);
            touched.Add(e.Value);
        }
        return steiner.terminals.Where(t => !touched.Contains(t)).ToArray();
    }

    // How the steps show a candidate: the indices of the edges it picks, and for a Reject the terminals it leaves out
    // (null: the step is not a Reject).
    private static RunSteps<(List<int> Combination, string[]? Unreached), EdgeSet> NewSteps(StepRecorder<EdgeSet> rec,
        List<KeyValuePair<string, string>> edges) => new(rec,
        // What every picture shows: the candidate's edges, and what the step is about: every edge of a candidate it
        // accepts, the terminals left out of one it rejects (none when it is in more than one piece).
        partial: s => new EdgeSet(s.Combination.Select(c => GraphSubsetFrames.EdgeId(edges[c].Key, edges[c].Value))),
        focus: s => s.Unreached ?? s.Combination.Select(c => GraphSubsetFrames.EdgeId(edges[c].Key, edges[c].Value)).ToArray());

    public string solve(STEINERTREE steiner) => Solve(steiner, StepRecorder<EdgeSet>.Off);

    public string Solve(STEINERTREE steiner, StepRecorder<EdgeSet> rec) {
        var steps = NewSteps(rec, steiner.edges);
        int tried = 0;
        List<int> combination = new List<int>();

        // Every combination of i edges, for i from one fewer than the terminals up to K (no more than the edges there are).
        int largest = Math.Min(steiner.K, steiner.edges.Count);
        for (int i = Math.Max(steiner.terminals.Count - 1, 0); i <= largest; i++) {
            combination = new List<int>();
            for (int j = 0; j < i; j++) {
                combination.Add(j);
            }

            do {
                if (timerHasExpired) return SolveRun.TimedOutAnswer;
                string certificate = indexListToCertificate(combination, steiner.edges);
                tried++;
                if (steiner.defaultVerifier.verify(steiner, certificate)) {
                    string[] found = combination.Select(c => GraphSubsetFrames.EdgeId(steiner.edges[c].Key, steiner.edges[c].Value)).ToArray();
                    steps.Accept((combination, null), $"Try edges {string.Join(", ", found)}. They connect every terminal.");
                    steps.Done(new EdgeSet(found), true, $"{found.Length} edges connect every terminal, found after {tried} tries.");
                    return certificate;
                }
                // The verifier only says no. In its order: a terminal left out fails first, else the edges are in more than
                // one piece. The terminals left out are looked up only while recording.
                string[] unreached = rec.Enabled ? Unreached(steiner, combination.Select(c => steiner.edges[c])) : [];
                if (unreached.Length == 1)
                    steps.Reject((combination, unreached),
                        $"Try edges {string.Join(", ", combination.Select(c => GraphSubsetFrames.EdgeId(steiner.edges[c].Key, steiner.edges[c].Value)))}: terminal {unreached[0]} isn't reached.");
                else if (unreached.Length > 1)
                    steps.Reject((combination, unreached),
                        $"Try edges {string.Join(", ", combination.Select(c => GraphSubsetFrames.EdgeId(steiner.edges[c].Key, steiner.edges[c].Value)))}: terminals {string.Join(", ", unreached)} aren't reached.");
                else
                    steps.Reject((combination, unreached),
                        $"Try edges {string.Join(", ", combination.Select(c => GraphSubsetFrames.EdgeId(steiner.edges[c].Key, steiner.edges[c].Value)))}: the edges form more than one piece.");
            } while (nextComb(combination, steiner.edges.Count));

        }
        steps.Done(new EdgeSet([]), false, $"Checked {tried} candidate edge sets. None of them connects every terminal using at most {steiner.K} edges.");
        return "{}";
    }

}
