using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_EXACTCOVER.Solvers;

// The steps Exact Cover Recursive records, for the Bipartite Graph picture and the Search Tree picture alike:
//   - an Accept per set it takes, one that shares no element with the sets already taken (in the tree, a node "S2" under
//     the set taken before it, the solution when the sets taken cover every element);
//   - a Reject when every set left overlaps the sets taken, naming the elements still not covered (in the tree, the set
//     taken last is pruned, its note the elements left);
//   - a Backtrack per set it takes back (that node fails, or stays pruned when it was a dead end);
//   - the Done, with the exact cover or why there is none; it names the answer's path in the tree, root first, or marks the
//     root failed.
// In the tree, a node is a set taken, and its children are the sets it tries next.
class ExactCoverRecursive : ISolver<EXACTCOVER, ChosenSets>, IIncrementalSolver, ISearchTreeSolver {

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

    // A step of the search: the sets taken so far, in the order taken, and the search-tree node it is about, the set at
    // position Set of S (-1: the root) at Depth (0 for the root, d for the d-th set taken on the branch), left in state
    // Tree. DeadEnd marks the Reject of a branch no set left can go on from.
    private readonly record struct Move(List<List<string>> Taken, int Depth, SearchNodeState Tree, int Set, bool DeadEnd = false);

    // How the steps show a move. index gives each set of S its position (by reference, so two sets with the same elements
    // keep their own ids). The search keeps its own tree (a "start" root, a dead end as a pruned node, a set failed when it
    // is taken back), so it gives RunSteps the tree node of each step rather than using SearchSteps.
    private static RunSteps<Move, ChosenSets> NewSteps(StepRecorder<ChosenSets> rec, EXACTCOVER exactCover, Dictionary<List<string>, int> index,
        DepthFirstTree tree) => new(rec,
        // What every picture shows: the sets taken, and the set taken last.
        partial: m => Picked(m.Taken, index),
        focus: m => m.Taken.Count > 0 ? [SetPieces.SetId(index[m.Taken[^1]])] : [],
        // The Search Tree picture's node only: the set taken ("S2"); it is the solution once no element is left uncovered
        // (the sets taken never share one), and a dead end's note is the elements left.
        tree: m => {
            int uncovered = exactCover.X.Count(x => !m.Taken.Any(s => s.Contains(x)));
            return tree.Step(m.Depth, m.Set >= 0 ? SetPieces.SetId(m.Set) : "",
                m.Tree == SearchNodeState.Explored && uncovered == 0 ? SearchNodeState.Solution : m.Tree,
                m.DeadEnd ? $"{Words.Count(uncovered, "element")} uncovered" : null);
        });

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
        // A set is taken at most once on a branch, so a branch is never deeper than the number of sets.
        var tree = new DepthFirstTree(exactCover.S.Count);
        var steps = NewSteps(rec, exactCover, index, tree);
        List<List<string>> result = solve_r(exactCover, uSet, subsets, choosenSubsets, tree, steps, index);
        if (timerHasExpired) return SolveRun.TimedOutAnswer; // solve_r unwinds at the time limit
        string certificate = subsetsToCertificate(result);
        if (result.Count > 0) {
            steps.Done(Picked(result, index), true, $"Every element is covered by exactly one chosen set: {Words.Braces(Picked(result, index).Sets)}.",
                tree: () => tree.AnswerPath(result.Select(s => SetPieces.SetId(index[s])).ToList()));
        } else {
            // Nothing chosen is the answer only when there are no elements.
            bool ok = exactCover.defaultVerifier.verify(exactCover, certificate);
            if (ok)
                steps.Done(new ChosenSets([]), true, $"There are no elements, so the empty collection {{}} is an exact cover.",
                    tree: () => tree.AnswerPath([]));
            else
                steps.Done(new ChosenSets([]), false, $"Every branch was undone. No exact cover exists.", tree: () => tree.NoAnswer());
        }
        return certificate;
    }

    // The sets taken, by id (S1, S2, ...).
    private static ChosenSets Picked(List<List<string>> chosen, Dictionary<List<string>, int> index) =>
        new(chosen.Select(s => SetPieces.SetId(index[s])));

    public List<List<string>> solve_r(EXACTCOVER exactCover, List<string> uSet, List<List<string>> subsetList, List<List<string>> choosenSubsets) {
        var index = new Dictionary<List<string>, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < exactCover.S.Count; i++) index[exactCover.S[i]] = i;
        var tree = new DepthFirstTree(exactCover.S.Count);
        return solve_r(exactCover, uSet, subsetList, choosenSubsets, tree, NewSteps(StepRecorder<ChosenSets>.Off, exactCover, index, tree), index);
    }

    private List<List<string>> solve_r(EXACTCOVER exactCover, List<string> uSet, List<List<string>> subsetList, List<List<string>> choosenSubsets,
        DepthFirstTree tree, RunSteps<Move, ChosenSets> steps, Dictionary<List<string>, int> index) {
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
            steps.Reject(new(choosenSubsets, choosenSubsets.Count, SearchNodeState.Pruned, choosenSubsets.Count > 0 ? index[choosenSubsets[^1]] : -1, DeadEnd: true),
                $"Every set left overlaps the sets taken, and {Words.List(Uncovered())} {(Uncovered().Length == 1 ? "is" : "are")} still not covered, so undo.",
                focus: () => Uncovered().Select(SetPieces.ElementId).ToArray());
        }

        //foreach remaining good set in subsets, add it to choosen subsets and recurse.
        foreach (List<string> set in new List<List<string>>(possibleSubsets)) {
            //if one returns a solution return it, otherwise return empty
            possibleSubsets.Remove(set);
            choosenSubsets.Add(set);
            int i = index[set];
            tree.Branch(choosenSubsets.Count);
            steps.Accept(new(choosenSubsets, choosenSubsets.Count, SearchNodeState.Explored, i), $"Take {SetPieces.SetText(exactCover.S, i)}: it shares no element with the sets taken.");
            List<List<string>> result = solve_r(exactCover, uSet, possibleSubsets, choosenSubsets, tree, steps, index);
            if (result.Count > 0)
                return result;
            choosenSubsets.Remove(set);
            possibleSubsets.Add(set);
            // In the tree, the set fails, unless it was a dead end (the tree keeps it pruned, with its note).
            steps.Backtrack(new(choosenSubsets, choosenSubsets.Count + 1, SearchNodeState.Failed, i),
                $"Undo {SetPieces.SetId(i)}: no exact cover follows from it.", focus: () => [SetPieces.SetId(i)]);
        }
        return new List<List<string>>();

    } //doesnt return null for some reason

}
