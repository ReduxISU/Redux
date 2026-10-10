using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using System.Numerics;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_NODESET.Visualizations;

namespace API.Problems.NPComplete.NPC_NODESET.Solvers;

class NodeSetBruteForce : ISolver<NODESET, NodeSet> {

    // --- Fields ---
    public string solverName { get; } = "Node Set Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for the Node Set problem";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Enumerates subsets of size 1..K (K <= nodes.Count, but K can
    // still be Theta(n), so the sum of C(n,i) terms is Theta(2^n) in the worst case); each
    // candidate costs O(K*m) to build the removed-edge set (toEdges) plus O(n^2 * m) to
    // check acyclicity via the reachability fixpoint in isACyclical.
    public string complexity { get; } = "O(2^n * n^2 * m), n = |nodes|, m = |edges|";

    public NodeSetBruteForce() {

    }
    // BigInteger, not long: 21! no longer fits in a long, and an overflowed count skipped every combination (#783).
    private BigInteger factorial(BigInteger x) {
        BigInteger y = 1;
        for (BigInteger i = 1; i <= x; i++) {
            y *= i;
        }
        return y;
    }
    //Function below turns index list into certificate
    private string indexListToCertificate(List<int> indecies, List<string> nodes) {
        string certificate = "";
        foreach (int i in indecies) {
            certificate += nodes[i] + ",";
        }
        certificate = certificate.TrimEnd(',');
        return "{" + certificate + "}";
    }

    // helper function to go through possible combinations
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
    public string solve(NODESET nodeSet) => Solve(nodeSet, StepRecorder<NodeSet>.Off);

    public string Solve(NODESET nodeSet, StepRecorder<NodeSet> rec) {
        var cycle = LayeredFrames.SurvivingCycle(NodeSetDefaultVisualization.Spec(nodeSet));
        int tried = 0;

        // Removing nothing comes first: an acyclic graph needs no node removed (#789).
        tried++;
        if (nodeSet.defaultVerifier.verify(nodeSet, "{}")) {
            rec.Accept(() => new NodeSet([]), () => "Try removing nothing. No cycle is left.");
            rec.Done(new NodeSet([]), true, "The graph has no cycle, so removing nothing is enough. Found after 1 try.");
            return "{}";
        }
        rec.Reject(() => new NodeSet([]), () => $"Try removing nothing: the cycle {cycle([], [])} is there.");
        for (int i = 0; i < nodeSet.K; i++) {
            List<int> combination = new List<int>();
            for (int j = 0; j <= i; j++) {
                combination.Add(j);
            }
            BigInteger reps = factorial(nodeSet.nodes.Count) / (factorial(i + 1) * factorial(nodeSet.nodes.Count - i - 1));
            for (BigInteger k = 0; k < reps; k++) {
                string certificate = indexListToCertificate(combination, nodeSet.nodes);
                tried++;
                string[] Candidate() => combination.Select(c => nodeSet.nodes[c]).ToArray();
                string[] Ids() => Candidate().Select(LayeredFrames.NodeId).ToArray();
                if (nodeSet.defaultVerifier.verify(nodeSet, certificate)) {
                    rec.Accept(() => new NodeSet(Candidate()), () => $"Try removing {certificate}. No cycle is left.", Ids);
                    rec.Done(new NodeSet(Candidate()), true, $"Removing {certificate} leaves no cycle, within K = {nodeSet.K}. Found after {tried} {(tried == 1 ? "try" : "tries")}.");
                    return certificate;
                }
                rec.Reject(() => new NodeSet(Candidate()), () => $"Try removing {certificate}: the cycle {cycle([], Candidate())} is still there.", Ids);
                combination = nextComb(combination, nodeSet.nodes.Count);

            }
        }
        // Removing nothing failed above, and so did every set of 1 to K nodes.
        rec.Done(new NodeSet([]), false,
            $"Checked every way to remove up to K = {nodeSet.K} {(nodeSet.K == 1 ? "node" : "nodes")} ({tried} {(tried == 1 ? "try" : "tries")}). A cycle always survives.");
        return "{}";
    }
}


