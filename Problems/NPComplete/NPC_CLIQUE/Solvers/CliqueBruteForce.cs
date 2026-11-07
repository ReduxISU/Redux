using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using System.Numerics;
using System.Diagnostics;
using static API.Interfaces.Graphs.GraphSubsetFrames;

namespace API.Problems.NPComplete.NPC_CLIQUE.Solvers;

// The steps Clique Brute Force records: one per candidate set of K nodes, in the order it tries them. A candidate is a
// Reject naming two of its nodes that aren't joined, or the Accept that ends the search; then the Done. The recorder's
// cap hides the rest of a long run.
class CliqueBruteForce : ISolver<CLIQUE, NodeSet> {

    // --- Fields ---
    public string solverName { get; } = "Clique Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for the NP-Complete Clique problem";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Caleb Eardley", "Kaden Marchetti" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Enumerates all C(n,K) size-K node combinations (worst case
    // Theta(2^n) at K ~ n/2 via nextComb); each candidate costs O(K^2 * m) to verify
    // (CliqueVerifier checks every pair in the K-set against the edge list).
    public MathematicalFunction timeComplexity { get; } = new("2^n * n^2 * m", ", n = |nodes|, m = |edges|");

    // --- Methods Including Constructors ---
    public CliqueBruteForce() {

    }
    // How the steps show a candidate: the indices of the nodes it picks.
    private static RunSteps<List<int>, NodeSet> NewSteps(StepRecorder<NodeSet> rec, List<string> nodes) => new(rec,
        // What every picture shows: the candidate's nodes, every one of them in focus.
        partial: combination => new NodeSet(combination.Select(i => nodes[i])),
        focus: combination => combination.Select(i => nodes[i]).ToArray());

    private BigInteger factorial(BigInteger x) {
        BigInteger y = 1;
        for (BigInteger i = 1; i <= x; i++) {
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
    public string solve(CLIQUE clique) => Solve(clique, StepRecorder<NodeSet>.Off);

    // Tries every set of K nodes in turn, the first K node indices first, until every pair in one is joined.
    public string Solve(CLIQUE clique, StepRecorder<NodeSet> rec) {
        var steps = NewSteps(rec, clique.nodes);
        // K==0 asks for the empty clique: the loop below tries the one size-0 candidate, "{}", and the
        // verifier accepts it.
        List<int> combination = new List<int>();
        for (int i = 0; i < clique.K; i++) {
            combination.Add(i);
        }
        BigInteger reps = factorial(clique.nodes.Count) / (factorial(clique.K) * factorial(clique.nodes.Count - clique.K));
        int tried = 0;
        for (int i = 0; i < reps; i++) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            string certificate = indexListToCertificate(combination, clique.nodes);
            tried++;
            if (clique.defaultVerifier.verify(clique, certificate)) {
                string[] found = combination.Select(c => clique.nodes[c]).ToArray();
                steps.Accept(combination, $"Try {Words.Braces(found)}. Every pair is joined.");
                steps.Done(new NodeSet(found), true, $"{Words.Braces(found)} is a clique, found after {tried} tries.");
                return certificate;
            }
            // The verifier only says no, so the pair that isn't joined is looked up for a kept step.
            steps.Reject(combination,
                $"Try {Words.Braces(combination.Select(c => clique.nodes[c]))}: {Why(SubsetRule.Clique, clique.nodes, clique.edges, combination.Select(c => clique.nodes[c]))}.");
            combination = nextComb(combination, clique.nodes.Count);

        }
        steps.Done(new NodeSet([]), false, $"Checked all {tried} candidate sets. None is a clique of size {clique.K}.");
        return "{}";
    }

    public List<string> getSteps(CLIQUE clique) {
        List<string> steps = new List<string>();
        List<int> combination = new List<int>();
        for (int i = 0; i < clique.K; i++) {
            combination.Add(i);
        }
        BigInteger reps = factorial(clique.nodes.Count) / (factorial(clique.K) * factorial(clique.nodes.Count - clique.K));
        for (int i = 0; i < reps; i++) {
            string certificate = indexListToCertificate(combination, clique.nodes);

            if (clique.defaultVerifier.verify(clique, certificate) || steps.Count == 99) {
                return steps;
            }
            if (steps.Count < 99) steps.Add(certificate);
            combination = nextComb(combination, clique.nodes.Count);

        }
        steps.Add("{}");
        return steps;
    }

    /// <summary>
    /// Given Clique instance in string format and solution string, outputs a solution dictionary with 
    /// true values mapped to nodes that are in the solution set else false. 
    /// </summary>
    /// <param name="problemInstance"></param>
    /// <param name="solutionString"></param>
    /// <returns></returns>
    public Dictionary<string, bool> getSolutionDict(string problemInstance, string solutionString) {

        Dictionary<string, bool> solutionDict = new Dictionary<string, bool>();
        GraphParser gParser = new GraphParser();
        CLIQUE clique = new CLIQUE(problemInstance);
        List<string> problemInstanceNodes = clique.nodes;
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
