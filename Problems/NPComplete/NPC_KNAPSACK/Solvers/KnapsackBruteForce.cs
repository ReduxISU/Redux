using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Packing;
using API.Interfaces.Steps;
using SPADE;

namespace API.Problems.NPComplete.NPC_KNAPSACK.Solvers;

// The steps Knapsack Brute Force records: one per set of items, in mask order (the empty set first). A set is a Reject
// saying it weighs more than W or is worth less than V, or the Accept that ends the search; then the Done. The recorder's
// cap hides the rest of a long run.
// Not incremental: every step judges a whole set of items.
class KnapsackBruteForce : ISolver<KNAPSACK, Placement> {

    // --- Fields ---
    public string solverName { get; } = "Knapsack Brute Force";
    public string solverDefinition { get; } = "This a brute force solver for the 0-1 Knapsack problem";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Russell Phillips" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration (own complexity string above already says "O(2^n)").
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;


    public MathematicalFunction timeComplexity { get; } = new("2^n", "");

    // Every subset of len items, as 0/1 per item, in mask order: 0, 1, 2, ... with bit k picking item k. Counted with one
    // digit per item, not an int mask, so there is no limit on the number of items.
    public IEnumerable<List<int>> possibleSolutions(int len) {
        int[] bits = new int[len];
        while (true) {
            yield return bits.ToList();
            int k = 0;
            while (k < len && bits[k] == 1) {
                bits[k] = 0;
                k++;
            }
            if (k == len) yield break;
            bits[k] = 1;
        }
    }
    // --- Methods Including Constructors ---
    //solver for 0-1 knapsack problem
    // How the steps show a candidate, given by its bits, one per item (1: taken): the items taken, all in focus.
    private static RunSteps<List<int>, Placement> NewSteps(StepRecorder<Placement> rec) => new(rec,
        // What every picture shows: the items taken, in the one container, every one of them in focus.
        partial: bits => new Placement(bits.Select(b => b == 1 ? 0 : -1)),
        focus: bits => Members(bits).Select(PackingFrames.ItemId).ToArray());

    // The positions of the items a candidate's bits take.
    private static List<int> Members(List<int> bits) => Enumerable.Range(0, bits.Count).Where(i => bits[i] == 1).ToList();

    public string solve(KNAPSACK knapsack) => Solve(knapsack, StepRecorder<Placement>.Off);

    // Tries every set of items until one weighs at most W and is worth at least V.
    public string Solve(KNAPSACK knapsack, StepRecorder<Placement> rec) {
        List<UtilCollection> items = knapsack.items.ToList();
        var steps = NewSteps(rec);
        int tried = 0;
        foreach (List<int> possibleSolution in possibleSolutions(items.Count())) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // stop at the time limit
            // Written out by position, not added to a SPADE set, so two identical items can both be taken.
            List<string> chosen = new List<string>();
            for (int i = 0; i < items.Count; i++) {
                if (possibleSolution[i] == 1) chosen.Add(items[i].ToString());
            }
            string strCertificate = "{" + string.Join(",", chosen) + "}";
            tried++;
            // The verifier only says yes or no, so the set's weight and value are worked out for the steps alone.
            int weight = rec.Enabled ? Members(possibleSolution).Sum(i => items[i][0].parseInt()) : 0;
            int value = rec.Enabled ? Members(possibleSolution).Sum(i => items[i][1].parseInt()) : 0;
            if (knapsack.defaultVerifier.verify(knapsack, strCertificate)) {
                steps.Accept(possibleSolution, $"Try {PackingFrames.Braces(Members(possibleSolution).Select(i => PackingFrames.Key(items[i])))}: weight {weight}, value {value}. It works.");
                steps.Done(new Placement(possibleSolution.Select(b => b == 1 ? 0 : -1)), true,
                    $"{PackingFrames.Braces(Members(possibleSolution).Select(i => PackingFrames.Key(items[i])))} fits in {knapsack.W} and is worth at least {knapsack.V}, found after {Words.Count(tried, "set")}.");
                return strCertificate;
            }
            if (weight > knapsack.W)
                steps.Reject(possibleSolution, $"Try {PackingFrames.Braces(Members(possibleSolution).Select(i => PackingFrames.Key(items[i])))}: weight {weight} is over {knapsack.W}.");
            else
                steps.Reject(possibleSolution, $"Try {PackingFrames.Braces(Members(possibleSolution).Select(i => PackingFrames.Key(items[i])))}: weight {weight}, value {value}, short of {knapsack.V}.");
        }

        steps.Done(new Placement([]), false, $"Checked all {Words.Count(tried, "set")}. None is worth {knapsack.V} within weight {knapsack.W}.");
        return "{}";
    }

}
