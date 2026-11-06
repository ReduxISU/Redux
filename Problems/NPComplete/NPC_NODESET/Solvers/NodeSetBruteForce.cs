using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using System.Numerics;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_NODESET.Visualizations;

namespace API.Problems.NPComplete.NPC_NODESET.Solvers;

// The steps Node Set Brute Force records: one per set of nodes it tries removing, removing nothing first, then every set
// of 1 node, 2 nodes, ... up to K. A set is a Reject naming a cycle that survives its removal, or the Accept that ends the
// search; then the Done. The recorder's cap hides the rest of a long run.
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
    public MathematicalFunction timeComplexity { get; } = new("2^n * n^2 * m", ", n = |nodes|, m = |edges|");

    public NodeSetBruteForce() {

    }
    // BigInteger, not long: 21! no longer fits in a long, and an overflowed count skipped every combination.
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
    // How the steps show a candidate: the indices of the nodes it removes.
    private static RunSteps<List<int>, NodeSet> NewSteps(StepRecorder<NodeSet> rec, List<string> nodes) => new(rec,
        // What every picture shows: the nodes removed, every one of them in focus.
        partial: combination => new NodeSet(combination.Select(i => nodes[i])),
        focus: combination => combination.Select(i => LayeredFrames.NodeId(nodes[i])).ToArray());

    public string solve(NODESET nodeSet) => Solve(nodeSet, StepRecorder<NodeSet>.Off);

    // Tries removing nothing, then every set of 1 node, 2 nodes, ... up to K, until what is left has no cycle.
    public string Solve(NODESET nodeSet, StepRecorder<NodeSet> rec) {
        var steps = NewSteps(rec, nodeSet.nodes);
        // The verifier only says no, so a cycle that survives is found for the steps alone (a plain solve builds no finder).
        var cycle = rec.Enabled ? LayeredFrames.SurvivingCycle(NodeSetDefaultVisualization.Spec(nodeSet)) : null;
        int tried = 0;

        // Removing nothing comes first: an acyclic graph needs no node removed.
        tried++;
        List<int> nothing = [];
        if (nodeSet.defaultVerifier.verify(nodeSet, "{}")) {
            steps.Accept(nothing, $"Try removing nothing. No cycle is left.");
            steps.Done(new NodeSet([]), true, $"The graph has no cycle, so removing nothing is enough. Found after 1 try.");
            return "{}";
        }
        steps.Reject(nothing, $"Try removing nothing: the cycle {cycle!([], [])} is there.");
        for (int i = 0; i < nodeSet.K; i++) {
            List<int> combination = new List<int>();
            for (int j = 0; j <= i; j++) {
                combination.Add(j);
            }
            BigInteger reps = factorial(nodeSet.nodes.Count) / (factorial(i + 1) * factorial(nodeSet.nodes.Count - i - 1));
            for (BigInteger k = 0; k < reps; k++) {
                if (timerHasExpired) return SolveRun.TimedOutAnswer;
                string certificate = indexListToCertificate(combination, nodeSet.nodes);
                tried++;
                if (nodeSet.defaultVerifier.verify(nodeSet, certificate)) {
                    steps.Accept(combination, $"Try removing {certificate}. No cycle is left.");
                    steps.Done(new NodeSet(combination.Select(c => nodeSet.nodes[c])), true,
                        $"Removing {certificate} leaves no cycle, within K = {nodeSet.K}. Found after {Words.Count(tried, "try", "tries")}.");
                    return certificate;
                }
                steps.Reject(combination, $"Try removing {certificate}: the cycle {cycle!([], combination.Select(c => nodeSet.nodes[c]))} is still there.");
                combination = nextComb(combination, nodeSet.nodes.Count);

            }
        }
        // Removing nothing failed above, and so did every set of 1 to K nodes.
        steps.Done(new NodeSet([]), false,
            $"Checked every way to remove up to K = {Words.Count(nodeSet.K, "node")} ({Words.Count(tried, "try", "tries")}). A cycle always survives.");
        return "{}";
    }
}


