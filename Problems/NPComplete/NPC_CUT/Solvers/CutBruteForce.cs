using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_CUT.Solvers;

// The steps Cut Brute Force records: one per candidate S side tried (sizes 1 to n/2), in the order the solver tries
// them. A side whose split cuts some other number of edges than K is a Reject saying how many cross; the first that cuts
// exactly K is the Accept that ends the search; then the Done. The recorder's cap hides the rest of a long run.
class CutBruteForce : ISolver<CUT, Groups> {

    // --- Fields ---
    public string solverName { get; } = "Cut Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for the Cut problem";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Enumerates the S sides of size 1..n/2 (every split of the
    // nodes, up to swapping the sides); each candidate costs O(n^2) to build
    // (certificateToEdges) plus O(m) to verify.
    public MathematicalFunction timeComplexity { get; } = new("2^n * n^2 * m", ", n = |nodes|, m = |edges|");

    public CutBruteForce() {

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

    private List<string> parseCertificate(string certificate) {

        List<string> nodeList = GraphParser.parseNodeListWithStringFunctions(certificate);
        return nodeList;
    }

    // Function below turns certificate into list of edges
    private string certificateToEdges(CUT cut, string certificate) {
        List<string> nodeList = parseCertificate(certificate);
        certificate = "{";
        foreach (var i in nodeList) {
            foreach (var j in cut.nodes) {
                if (!nodeList.Contains(j)) {
                    KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(i, j);
                    KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(j, i);
                    if ((cut.edges.Contains(pairCheck1) || cut.edges.Contains(pairCheck2)) && !i.Equals(j)) { // checks if is being cut
                        certificate += "{" + i + "," + j + "},"; // adds edge
                    }
                }
            }
        }
        certificate = certificate.TrimEnd(',');
        certificate += "}";
        return certificate;

    }

    // Moves combination to the next combination of its size from 0..size-1, in lexicographic order. Returns false,
    // leaving it unchanged, when it is already the last one.
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
    // How the steps show a candidate, given by the indexes of its S side's nodes: the split, S as group 0 and the rest as
    // group 1 (the edges that cross are the cut).
    private static RunSteps<List<int>, Groups> NewSteps(List<string> nodes, StepRecorder<Groups> rec) => new(rec,
        // What every picture shows: the split.
        partial: combination => GraphEdgeFrames.Split(nodes, Side(nodes, combination)));

    // The names of the S side's nodes.
    private static string[] Side(List<string> nodes, List<int> combination) => combination.Select(c => nodes[c]).ToArray();

    // The other side, written as the steps write a side.
    private static string Rest(List<string> nodes, List<int> combination) => GraphEdgeFrames.Side(nodes.Except(Side(nodes, combination)));

    public string solve(CUT cut) => Solve(cut, StepRecorder<Groups>.Off);

    // Tries every S side of 1 to n/2 nodes, in order of size, until the edges that cross number exactly K.
    public string Solve(CUT cut, StepRecorder<Groups> rec) {
        var steps = NewSteps(cut.nodes, rec);
        if (cut.K > cut.edges.Count) { // impossible to do cut if not enough edges
            steps.Done(new Groups([]), false, $"The graph has only {cut.edges.Count} edges, so no cut can have {cut.K}.");
            return "{}";
        }
        if (cut.K < 0) {
            steps.Done(new Groups([]), false, $"No cut has {cut.K} edges.");
            return "{}";
        }
        if (cut.K == 0) { // no side needs trying: "{}" is the only candidate for K = 0
            // Both sides of a cut hold a node, so "{}" is a cut only when the graph falls into separate pieces. The
            // verifier decides.
            if (cut.defaultVerifier.verify(cut, "{}"))
                steps.Done(new Groups([]), true, $"The graph falls into separate pieces, so splitting them apart cuts no edges.");
            else
                steps.Done(new Groups([]), false, $"Every split of the nodes into two sides cuts an edge, so no cut has 0 edges.");
            return "{}";
        }
        int tried = 0;
        List<int> combination = new List<int>();
        // Every S side of 1 to n/2 nodes. K bounds the edges that cross, not the size of a side, and a bigger S
        // side gives the same cut as its smaller other side, so this tries every split.
        for (int size = 1; size <= cut.nodes.Count / 2; size++) {
            combination = new List<int>();
            for (int j = 0; j < size; j++) {
                combination.Add(j);
            }
            do {
                if (timerHasExpired) return SolveRun.TimedOutAnswer; // stop at the time limit
                string certificate = indexListToCertificate(combination, cut.nodes);
                certificate = certificateToEdges(cut, certificate); // last two could be one function, but it is okay
                tried++;
                if (cut.defaultVerifier.verify(cut, certificate)) {
                    steps.Accept(combination, $"Put {GraphEdgeFrames.Side(Side(cut.nodes, combination))} against {Rest(cut.nodes, combination)}: exactly {cut.K} edges cross.");
                    steps.Done(GraphEdgeFrames.Split(cut.nodes, Side(cut.nodes, combination)), true,
                        $"{GraphEdgeFrames.Side(Side(cut.nodes, combination))} against {Rest(cut.nodes, combination)} cuts {cut.K} edges, found after {tried} tries.");
                    return certificate;
                }
                // The verifier only says no, so the edges that cross are counted for the step alone.
                steps.Reject(combination,
                    $"Put {GraphEdgeFrames.Side(Side(cut.nodes, combination))} against {Rest(cut.nodes, combination)}: {GraphEdgeFrames.Crossing(cut.edges.Select(e => new GraphEdgeInput(e.Key, e.Value)), Side(cut.nodes, combination).ToHashSet()).Count} edges cross, not {cut.K}.");
            } while (nextComb(combination, cut.nodes.Count));
        }
        if (cut.nodes.Count < 2)
            steps.Done(new Groups([]), false, $"A graph needs at least two nodes to have a cut.");
        else if (cut.nodes.Count / 2 == 1)
            steps.Done(new Groups([]), false,
                $"Checked {tried} S sides of 1 node, which covers every split of the nodes into two sides. None cuts exactly {Words.Count(cut.K, "edge")}.");
        else
            steps.Done(new Groups([]), false,
                $"Checked {tried} S sides of 1 to {cut.nodes.Count / 2} nodes, which covers every split of the nodes into two sides. None cuts exactly {Words.Count(cut.K, "edge")}.");
        return "{}";
    }
}


