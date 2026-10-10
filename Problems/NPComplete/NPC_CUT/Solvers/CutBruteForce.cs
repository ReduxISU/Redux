using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_CUT.Solvers;

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
    // Declared, not derived. Enumerates subsets of size 1..K (K <= edges.Count, but K
    // can still be Theta(n)); each candidate costs O(n^2) to build (certificateToEdges)
    // plus O(m) to verify.
    public string complexity { get; } = "O(2^n * n^2 * m), n = |nodes|, m = |edges|";

    public CutBruteForce() {

    }
    private long factorial(long x) {
        long y = 1;
        for (long i = 1; i <= x; i++) {
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
    public string solve(CUT cut) => Solve(cut, StepRecorder<Groups>.Off);

    // Steps: one per candidate side S tried (sizes 1..K), in the order the solver tries them. The partial answer is the
    // split (S is group 0, the rest group 1); the edges that cross it are the cut. Recorder state lives only in this call.
    public string Solve(CUT cut, StepRecorder<Groups> rec) {
        if (cut.K > cut.edges.Count) { // impossible to do cut if not enough edges
            rec.Done(new Groups([]), false, $"The graph has only {cut.edges.Count} edges, so no cut can have {cut.K}.");
            return "{}";
        }
        int tried = 0;
        List<int> combination = new List<int>();
        string[] Side() => combination.Select(c => cut.nodes[c]).ToArray();
        string Rest() => GraphEdgeFrames.Side(cut.nodes.Except(Side()));
        int Crossing() => GraphEdgeFrames.Crossing(cut.edges.Select(e => new GraphEdgeInput(e.Key, e.Value)), Side().ToHashSet()).Count;
        for (int i = 0; i < cut.K; i++) {
            combination = new List<int>();
            for (int j = 0; j <= i; j++) {
                combination.Add(j);
            }
            long reps = factorial(cut.nodes.Count) / (factorial(i + 1) * factorial(cut.nodes.Count - i - 1));
            for (int k = 0; k < reps; k++) {
                string certificate = indexListToCertificate(combination, cut.nodes);
                certificate = certificateToEdges(cut, certificate); // last two could be one function, but it is okay
                tried++;
                if (cut.defaultVerifier.verify(cut, certificate)) {
                    rec.Accept(() => GraphEdgeFrames.Split(cut.nodes, Side()), () => $"Put {GraphEdgeFrames.Side(Side())} against {Rest()}: exactly {cut.K} edges cross.", Array.Empty<string>());
                    rec.Done(GraphEdgeFrames.Split(cut.nodes, Side()), true, $"{GraphEdgeFrames.Side(Side())} against {Rest()} cuts {cut.K} edges, found after {tried} tries.");
                    return certificate;
                }
                rec.Reject(() => GraphEdgeFrames.Split(cut.nodes, Side()),
                    () => $"Put {GraphEdgeFrames.Side(Side())} against {Rest()}: {Crossing()} edges cross, not {cut.K}.", Array.Empty<string>());
                combination = nextComb(combination, cut.nodes.Count);

            }
        }
        if (cut.K == 0) rec.Done(new Groups([]), true, "A cut of size 0 needs no edges.");
        else rec.Done(new Groups([]), false, $"Checked {tried} S sides of at most {Math.Min(cut.K, cut.nodes.Count)} nodes (this solver tries no larger ones). None cuts exactly {cut.K} edges.");
        return "{}";
    }
}


