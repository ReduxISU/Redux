using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Interfaces.Graphs.GraphParser;
using System.Linq;
using System.Numerics;


namespace API.Problems.NPComplete.NPC_VERTEXCOVER.Solvers;

class VertexCoverBruteForce : ISolver<VERTEXCOVER, NodeSet> {

    // --- Fields ---
    public string solverName { get; } = "Vertex Cover Brute Force";
    public string solverDefinition { get; } = "This solver simply tests combinations of nodes of size k until a solution is found, or all combinations are tested.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Caleb Eardley" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Enumerates all C(n,K) size-K node combinations (worst case
    // Theta(2^n) at K ~ n/2 via nextComb); each candidate costs O(K*n) to check certificate
    // nodes belong to G plus O(K*m) to check every edge is incident to the set (VCVerifier),
    // i.e. O(K*(n+m)) per candidate.
    public string complexity { get; } = "O(2^n * n * (n + m)), n = |nodes|, m = |edges|";

    // --- Methods Including Constructors ---
    public VertexCoverBruteForce() {

    }



    private BigInteger factorial(BigInteger x) {
        BigInteger y = 1;
        for (BigInteger i = 1; i <= x; i++) {
            y *= i;
        }
        return y;
    }
    private string indexListToCertificate(List<int> indecies, List<string> nodes) {
        return "{" + string.Join(",", indecies.Select(i => nodes[i])) + "}";
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
    /// <summary>
    /// Solves a VERTEXCOVER instance input.
    /// </summary>
    /// <param name="G"> G is an undirected graph instance string</param>
    /// <returns>
    ///  Subset of nodes that cover whole graph. 
    /// </returns>
    public string solve(VERTEXCOVER G) => Solve(G, StepRecorder<NodeSet>.Off);

    // Steps: one per candidate set of size K. Recorder state lives only in this call.
    public string Solve(VERTEXCOVER G, StepRecorder<NodeSet> rec) {
        if (G.K == 0) {
            rec.Done(new NodeSet([]), G.edges.Count == 0,
                G.edges.Count == 0 ? "There are no edges, so the empty set covers everything." : "A cover of size 0 cannot cover any edge.");
            return "{}";
        }
        List<int> combination = new List<int>();
        for (int i = 0; i < G.K; i++) {
            combination.Add(i);
        }
        BigInteger reps = factorial(G.nodes.Count) / (factorial(G.K) * factorial(G.nodes.Count - G.K));
        int tried = 0;
        string[] Candidate() => combination.Select(i => G.nodes[i]).ToArray();
        for (int i = 0; i < reps; i++) {
            string certificate = indexListToCertificate(combination, G.nodes);
            tried++;
            if (G.defaultVerifier.verify(G, certificate)) {
                rec.Accept(() => new NodeSet(Candidate()), () => $"Try {GraphSubsetFrames.Braces(Candidate())}. It covers every edge.", Candidate);
                rec.Done(new NodeSet(Candidate()), true, $"{GraphSubsetFrames.Braces(Candidate())} covers every edge, found after {tried} tries.");
                return certificate;
            }
            rec.Reject(() => new NodeSet(Candidate()),
                () => $"Try {GraphSubsetFrames.Braces(Candidate())}: {GraphSubsetFrames.Why(SubsetRule.VertexCover, G.nodes, G.edges, Candidate())}.",
                Candidate);
            combination = nextComb(combination, G.nodes.Count);

        }
        rec.Done(new NodeSet([]), false, $"Checked all {tried} sets of {G.K} nodes. None covers every edge.");
        return "{}";
    }


    public Dictionary<string, bool> getSolutionDict(string problemInstance, string solutionString) {
        Dictionary<string, bool> solutionDict = new Dictionary<string, bool>();
        // GraphParser gParser = new GraphParser();
        VERTEXCOVER vertexcover = new VERTEXCOVER(problemInstance);
        List<string> problemInstanceNodes = vertexcover.nodes;
        // List<string> solvedNodes = gParser.getNodesFromNodeListString(solutionString);
        List<string> solvedNodes = GraphParser.parseNodeListWithStringFunctions(solutionString);

        // Remove solvedNodes from instanceNodes
        foreach (string node in solvedNodes) {
            problemInstanceNodes.Remove(node);
            //  Console.WriteLine("Solved nodes: "+node);
            solutionDict.Add(node, true);
        }
        // Add solved nodes to dict as {name, true}
        // Add remaining instance nodes as {name, false}

        foreach (string node in problemInstanceNodes) {

            solutionDict.Add(node, false);
        }

        return solutionDict;
    }

}
