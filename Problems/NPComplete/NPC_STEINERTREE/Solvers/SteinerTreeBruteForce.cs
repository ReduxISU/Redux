using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_STEINERTREE.Solvers;

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
    // The outer loop sums C(m, i+1) (m = |edges|) over i from |terminals|-1 to K -- a partial sum of
    // binomial coefficients of m, which is bounded above by the sum of ALL of them, 2^m, regardless of
    // where K falls (and reaches that order whenever K is near m/2). So this enumerates edge subsets,
    // not node subsets or permutations. Each candidate costs O(i) to build (indexListToCertificate) and
    // SteinerTreeVerifier's IsConnected/terminal-coverage check costs O(i*t) (t = |terminals|, from the
    // per-edge-endpoint List.Contains/Remove scans against the terminals list), i <= m.
    public string complexity { get; } = "O(2^m * m * t), m = |edges|, t = |terminals|";

    // --- Methods Including Constructors ---
    public SteinerTreeBruteForce() {

    }

    private long factorial(long x) {
        long y = 1;
        for (long i = 1; i <= x; i++) {
            y *= i;
        }
        return y;
    }

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

    private string indexListToCertificate(List<int> binary, List<KeyValuePair<string, string>> edges) {
        string certificate = "";
        foreach (int i in binary) {
            certificate += "{" + edges[i].Key + ',' + edges[i].Value + "},";
        }
        return "{" + certificate.TrimEnd(',') + "}";
    }

    // Why a candidate edge set is not a Steiner tree, in the verifier's own order: a terminal left out, then more than one piece.
    private static (string Reason, string[] Focus) Why(STEINERTREE steiner, IEnumerable<KeyValuePair<string, string>> candidate) {
        var touched = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in candidate) {
            touched.Add(e.Key);
            touched.Add(e.Value);
        }
        var missing = steiner.terminals.Where(t => !touched.Contains(t)).ToArray();
        if (missing.Length > 0)
            return (missing.Length == 1 ? $"terminal {missing[0]} isn't reached" : $"terminals {string.Join(", ", missing)} aren't reached", missing);
        return ("the edges form more than one piece", []);
    }

    public string solve(STEINERTREE steiner) => Solve(steiner, StepRecorder<EdgeSet>.Off);

    // Steps: one per candidate edge set tried, in the order the solver tries them (sizes from one fewer than the
    // terminals up to K). Recorder state lives only in this call.
    public string Solve(STEINERTREE steiner, StepRecorder<EdgeSet> rec) {
        int tried = 0;
        List<int> combination = new List<int>();
        string[] Candidate() => combination.Select(c => GraphSubsetFrames.EdgeId(steiner.edges[c].Key, steiner.edges[c].Value)).ToArray();

        for (int i = steiner.terminals.Count - 1; i <= steiner.K; i++) {
            combination = new List<int>();
            for (int j = 0; j < i; j++) {
                combination.Add(j);
            }

            long reps = factorial(steiner.edges.Count) / (factorial(i + 1) * factorial(steiner.edges.Count - i - 1));
            for (int k = 0; k < reps; k++) {
                string certificate = indexListToCertificate(combination, steiner.edges);
                tried++;
                if (steiner.defaultVerifier.verify(steiner, certificate)) {
                    rec.Accept(() => new EdgeSet(Candidate()), () => $"Try edges {string.Join(", ", Candidate())}. They connect every terminal.", Candidate);
                    rec.Done(new EdgeSet(Candidate()), true, $"{Candidate().Length} edges connect every terminal, found after {tried} tries.");
                    return certificate;
                }
                rec.Reject(() => new EdgeSet(Candidate()),
                    () => $"Try edges {string.Join(", ", Candidate())}: {Why(steiner, combination.Select(c => steiner.edges[c])).Reason}.",
                    () => Why(steiner, combination.Select(c => steiner.edges[c])).Focus);
                combination = nextComb(combination, steiner.edges.Count);

            }

        }
        rec.Done(new EdgeSet([]), false, $"Checked {tried} candidate edge sets. None of them connects every terminal using at most {steiner.K} edges.");
        return "{}";
    }

}
