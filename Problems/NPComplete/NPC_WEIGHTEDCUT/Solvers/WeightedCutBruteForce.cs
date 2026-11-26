using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_WEIGHTEDCUT.Solvers;

class WeightedCutBruteForce : ISolver<WEIGHTEDCUT> {

    // --- Fields ---
    public string solverName { get; } = "Weighted Cut Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for the Weighted Cut problem";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Enumerates ALL subset sizes 0..n-1 regardless of K (unlike
    // CutBruteForce, this is never bounded by K); each candidate costs O(n^2 * m) to
    // build (certificateToEdges) plus O(m) to verify.
    public MathematicalFunction timeComplexity { get; } = new("2^|N| * |N|^2 * (|E| + 1)", ", |N| = number of nodes, |E| = number of edges");

    public WeightedCutBruteForce() {

    }
    // The number of ways to choose k of n, as n/1 * (n-1)/2 * ...: each partial product is itself a binomial, so it divides
    // exactly. Written so in place of n! / (k! (n-k)!), whose factorials overflow a long past 20 nodes and gave a wrong count
    // (24 nodes: 13 sides checked, then "none" reported).
    private static long Binomial(long n, long k) {
        long r = 1;
        for (long j = 1; j <= k; j++) r = checked(r * (n - k + j)) / j;
        return r;
    }
    private string indexListToCertificate(List<int> indecies, List<string> nodes) {
        // Console.WriteLine("indecies: ", indecies.ToString());
        string certificate = "";
        foreach (int i in indecies) {
            certificate += nodes[i] + ",";
        }
        certificate = certificate.TrimEnd(',');
        // Console.WriteLine("returned statement: {"+certificate+"}");
        return "{" + certificate + "}";
    }

    private List<string> parseCertificate(string certificate) {

        List<string> nodeList = GraphParser.parseNodeListWithStringFunctions(certificate);
        return nodeList;
    }

    // Function below turns certificate into list of edges
    private string certificateToEdges(WEIGHTEDCUT cut, string certificate) {
        List<string> nodeList = parseCertificate(certificate);
        certificate = "{";
        foreach (var i in nodeList) {
            foreach (var j in cut.nodes) {
                if (!nodeList.Contains(j)) {
                    int weight = 0;
                    bool edgeFound1 = cut.edges.Any(edge => edge.source == j && edge.destination == i);
                    bool edgeFound2 = cut.edges.Any(edge => edge.source == i && edge.destination == j);
                    if (edgeFound1 && !i.Equals(j)) { // checks if edge exists
                        foreach (var edge in cut.edges) {
                            if (edge.source == j && edge.destination == i) {
                                weight = edge.weight;
                            }
                        }
                        certificate += "({" + i + "," + j + "}," + weight + "),"; // adds edge
                    } else if (edgeFound2 && !i.Equals(j)) { // checks if edge exists
                        foreach (var edge in cut.edges) {
                            if (edge.source == i && edge.destination == j) {
                                weight = edge.weight;
                            }
                        }
                        certificate += "({" + i + "," + j + "}," + weight + "),"; // adds edge
                    }
                }
            }
        }
        certificate = certificate.TrimEnd(',');
        certificate += "}";
        return certificate;

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
    public string solve(WEIGHTEDCUT cut) {
        for (int i = 0; i < cut.nodes.Count; i++) {
            List<int> combination = new List<int>();
            for (int j = 0; j <= i; j++) {
                combination.Add(j);
            }
            long reps = Binomial(cut.nodes.Count, i + 1);
            for (int k = 0; k < reps; k++) {
                if (timerHasExpired) return SolveRun.TimedOutAnswer; // #935: stop at the time limit
                string certificate = indexListToCertificate(combination, cut.nodes);
                certificate = certificateToEdges(cut, certificate);
                if (cut.defaultVerifier.verify(cut, certificate)) {
                    return certificate;
                }
                combination = nextComb(combination, cut.nodes.Count);
            }
        }
        return "{}";
    }
}
