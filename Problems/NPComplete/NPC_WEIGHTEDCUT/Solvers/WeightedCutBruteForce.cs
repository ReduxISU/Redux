using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_WEIGHTEDCUT.Solvers;

// The steps Weighted Cut Brute Force records: one per candidate S side tried (every size, never bounded by K), in the
// order the solver tries them. A side whose crossing edges weigh something other than K is a Reject saying what they
// weigh; the first whose crossing edges weigh K is the Accept that ends the search; then the Done. The recorder's cap
// hides the rest of a long run.
class WeightedCutBruteForce : ISolver<WEIGHTEDCUT, Groups> {

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
    public MathematicalFunction timeComplexity { get; } = new("2^n * n^2 * m", ", n = |nodes|, m = |edges|");

    public WeightedCutBruteForce() {

    }
    private long factorial(long x) {
        long y = 1;
        for (long i = 1; i <= x; i++) {
            y *= i;
        }
        return y;
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
    // How the steps show a candidate, given by the indexes of its S side's nodes: the split, S as group 0 and the rest as
    // group 1 (the edges that cross are the cut).
    private static RunSteps<List<int>, Groups> NewSteps(List<string> nodes, StepRecorder<Groups> rec) => new(rec,
        // What every picture shows: the split.
        partial: combination => GraphEdgeFrames.Split(nodes, Side(nodes, combination)));

    // The names of the S side's nodes.
    private static string[] Side(List<string> nodes, List<int> combination) => combination.Select(c => nodes[c]).ToArray();

    // The other side, written as the steps write a side.
    private static string Rest(List<string> nodes, List<int> combination) => GraphEdgeFrames.Side(nodes.Except(Side(nodes, combination)));

    public string solve(WEIGHTEDCUT cut) => Solve(cut, StepRecorder<Groups>.Off);

    // Tries every S side, of every size (never bounded by K), until the crossing edges weigh exactly K.
    public string Solve(WEIGHTEDCUT cut, StepRecorder<Groups> rec) {
        var steps = NewSteps(cut.nodes, rec);
        int tried = 0;
        List<int> combination = new List<int>();
        for (int i = 0; i < cut.nodes.Count; i++) {
            combination = new List<int>();
            for (int j = 0; j <= i; j++) {
                combination.Add(j);
            }
            long reps = factorial(cut.nodes.Count) / (factorial(i + 1) * factorial(cut.nodes.Count - i - 1));
            for (int k = 0; k < reps; k++) {
                if (timerHasExpired) return SolveRun.TimedOutAnswer; // stop at the time limit
                string certificate = indexListToCertificate(combination, cut.nodes);
                certificate = certificateToEdges(cut, certificate);
                tried++;
                if (cut.defaultVerifier.verify(cut, certificate)) {
                    steps.Accept(combination, $"Put {GraphEdgeFrames.Side(Side(cut.nodes, combination))} against {Rest(cut.nodes, combination)}: the crossing edges weigh {cut.K}, as wanted.");
                    steps.Done(GraphEdgeFrames.Split(cut.nodes, Side(cut.nodes, combination)), true,
                        $"{GraphEdgeFrames.Side(Side(cut.nodes, combination))} against {Rest(cut.nodes, combination)} cuts edges of total weight {cut.K}, found after {tried} tries.");
                    return certificate;
                }
                // The verifier only says no, so the crossing edges are weighed for the step alone.
                steps.Reject(combination,
                    $"Put {GraphEdgeFrames.Side(Side(cut.nodes, combination))} against {Rest(cut.nodes, combination)}: the crossing edges weigh {GraphEdgeFrames.Crossing(cut.edges.Select(e => new GraphEdgeInput(e.source, e.destination, e.weight)), Side(cut.nodes, combination).ToHashSet()).Weight}, not {cut.K}.");
                combination = nextComb(combination, cut.nodes.Count);
            }
        }
        steps.Done(new Groups([]), false, $"Checked {tried} ways to pick the S side. None cuts edges of total weight {cut.K}.");
        return "{}";
    }
}
