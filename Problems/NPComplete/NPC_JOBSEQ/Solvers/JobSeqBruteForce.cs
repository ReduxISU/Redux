using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Schedule;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_JOBSEQ.Solvers;

// Not incremental: every step judges a whole order of the jobs.
class JobSeqBruteForce : ISolver<JOBSEQ, JobOrder> {

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
    // Declared, not derived. GenerateCombinations eagerly materializes all p! permutations
    // of the job indices (p = number of jobs) before solve() iterates and verifies each
    // (verify is O(p)).
    public string complexity { get; } = "O(p! * p), p = number of jobs";

    // --- Methods Including Constructors ---
    public JobSeqBruteForce() {

    }
    public static List<List<int>> GenerateCombinations(int x) {
        List<int> currentCombination = new List<int>();
        for (int i = 0; i < x; i++) {
            currentCombination.Add(i);
        }

        List<List<int>> combinations = new List<List<int>>();
        combinations.Add(new List<int>(currentCombination));

        while (true) {
            if (GetNextCombination(currentCombination)) {
                combinations.Add(new List<int>(currentCombination));
            } else {
                break; // All combinations have been generated
            }
        }

        return combinations;
    }

    /// <summary>
    /// The orders of <paramref name="x"/> jobs, one at a time, in the same order as <see cref="GenerateCombinations"/>
    /// (lexicographic, starting from 0, 1, ..., x - 1), without building them all first (#802). Each order is a new list.
    /// </summary>
    public static IEnumerable<List<int>> Orders(int x) {
        List<int> current = Enumerable.Range(0, x).ToList();
        do {
            yield return new List<int>(current);
        } while (GetNextCombination(current));
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
    public string solve(JOBSEQ jobseq) => Solve(jobseq, StepRecorder<JobOrder>.Off);

    // Steps: one per order tried, in the order Orders hands them out; the first order whose late penalty fits K is accepted.
    public string Solve(JOBSEQ jobseq, StepRecorder<JobOrder> rec) {
        int tried = 0;
        foreach (List<int> permutation in Orders(jobseq.T.Count())) {
            tried++;
            // Read only inside recorder lambdas (and for Done), so they cost nothing when recording is off.
            JobOrder Order() => ScheduleFrames.Order(permutation);
            string[] LateIds() => Late(jobseq, permutation).Select(ScheduleFrames.JobId).ToArray();
            if (jobseq.defaultVerifier.verify(jobseq, permutation)) {
                rec.Accept(Order, () => TryCaption(jobseq, permutation, fits: true));
                rec.Done(Order(), true,
                    $"{permToCertificate(permutation)} is the first order that keeps the late penalty within K = {jobseq.K}, found after {Tries(tried)}.");
                return permToCertificate(permutation);
            }
            rec.Reject(Order, () => TryCaption(jobseq, permutation, fits: false), LateIds);
        }
        rec.Done(new JobOrder([]), false, tried == 1
            ? $"There is only one order, and its late penalty is more than K = {jobseq.K}."
            : $"Checked all {Tries(tried)}. None keeps the late penalty within K = {jobseq.K}.");
        // No order keeps the penalty within K: "{}" is "no answer" (#801, #605).
        return "{}";
    }

    // The jobs of an order that finish after their deadline, in run order (late as the verifier counts it).
    private static List<int> Late(JOBSEQ jobseq, List<int> order) {
        var late = new List<int>();
        int time = 0;
        foreach (int j in order) {
            time += jobseq.T[j];
            if (time > jobseq.D[j]) late.Add(j);
        }
        return late;
    }

    // "Try (0,1,2,5,3,4): jobs 2, 3 and 4 finish late, penalty 10 > K = 4."
    private string TryCaption(JOBSEQ jobseq, List<int> order, bool fits) {
        var late = Late(jobseq, order);
        int penalty = late.Sum(j => jobseq.P[j]);
        string which = late.Count switch {
            0 => "every job finishes on time",
            1 => $"job {late[0]} finishes late",
            _ => $"jobs {ScheduleFrames.AndList(late.Select(j => j.ToString()).ToList())} finish late",
        };
        return fits
            ? $"Try {permToCertificate(order)}: {which}, penalty {penalty}, within K = {jobseq.K}. It works."
            : $"Try {permToCertificate(order)}: {which}, penalty {penalty} > K = {jobseq.K}.";
    }

    private static string Tries(int n) => n == 1 ? "1 order" : $"{ScheduleFrames.Num(n)} orders";

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
