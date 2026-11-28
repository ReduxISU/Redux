using API.Interfaces;
using API.Problems.NPComplete.NPC_HITTINGSET;
using SPADE;

namespace API.Problems.NPComplete.NPC_HITTINGSET.Solvers;

class HittingSetBruteForce : ISolver<HITTINGSET> {

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
    public MathematicalFunction timeComplexity { get; } = new("2^|U| * |S| * |U|", ", |U| = size of the universe, |S| = number of subsets");

    // --- Methods Including Constructors ---
    public HittingSetBruteForce() {

    }

    private const int MaxItems = 62;

    // Every 0/1 choice of len items, item b taken when bit b of i is set. A long mask holds at most 62 items
    // (1L << 63 is negative); the int mask this replaced went wrong past 30 (#1028).
    public IEnumerable<List<int>> possibleSolutions(int len, string problemName) {
        if (len > MaxItems) throw new ExactSearchLimitExceededException(problemName, len, MaxItems);
        for (long i = 0; i < (1L << len); i++) {
            List<int> solution = new List<int>(len);
            for (int b = 0; b < len; b++)
                solution.Add((int)((i >> b) & 1));
            yield return solution;
        }
    }

    public string solve(HITTINGSET hittingSet) {
        List<UtilCollection> items = hittingSet.universalSet.ToList();
        foreach (List<int> possibleSolution in possibleSolutions(items.Count(), hittingSet.problemName)) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // #935: stop at the time limit
            UtilCollection certificate = new UtilCollection("{}");
            for (int i = 0; i < items.Count; i++) {
                if (possibleSolution[i] == 1) certificate.Add(items[i]);
            }
            string strCertificate = certificate.ToString();
            if (hittingSet.defaultVerifier.verify(hittingSet, strCertificate)) return strCertificate;
        }

        return "";

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
