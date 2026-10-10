using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Packing;
using API.Interfaces.Steps;
using SPADE;

namespace API.Problems.NPComplete.NPC_KNAPSACK.Solvers;

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


    public string complexity { get; } = "O(2^n)";

    // Every subset of len items, as 0/1 per item, in mask order: 0, 1, 2, ... with bit k picking item k. Counted with one
    // digit per item, not an int mask, so there is no limit on the number of items (#791).
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
    public string solve(KNAPSACK knapsack) => Solve(knapsack, StepRecorder<Placement>.Off);

    // Steps: one per set of items tried, in mask order (the empty set first); the first set within W worth at least V is
    // accepted.
    public string Solve(KNAPSACK knapsack, StepRecorder<Placement> rec) {
        List<UtilCollection> items = knapsack.items.ToList();
        int tried = 0;
        foreach (List<int> possibleSolution in possibleSolutions(items.Count())) {
            UtilCollection certificate = new UtilCollection("{}");
            for (int i = 0; i < knapsack.items.Count(); i++) {
                if (possibleSolution[i] == 1) certificate.Add(items[i]);
            }
            string strCertificate = certificate.ToString();
            tried++;
            // Read only inside recorder lambdas (and for Done), so they cost nothing when recording is off.
            List<int> Members() => Enumerable.Range(0, items.Count).Where(i => possibleSolution[i] == 1).ToList();
            Placement Chosen() => new(possibleSolution.Select(b => b == 1 ? 0 : -1));
            string[] Focus() => Members().Select(PackingFrames.ItemId).ToArray();
            string Set() => PackingFrames.Braces(Members().Select(i => PackingFrames.Key(items[i])));
            int Weight() => Members().Sum(i => items[i][0].parseInt());
            int Value() => Members().Sum(i => items[i][1].parseInt());
            if (knapsack.defaultVerifier.verify(knapsack, strCertificate)) {
                rec.Accept(Chosen, () => $"Try {Set()}: weight {Weight()}, value {Value()}. It works.", Focus);
                rec.Done(Chosen(), true, $"{Set()} fits in {knapsack.W} and is worth at least {knapsack.V}, found after {Tries(tried)}.");
                return strCertificate;
            }
            rec.Reject(Chosen, () => Weight() > knapsack.W
                ? $"Try {Set()}: weight {Weight()} is over {knapsack.W}."
                : $"Try {Set()}: weight {Weight()}, value {Value()}, short of {knapsack.V}.", Focus);
        }

        rec.Done(new Placement([]), false, $"Checked all {Tries(tried)}. None is worth {knapsack.V} within weight {knapsack.W}.");
        return "{}";
    }

    private static string Tries(int n) => n == 1 ? "1 set" : $"{n} sets";

}
