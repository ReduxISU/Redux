using API.Interfaces;
using API.Problems.NPComplete.NPC_HITTINGSET;
using SPADE;

namespace API.Problems.NPComplete.NPC_HITTINGSET.Solvers;

class HittingSetBruteForce : ISolver<HITTINGSET> {

        // --- Fields ---
        public string solverName { get; } = "Hitting Set Brute Force";
        public string solverDefinition { get; } = "This is a brute force solver for Hitting Set";
        public string source { get; } = "";
        public string[] contributors { get; } = { "Russell Phillips" };
        public bool timerHasExpired { get; set; }
        // Declared, not derived. Unpruned exhaustive enumeration.
        public SolverType solverType { get; } = SolverType.BruteForce;
        public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
        // Declared, not derived. possibleSolutions enumerates all 2^n subsets of the universal
        // set (n = |universalSet|; there is no K to bound this one, unlike the other set-cover-
        // family brute forces in this batch). Each candidate costs O(s*n) to verify
        // (HittingSetVerifier intersects the candidate against every one of the s subsets).
        public string complexity { get; } = "O(2^n * s * n), n = |universalSet|, s = number of subsets in S";

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

        public string solve(HITTINGSET hittingSet) {
                List<UtilCollection> items = hittingSet.universalSet.ToList();
                foreach (List<int> possibleSolution in possibleSolutions(items.Count())) {
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
