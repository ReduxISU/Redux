using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using static API.Interfaces.Graphs.GraphSubsetFrames;

namespace API.Problems.NPComplete.NPC_INDEPENDENTSET.Solvers;

// The steps Independent Set Brute Force records: one per candidate set of K nodes, in the order it tries them. A
// candidate is a Reject naming two of its nodes that are joined, or the Accept that ends the search; then the Done. The
// recorder's cap hides the rest of a long run.
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
    public MathematicalFunction timeComplexity { get; } = new("2^n * n^2 * m", ", n = |nodes|, m = |edges|");

    // --- Methods Including Constructors ---
    public IndependentSetBruteForce() {

    }
    private long factorial(long x) {
        long y = 1;
        for (long i = 1; i <= x; i++) {
            y *= i;
        }
        return y;
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
    // How the steps show a candidate: the indices of the nodes it picks.
    private static RunSteps<List<int>, NodeSet> NewSteps(StepRecorder<NodeSet> rec, List<string> nodes) => new(rec,
        // What every picture shows: the candidate's nodes, every one of them in focus.
        partial: combination => new NodeSet(combination.Select(i => nodes[i])),
        focus: combination => combination.Select(i => nodes[i]).ToArray());

    public string solve(INDEPENDENTSET independentSet) => Solve(independentSet, StepRecorder<NodeSet>.Off);

    // Tries every set of K nodes in turn, the first K node indices first, until one has no two nodes joined.
    public string Solve(INDEPENDENTSET independentSet, StepRecorder<NodeSet> rec) {
        var steps = NewSteps(rec, independentSet.nodes);
        List<int> combination = new List<int>();
        for (int i = 0; i < independentSet.K; i++) {
            combination.Add(i);
        }
        long reps = factorial(independentSet.nodes.Count) / (factorial(independentSet.K) * factorial(independentSet.nodes.Count - independentSet.K));
        int tried = 0;
        for (int i = 0; i < reps; i++) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            string certificate = indexListToCertificate(combination, independentSet.nodes);
            tried++;
            if (independentSet.defaultVerifier.verify(independentSet, certificate)) {
                string[] found = combination.Select(c => independentSet.nodes[c]).ToArray();
                steps.Accept(combination, $"Try {Braces(found)}. No two are joined.");
                steps.Done(new NodeSet(found), true, $"{Braces(found)} is an independent set of size {independentSet.K}, found after {tried} tries.");
                return certificate;
            }
            // The verifier only says no, so the joined pair is looked up for a kept step.
            steps.Reject(combination,
                $"Try {Braces(combination.Select(c => independentSet.nodes[c]))}: {Why(SubsetRule.IndependentSet, independentSet.nodes, independentSet.edges, combination.Select(c => independentSet.nodes[c]))}.");
            combination = nextComb(combination, independentSet.nodes.Count);

        }
        steps.Done(new NodeSet([]), false, $"Checked all {tried} sets of {independentSet.K} nodes. None is independent.");
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
