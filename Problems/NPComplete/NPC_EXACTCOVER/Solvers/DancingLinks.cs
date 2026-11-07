using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System.Diagnostics;

namespace API.Problems.NPComplete.NPC_EXACTCOVER.Solvers;

// The steps Knuth's Algorithm X records, for the Bipartite Graph picture and the Search Tree picture alike:
//   - an Accept per set it tries for the uncovered element with the fewest sets left (the first set saying which element
//     that is, the others that they are tried for it instead); in the tree, a node "S2" under the set taken before it, the
//     solution when it covers the last element;
//   - a Reject when an uncovered element has no set left that doesn't double up an element already covered (in the tree,
//     a pruned node named after the element, noted "no set left");
//   - a Backtrack per set it takes back (that node fails);
//   - the Done, with the exact cover or why there is none; it names the answer's path in the tree, root first, or marks the
//     root failed.
// In the tree, a node is a set taken, and its children are the sets tried for the next element.
class DancingLinks : ISolver<EXACTCOVER, ChosenSets>, IIncrementalSolver, ISearchTreeSolver {

    // --- Fields ---
    public string solverName { get; } = "Knuth's Algorithm X";
    public string solverDefinition { get; } = "";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Exact search WITH pruning (Algorithm X via dancing links) -- distinct
    // from ExactCoverBruteForce, which does not prune. Also DepthFirstSearch: iterate() recurses one
    // row choice deeper before trying the next.
    public SolverType solverType { get; } = SolverType.Backtracking | SolverType.DepthFirstSearch;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Choosing the minimum-remaining-rows column each step (iterate's OrderBy) is a heuristic that cuts
    // branching in practice, but it does not change the worst-case bound: any subset of the s = |S| rows
    // could still be visited by some sequence of column choices, so the search tree is still bounded by
    // the standard O(2^s) exact-cover worst case. Each node's select/deselect walks, for every column a
    // row covers (<= x = |X|), every row covering that column (<= s), every column that row covers
    // (<= x), doing an O(s) List<int>.Remove -- O(s^2 * x^2) per node in the worst (densest) case.
    public MathematicalFunction timeComplexity { get; } = new("2^s * s^2 * x^2", ", s = |S| (rows/subsets), x = |X| (columns/universe)");

    // --- Methods Including Constructors ---
    public DancingLinks() {

    }

    // A step of the search: its stack of sets taken (positions in S; the last taken on top), and the search-tree node it is
    // about, at Depth (0 for the root, d for the d-th set taken on the branch), left in state Tree: the set Row, or the
    // element Element that has no set left.
    private readonly record struct Move(Stack<int> Taken, int Depth, SearchNodeState Tree, int Row = -1, string? Element = null);

    // How the steps show a move. The search keeps its own tree (a "start" root, an element with no set left as a pruned
    // node, a set failed when it is taken back), so it gives RunSteps the tree node of each step rather than using
    // SearchSteps.
    private static RunSteps<Move, ChosenSets> NewSteps(StepRecorder<ChosenSets> rec, DepthFirstTree tree) => new(rec,
        // What every picture shows: the sets taken, and the set taken last.
        partial: m => Picked(m.Taken),
        focus: m => m.Taken.Count > 0 ? [BipartiteFrames.SetId(m.Taken.Peek())] : [],
        // The Search Tree picture's node only: the set taken ("S2"), or the element with no set left, noted.
        tree: m => m.Row >= 0 ? tree.Step(m.Depth, BipartiteFrames.SetId(m.Row), m.Tree) : tree.Step(m.Depth, m.Element!, m.Tree, "no set left"));

    public string solve(EXACTCOVER exactCover) => Solve(exactCover, StepRecorder<ChosenSets>.Off);

    // Algorithm X: X maps each element not yet covered (column) to the sets (rows) still able to cover it, Y each set to
    // its elements. Taking a set removes its elements' columns and every other set that shares one of them; taking it back
    // restores them.
    public string Solve(EXACTCOVER exactCover, StepRecorder<ChosenSets> rec) {
        // Each set taken covers at least one more element, so a branch takes at most as many sets as there are elements (or
        // sets); an element with no set left is one deeper.
        var tree = new DepthFirstTree(Math.Min(exactCover.S.Count, exactCover.X.Count) + 1);
        var steps = NewSteps(rec, tree);

        Dictionary<int, List<int>> Y = new Dictionary<int, List<int>>();
        Dictionary<int, List<int>> X = new Dictionary<int, List<int>>();
        Dictionary<string, int> names = new Dictionary<string, int>();

        for (int i = 0; i < exactCover.S.Count; i++) {
            Y.Add(i, new List<int>());
        }

        for (int i = 0; i < exactCover.X.Count; i++) {
            names.Add(exactCover.X[i], i);
            X.Add(i, new List<int>());
        }

        for (int i = 0; i < exactCover.S.Count; i++) {
            foreach (var j in exactCover.S[i]) {
                X[names[j]].Add(i);
                Y[i].Add(names[j]);
            }
        }

        Stack<int> selectedSets = new Stack<int>();
        bool foundSolution = false;
        iterate(Y, ref X, ref selectedSets, ref foundSolution, exactCover, tree, steps);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;

        if (selectedSets.Any()) {
            // The answer's path in the tree: the sets in the order taken (the stack lists the last taken first).
            steps.Done(Picked(selectedSets), true, $"Every element is covered by exactly one chosen set: {BipartiteFrames.Braces(Picked(selectedSets).Sets)}.",
                tree: () => tree.AnswerPath(selectedSets.Reverse().Select(BipartiteFrames.SetId).ToList()));
            return solutionToCertificate(selectedSets, exactCover);
        }

        // With nothing selected, a solution was found only when X is empty: then {} is the exact cover.
        if (foundSolution)
            steps.Done(new ChosenSets([]), true, $"There are no elements, so the empty collection {{}} is an exact cover.",
                tree: () => tree.AnswerPath([]));
        else
            steps.Done(new ChosenSets([]), false, $"Every branch was undone. No exact cover exists.", tree: () => tree.NoAnswer());
        return "{}";
    }

    // The sets on the stack, by id (S1, S2, ...).
    private static ChosenSets Picked(Stack<int> solution) => new(solution.Select(BipartiteFrames.SetId));

    private void iterate(Dictionary<int, List<int>> Y, ref Dictionary<int, List<int>> X, ref Stack<int> solution, ref bool foundSolution,
        EXACTCOVER exactCover, DepthFirstTree tree, RunSteps<Move, ChosenSets> steps) {
        if (!X.Keys.Any()) foundSolution = true;
        if (foundSolution == true) return;
        else {
            // Branch on the uncovered element with the fewest sets left.
            int minimumColumn = X.OrderBy(kv => kv.Value.Count).First().Key;
            // A copy: select and deselect change this column's row list while the loop runs.
            var rows = X[minimumColumn].ToList();
            string element = exactCover.X[minimumColumn];
            if (rows.Count == 0) {
                // In the tree, the element is a dead end under the set taken last.
                tree.Branch(solution.Count + 1);
                steps.Reject(new(solution, solution.Count + 1, SearchNodeState.Pruned, Element: element),
                    $"No set left can cover {element} without doubling up, so undo.", focus: () => [BipartiteFrames.ElementId(element)]);
            }
            bool first = true;
            foreach (var row in rows) {
                if (timerHasExpired) return; // unwind at the time limit; solve() returns the timed-out answer
                solution.Push(row);
                Stack<List<int>> columns = select(Y, ref X, row);
                tree.Branch(solution.Count);
                // In the tree, the set that covers the last element is the solution.
                var tried = new Move(solution, solution.Count, X.Count == 0 ? SearchNodeState.Solution : SearchNodeState.Explored, row);
                if (first)
                    steps.Accept(tried, $"{element} has the fewest options ({rows.Count}). Try {BipartiteFrames.SetText(exactCover.S, row)}.");
                else
                    steps.Accept(tried, $"Try {BipartiteFrames.SetText(exactCover.S, row)} for {element} instead.");
                first = false;
                iterate(Y, ref X, ref solution, ref foundSolution, exactCover, tree, steps);
                if (foundSolution == true) return;
                deselect(Y, ref X, row, ref columns);
                solution.Pop();
                steps.Backtrack(new(solution, solution.Count + 1, SearchNodeState.Failed, row),
                    $"Undo {BipartiteFrames.SetId(row)}: no exact cover follows from it.", focus: () => [BipartiteFrames.SetId(row)]);
            }
        }
    }

    private Stack<List<int>> select(Dictionary<int, List<int>> Y, ref Dictionary<int, List<int>> X, int row) {
        Stack<List<int>> columns = new Stack<List<int>>();
        foreach (var j in Y[row]) {
            foreach (var i in X[j])
                foreach (var k in Y[i])
                    if (k != j)
                        X[k].Remove(i);
            columns.Push(X[j]);
            X.Remove(j);
        }
        return columns;
    }

    private void deselect(Dictionary<int, List<int>> Y, ref Dictionary<int, List<int>> X, int row, ref Stack<List<int>> columns) {
        List<int> reversed = new List<int>(Y[row]);
        reversed.Reverse();
        foreach (var j in reversed) {
            if (!X.Keys.Contains(j)) X.Add(j, new List<int>());
            X[j] = columns.Pop();
            foreach (var i in X[j])
                foreach (var k in Y[i])
                    if (k != j) {
                        if (!X.Keys.Contains(k)) X.Add(k, new List<int>());
                        X[k].Add(i);
                    }
        }
    }

    public string solutionToCertificate(Stack<int> selectedSets, EXACTCOVER exactCover) {
        string solution = "{";
        foreach (var i in selectedSets) {
            solution += "{";
            foreach (var j in exactCover.S[i]) {
                solution += j + ",";
            }
            solution = solution.TrimEnd(',') + "},";
        }
        return solution.TrimEnd(',') + "}";
    }
}
