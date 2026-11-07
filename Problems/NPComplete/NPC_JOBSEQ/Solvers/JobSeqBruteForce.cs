using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Schedule;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_JOBSEQ.Solvers;

// The steps Job Sequencing Brute Force records, for the Schedule picture: one per order of the jobs, in the order Orders
// hands them out. An order is a Reject saying which jobs finish late and what penalty that costs (the late jobs in focus),
// or the Accept of the first order whose penalty is within K; then the Done. The recorder's cap hides the rest of a long
// run.
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
    // Declared, not derived. GenerateCombinations lazily yields all p! permutations
    // of the job indices (p = number of jobs) as solve() iterates and verifies each
    // (verify is O(p)).
    public MathematicalFunction timeComplexity { get; } = new("p! * p", ", p = number of jobs");

    // --- Methods Including Constructors ---
    public JobSeqBruteForce() {

    }
    // Lazy (same order as before), so solve() can stop at the time limit without first building all p! permutations.
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

    /// <summary>
    /// The orders of <paramref name="x"/> jobs, one at a time, in the same order as <see cref="GenerateCombinations"/>
    /// (lexicographic, starting from 0, 1, ..., x - 1), without building them all first. Each order is a new list.
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
    // How the steps show an order: the jobs in that order, and the ones that finish late.
    private static RunSteps<(List<int> Order, IReadOnlyList<int> Late), JobOrder> NewSteps(StepRecorder<JobOrder> rec) => new(rec,
        // What every picture shows: the order, and its late jobs.
        partial: s => ScheduleFrames.Order(s.Order),
        focus: s => s.Late.Select(ScheduleFrames.JobId).ToArray());

    public string solve(JOBSEQ jobseq) => Solve(jobseq, StepRecorder<JobOrder>.Off);

    // Tries every order of the jobs until one keeps the late penalty within K.
    public string Solve(JOBSEQ jobseq, StepRecorder<JobOrder> rec) {
        int tried = 0;
        var steps = NewSteps(rec);
        foreach (List<int> permutation in Orders(jobseq.T.Count())) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // stop at the time limit
            tried++;
            // The verifier only says yes or no, so which jobs finish late, and the penalty they cost, are worked out for the
            // steps alone (only while recording).
            IReadOnlyList<int> late = rec.Enabled ? Late(jobseq, permutation) : [];
            int penalty = rec.Enabled ? late.Sum(j => jobseq.P[j]) : 0;
            if (jobseq.defaultVerifier.verify(jobseq, permutation)) {
                // The answer's step is about the whole order, not its late jobs.
                if (late.Count == 0)
                    steps.Accept((permutation, late), $"Try {permToCertificate(permutation)}: every job finishes on time, penalty {penalty}, within K = {jobseq.K}. It works.", focus: () => []);
                else if (late.Count == 1)
                    steps.Accept((permutation, late), $"Try {permToCertificate(permutation)}: job {late[0]} finishes late, penalty {penalty}, within K = {jobseq.K}. It works.", focus: () => []);
                else
                    steps.Accept((permutation, late), $"Try {permToCertificate(permutation)}: jobs {Words.List(late.Select(j => j.ToString()).ToList())} finish late, penalty {penalty}, within K = {jobseq.K}. It works.", focus: () => []);
                steps.Done(ScheduleFrames.Order(permutation), true, $"{permToCertificate(permutation)} is the first order that keeps the late penalty within K = {jobseq.K}, found after {Words.Number(tried)} {(tried == 1 ? "order" : "orders")}.");
                return permToCertificate(permutation);
            }
            if (late.Count == 0)
                steps.Reject((permutation, late), $"Try {permToCertificate(permutation)}: every job finishes on time, penalty {penalty} > K = {jobseq.K}.");
            else if (late.Count == 1)
                steps.Reject((permutation, late), $"Try {permToCertificate(permutation)}: job {late[0]} finishes late, penalty {penalty} > K = {jobseq.K}.");
            else
                steps.Reject((permutation, late), $"Try {permToCertificate(permutation)}: jobs {Words.List(late.Select(j => j.ToString()).ToList())} finish late, penalty {penalty} > K = {jobseq.K}.");
        }
        if (tried == 1)
            steps.Done(new JobOrder([]), false, $"There is only one order, and its late penalty is more than K = {jobseq.K}.");
        else
            steps.Done(new JobOrder([]), false, $"Checked all {Words.Number(tried)} {(tried == 1 ? "order" : "orders")}. None keeps the late penalty within K = {jobseq.K}.");
        // No order keeps the penalty within K: "{}" is "no answer".
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
