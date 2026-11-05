using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_HITTINGSET;
using API.Problems.NPComplete.NPC_HITTINGSET.Visualizations;
using SPADE;

namespace API.Problems.NPComplete.NPC_HITTINGSET.Solvers;

class HittingSetBruteForce : ISolver<HITTINGSET, NodeSet> {

    // --- Fields ---
    public string solverName { get; } = "Exact Hitting Set Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for Exact Hitting Set. It tests every subset of U and returns the first one that intersects every subset in S in exactly one element.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Russell Phillips" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. possibleSolutions enumerates all 2^n subsets of the universal
    // set (n = |universalSet|; there is no K to bound this one, unlike the other set-cover-
    // family brute forces in this batch). Each candidate costs O(s*n) to verify
    // (HittingSetVerifier intersects the candidate against every one of the s subsets).
    public MathematicalFunction timeComplexity { get; } = new("2^n * s * n", ", n = |universalSet|, s = number of subsets in S");

    // --- Methods Including Constructors ---
    public HittingSetBruteForce() {

    }

    public IEnumerable<List<int>> possibleSolutions(int len) {
        for (int i = 0; i < Math.Pow(2, len); i++) {
            List<int> solution = new List<int>();
            for (int solBin = i + (int)Math.Pow(2, len); solBin != 1; solBin >>= 1) {
                if ((solBin & 1) == 0)
                    solution.Add(0);
                else
                    solution.Add(1);
            }
            yield return solution;
        }
    }

    public string solve(HITTINGSET hittingSet) => Solve(hittingSet, StepRecorder<NodeSet>.Off);

    // Steps: one per subset of U tried, in the order the solver tries them (counting in binary from the empty set, the
    // first element the lowest bit). Recorder state lives only in this call.
    public string Solve(HITTINGSET hittingSet, StepRecorder<NodeSet> rec) {
        List<UtilCollection> items = hittingSet.universalSet.ToList();
        int tried = 0;
        foreach (List<int> possibleSolution in possibleSolutions(items.Count())) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // #935: stop at the time limit
            UtilCollection certificate = new UtilCollection("{}");
            for (int i = 0; i < items.Count; i++) {
                if (possibleSolution[i] == 1) certificate.Add(items[i]);
            }
            string strCertificate = certificate.ToString();
            string[] Picked() => Enumerable.Range(0, items.Count).Where(i => possibleSolution[i] == 1).Select(i => items[i].ToString()).ToArray();
            tried++;
            if (hittingSet.defaultVerifier.verify(hittingSet, strCertificate)) {
                rec.Accept(() => new NodeSet(Picked()), () => $"Try {BipartiteFrames.Braces(Picked())}. It hits every set exactly once.",
                    () => Picked().Select(BipartiteFrames.ElementId).ToArray());
                rec.Done(new NodeSet(Picked()), true,
                    $"{BipartiteFrames.Braces(Picked())} hits every set exactly once, found after {tried} {(tried == 1 ? "try" : "tries")}.");
                return strCertificate;
            }
            rec.Reject(() => new NodeSet(Picked()), () => $"Try {BipartiteFrames.Braces(Picked())}: {Why(hittingSet, Picked())}.",
                () => Picked().Select(BipartiteFrames.ElementId).ToArray());
        }

        // No hitting set exists. "{}", as the other set solvers answer; the verifier rejects it whenever S is not empty (#776).
        rec.Done(new NodeSet([]), false, $"Checked all {tried} subsets of U. None hits every set exactly once.");
        return "{}";

    }

    private static string Why(HITTINGSET hittingSet, string[] picked) {
        string why = BipartiteFrames.Why(HittingSetDefaultVisualization.Spec(hittingSet), picked.Select(BipartiteFrames.ElementId));
        return why.Length > 0 ? why : "the verifier rejects it";
    }

    /// <summary>
    /// Given Independent Set instance in string format and solution string, outputs a solution dictionary with 
    /// true values mapped to nodes that are in the solution set else false. 
    /// </summary>
    /// <param name="problemInstance"></param>
    /// <param name="solutionString"></param>
    /// <returns></returns>
    public Dictionary<string, bool> getSolutionDict(string problemInstance, string solutionString) {
        throw new NotImplementedException();
    }
}
