using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using System.Numerics;
using System.Diagnostics;

namespace API.Problems.NPComplete.NPC_CLIQUE.Solvers;

class CliqueBruteForce : ISolver<CLIQUE> {

    // --- Fields ---
    public string solverName { get; } = "Clique Brute Force Solver";
    public string solverDefinition { get; } = "This is a brute force solver for the NP-Complete Clique problem";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Caleb Eardley", "Kaden Marchetti" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Enumerates all C(n,K) size-K node combinations (worst case
    // Theta(2^n) at K ~ n/2 via nextComb); each candidate costs O(K^2 * m) to verify
    // (CliqueVerifier checks every pair in the K-set against the edge list).
    public string complexity { get; } = "O(2^n * n^2 * m), n = |nodes|, m = |edges|";

    // --- Methods Including Constructors ---
    public CliqueBruteForce() {

    }
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
    public string solve(CLIQUE clique) {
        // K==0 asks for the empty clique, which is trivially correct. The
        // verifier legitimately rejects "{}" as a malformed/empty certificate
        // for the general case, so that round-trip is skipped here rather than
        // weakening the verifier's guard.
        if (clique.K == 0) {
            return "{}";
        }
        List<int> combination = new List<int>();
        for (int i = 0; i < clique.K; i++) {
            combination.Add(i);
        }
        BigInteger reps = factorial(clique.nodes.Count) / (factorial(clique.K) * factorial(clique.nodes.Count - clique.K));
        for (int i = 0; i < reps; i++) {
            string certificate = indexListToCertificate(combination, clique.nodes);
            if (clique.defaultVerifier.verify(clique, certificate)) {
                return certificate;
            }
            combination = nextComb(combination, clique.nodes.Count);

        }
        return "{}";
    }

    public List<string> getSteps(CLIQUE clique) {
        List<string> steps = new List<string>();
        // K==0: same trivial empty-clique case as solve(). The loop below would
        // hand "{}" to the verifier on its first (only) iteration and it would
        // throw, so short-circuit here instead.
        if (clique.K == 0) {
            return steps;
        }
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
