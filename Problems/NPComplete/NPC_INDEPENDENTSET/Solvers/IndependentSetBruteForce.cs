using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_INDEPENDENTSET.Solvers;

class IndependentSetBruteForce : ISolver<INDEPENDENTSET, NodeSet> {

    // --- Fields ---
    public string solverName { get; } = "Independent Set Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for the NP-Complete Independent Set problem";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Russell Phillips" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Enumerates all C(n,K) size-K node combinations (worst case
    // Theta(2^n) at K ~ n/2 via nextComb); each candidate costs O(K^2 * m) to verify
    // (IndependentSetVerifier checks every pair in the K-set against the edge list).
    // Written with the K bound, so the Big-O precheck (#843) does not refuse a small K for what a large K costs.
    public MathematicalFunction timeComplexity { get; } = new("C(|N|, K) * K^2 * (|E| + 1)", ", |N| = number of nodes, |E| = number of edges, K = independent-set size");

    // --- Methods Including Constructors ---
    public IndependentSetBruteForce() {

    }
    // The number of ways to choose k of n, as n/1 * (n-1)/2 * ...: each partial product is itself a binomial, so it divides
    // exactly. Written so in place of n! / (k! (n-k)!), whose factorials overflow a long past 20 items and gave a wrong count.
    private static long Binomial(long n, long k) {
        long r = 1;
        for (long j = 1; j <= k; j++) r = checked(r * (n - k + j)) / j;
        return r;
    }
    private string indexListToCertificate(List<int> indecies, List<string> nodes) {
        string certificate = "";
        foreach (int i in indecies) {
            certificate += nodes[i] + ",";
        }
        certificate = certificate.TrimEnd(',');
        return "{" + certificate + "}";
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
    public string solve(INDEPENDENTSET independentSet) => Solve(independentSet, StepRecorder<NodeSet>.Off);

    // Steps: one per candidate set of size K. Recorder state lives only in this call.
    public string Solve(INDEPENDENTSET independentSet, StepRecorder<NodeSet> rec) {
        List<int> combination = new List<int>();
        for (int i = 0; i < independentSet.K; i++) {
            combination.Add(i);
        }
        long reps = Binomial(independentSet.nodes.Count, independentSet.K);
        int tried = 0;
        string[] Candidate() => combination.Select(i => independentSet.nodes[i]).ToArray();
        for (int i = 0; i < reps; i++) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // #935: stop at the time limit
            string certificate = indexListToCertificate(combination, independentSet.nodes);
            tried++;
            if (independentSet.defaultVerifier.verify(independentSet, certificate)) {
                rec.Accept(() => new NodeSet(Candidate()), () => $"Try {GraphSubsetFrames.Braces(Candidate())}. No two are joined.", Candidate);
                rec.Done(new NodeSet(Candidate()), true,
                    $"{GraphSubsetFrames.Braces(Candidate())} is an independent set of size {independentSet.K}, found after {tried} tries.");
                return certificate;
            }
            rec.Reject(() => new NodeSet(Candidate()),
                () => $"Try {GraphSubsetFrames.Braces(Candidate())}: {GraphSubsetFrames.Why(SubsetRule.IndependentSet, independentSet.nodes, independentSet.edges, Candidate())}.",
                Candidate);
            combination = nextComb(combination, independentSet.nodes.Count);

        }
        rec.Done(new NodeSet([]), false, $"Checked all {tried} sets of {independentSet.K} nodes. None is independent.");
        return "{}";
    }

    public Dictionary<string, bool> getSolutionDict(string problemInstance, string solutionString) {

        Dictionary<string, bool> solutionDict = new Dictionary<string, bool>();
        GraphParser gParser = new GraphParser();
        INDEPENDENTSET independentset = new INDEPENDENTSET(problemInstance);
        List<string> problemInstanceNodes = independentset.nodes;
        List<string> solvedNodes = gParser.getNodesFromNodeListString(solutionString);

        // Remove solvedNodes from instanceNodes
        foreach (string node in solvedNodes) {
            problemInstanceNodes.Remove(node);
            solutionDict.Add(node, true);
        }
        foreach (string node in problemInstanceNodes) {
            solutionDict.Add(node, false);
        }
        return solutionDict;
    }
}
