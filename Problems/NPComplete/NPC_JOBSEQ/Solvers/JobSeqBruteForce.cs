using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_JOBSEQ.Solvers;

class JobSeqBruteForce : ISolver<JOBSEQ> {

    // --- Fields ---
    public string solverName { get; } = "Job Sequencing Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for the NP-Complete Job Sequencing problem";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Russell Phillips" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration over permutations -- factorial worst case.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Factorial;
    // Declared, not derived. GenerateCombinations lazily yields all p! permutations
    // of the job indices (p = number of jobs) as solve() iterates and verifies each
    // (verify is O(p)).
    public MathematicalFunction timeComplexity { get; } = new("p! * p", ", p = number of jobs");

    // --- Methods Including Constructors ---
    public JobSeqBruteForce() {

    }
    // #935: lazy (same order as before), so solve() can stop at the time limit without first building all p! permutations.
    public static IEnumerable<List<int>> GenerateCombinations(int x) {
        List<int> currentCombination = new List<int>();
        for (int i = 0; i < x; i++) {
            currentCombination.Add(i);
        }

        yield return new List<int>(currentCombination);

        while (true) {
            if (GetNextCombination(currentCombination)) {
                yield return new List<int>(currentCombination);
            } else {
                break; // All combinations have been generated
            }
        }
    }

    private static bool GetNextCombination(List<int> combination) {
        int x = combination.Count;
        int i = x - 2;
        while (i >= 0 && combination[i] >= combination[i + 1]) {
            i--;
        }

        if (i < 0) {
            return false; // No more combinations
        }

        int j = x - 1;
        while (combination[j] <= combination[i]) {
            j--;
        }

        // Swap elements at indices i and j
        int temp = combination[i];
        combination[i] = combination[j];
        combination[j] = temp;

        // Reverse the sequence from i+1 to the end
        combination.Reverse(i + 1, x - i - 1);

        return true;
    }
    public string permToCertificate(List<int> permutation) {
        string certificate = "(";
        foreach (int i in permutation) {
            certificate += $"{i},";
        }
        return certificate.TrimEnd(',') + ")";
    }
    public string solve(JOBSEQ jobseq) {
        foreach (List<int> permutation in GenerateCombinations(jobseq.T.Count())) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            if (jobseq.defaultVerifier.verify(jobseq, permutation)) {
                return permToCertificate(permutation);
            }
        }
        return "()";
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
