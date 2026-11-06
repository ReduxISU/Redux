using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_EXACTCOVER.Solvers;

// The steps Exact Cover Recursive records:
//   - an Accept per set it takes, one that shares no element with the sets already taken;
//   - a Reject when every set left overlaps the sets taken, naming the elements still not covered;
//   - a Backtrack per set it takes back;
//   - the Done, with the exact cover or why there is none.
// It records no search tree.
class ExactCoverRecursive : ISolver<EXACTCOVER, ChosenSets>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "Exact Cover Recursive";
    public string solverDefinition { get; } = "This is a optimized recursive solver for Exact Cover";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Russell Phillips" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Exact recursive search WITH pruning -- distinct from ExactCoverBruteForce,
    // which does not prune.
    public SolverType solverType { get; } = SolverType.Backtracking;
    // Corrected from Exponential: solve_r has no index cursor to fix a selection order -- at every
    // level it re-scans the full remaining `possibleSubsets` list and recurses once per candidate, so
    // the exact same final combination of subsets can be assembled via any of its orderings, all of
    // which get explored separately (classic combination-search-without-a-cursor blowup). When no two
    // subsets in S share elements (so shareElememnts never prunes a branch), this degenerates into a
    // full permutation tree of the s = |S| subsets: O(s!) recursive calls in the worst case, matching
    // the Backtracking + Factorial pairing already used by NQueensBacktracking.
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Factorial;
    // Per node: building possibleSubsets scans up to s remaining subsets, each checked via
    // shareElememnts against up to s chosen subsets, O(n^2) per pairwise check (n = |X|, bounding
    // subset size) -- O(s^2 * n^2) per node. Combined with the O(s!) node count above.
    public MathematicalFunction timeComplexity { get; } = new("s! * s^2 * n^2", ", s = |S| (candidate subsets), n = |X| (bounds subset-comparison cost)");

    // --- Methods Including Constructors ---
    public ExactCoverRecursive() {

    }
    private string BinaryToCertificate(List<int> binary, List<List<string>> S) {
        string certificate = "";
        for (int i = 0; i < binary.Count; i++) {
            if (binary[i] == 1) {
                certificate += "{";
                foreach (var element in S[i]) {
                    certificate += element + ",";
                }
                certificate = certificate.TrimEnd(',') + "},";
            }
        }
        return "{" + certificate.TrimEnd(',') + "}";

    }

    private string subsetsToCertificate(List<List<string>> solution) {
        if (solution.Count == 0) {
            // The empty collection, as the other Exact Cover solvers write it; also their "no answer".
            return "{}";
        }

        string certificate = "{";

        foreach (List<string> set in solution) {
            certificate += "{";
            foreach (string item in set) {
                certificate += item.ToString() + ',';
            }
            certificate = certificate.TrimEnd(',') + "},";
        }
        certificate = certificate.TrimEnd(',') + "}";
        return certificate;

    }

    private bool shareElememnts(List<string> a, List<string> b) {
        foreach (string item in a) {
            if (b.Contains(item))
                return true;
        }
        return false;
    }

    private bool shareElememnts(List<string> set, List<List<string>> sets) {
        foreach (List<string> set2 in sets) {
            if (shareElememnts(set, set2)) {
                return true;
            }
        }
        return false;
    }

    // How the steps show the search, given by the sets taken so far, in the order taken. index gives each set of S its
    // position (by reference, so two sets with the same elements keep their own ids).
    private static RunSteps<List<List<string>>, ChosenSets> NewSteps(StepRecorder<ChosenSets> rec, Dictionary<List<string>, int> index) => new(rec,
        // What every picture shows: the sets taken, and the set taken last.
        partial: chosen => Picked(chosen, index),
        focus: chosen => chosen.Count > 0 ? [BipartiteFrames.SetId(index[chosen[^1]])] : []);

    public string solve(EXACTCOVER exactCover) => Solve(exactCover, StepRecorder<ChosenSets>.Off);

    // A depth-first search: take any set that shares no element with the sets taken, recurse, and take it back when no
    // exact cover follows from it. It stops as soon as the sets taken are an exact cover.
    public string Solve(EXACTCOVER exactCover, StepRecorder<ChosenSets> rec) {
        List<string> uSet = new List<string>(exactCover.X);
        List<List<string>> subsets = new List<List<string>>(exactCover.S);
        List<List<string>> choosenSubsets = new List<List<string>>();
        // Sets are tracked by reference, so two sets with the same elements keep their own ids (S1, S2, ...).
        var index = new Dictionary<List<string>, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < exactCover.S.Count; i++) index[exactCover.S[i]] = i;
        var steps = NewSteps(rec, index);
        List<List<string>> result = solve_r(exactCover, uSet, subsets, choosenSubsets, steps, index);
        if (timerHasExpired) return SolveRun.TimedOutAnswer; // solve_r unwinds at the time limit
        string certificate = subsetsToCertificate(result);
        if (result.Count > 0) {
            steps.Done(Picked(result, index), true, $"Every element is covered by exactly one chosen set: {BipartiteFrames.Braces(Picked(result, index).Sets)}.");
        } else {
            // Nothing chosen is the answer only when there are no elements.
            bool ok = exactCover.defaultVerifier.verify(exactCover, certificate);
            if (ok)
                steps.Done(new ChosenSets([]), true, $"There are no elements, so the empty collection {{}} is an exact cover.");
            else
                steps.Done(new ChosenSets([]), false, $"Every branch was undone. No exact cover exists.");
        }
        return certificate;
    }

    // The sets taken, by id (S1, S2, ...).
    private static ChosenSets Picked(List<List<string>> chosen, Dictionary<List<string>, int> index) =>
        new(chosen.Select(s => BipartiteFrames.SetId(index[s])));

    public List<List<string>> solve_r(EXACTCOVER exactCover, List<string> uSet, List<List<string>> subsetList, List<List<string>> choosenSubsets) {
        var index = new Dictionary<List<string>, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < exactCover.S.Count; i++) index[exactCover.S[i]] = i;
        return solve_r(exactCover, uSet, subsetList, choosenSubsets, NewSteps(StepRecorder<ChosenSets>.Off, index), index);
    }

    private List<List<string>> solve_r(EXACTCOVER exactCover, List<string> uSet, List<List<string>> subsetList, List<List<string>> choosenSubsets,
        RunSteps<List<List<string>>, ChosenSets> steps, Dictionary<List<string>, int> index) {
        // Unwind at the time limit; Solve then returns the timed-out answer.
        if (timerHasExpired)
            return new List<List<string>>();
        //check if choosen subsets is a solution, if it is, return it
        if (exactCover.defaultVerifier.verify(exactCover, subsetsToCertificate(choosenSubsets)))
            return choosenSubsets;

        //only look into subsets that don't already share an element with a choosen subset
        List<List<string>> possibleSubsets = new List<List<string>>();
        foreach (List<string> possibleset in subsetList) {
            if (!shareElememnts(possibleset, choosenSubsets)) {
                possibleSubsets.Add(possibleset);
            }
        }
        if (possibleSubsets.Count == 0) {
            // A dead end. The elements still not covered are named only for a kept step.
            string[] Uncovered() => exactCover.X.Where(x => !choosenSubsets.Any(s => s.Contains(x))).ToArray();
            steps.Reject(choosenSubsets,
                $"Every set left overlaps the sets taken, and {BipartiteFrames.List(Uncovered())} {(Uncovered().Length == 1 ? "is" : "are")} still not covered, so undo.",
                focus: () => Uncovered().Select(BipartiteFrames.ElementId).ToArray());
        }

        //foreach remaining good set in subsets, add it to choosen subsets and recurse.
        foreach (List<string> set in new List<List<string>>(possibleSubsets)) {
            //if one returns a solution return it, otherwise return empty
            possibleSubsets.Remove(set);
            choosenSubsets.Add(set);
            int i = index[set];
            steps.Accept(choosenSubsets, $"Take {BipartiteFrames.SetText(exactCover.S, i)}: it shares no element with the sets taken.");
            List<List<string>> result = solve_r(exactCover, uSet, possibleSubsets, choosenSubsets, steps, index);
            if (result.Count > 0)
                return result;
            choosenSubsets.Remove(set);
            possibleSubsets.Add(set);
            steps.Backtrack(choosenSubsets, $"Undo {BipartiteFrames.SetId(i)}: no exact cover follows from it.", focus: () => [BipartiteFrames.SetId(i)]);
        }
        return new List<List<string>>();

    } //doesnt return null for some reason

}
