using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System.Diagnostics;

namespace API.Problems.NPComplete.NPC_SETCOVER.Solvers;

// The steps Set Cover Algorithm X Bounded Search records, for the Bipartite Graph picture and the Search Tree picture
// alike:
//   - an Accept per set it takes for the uncovered element in the fewest sets (the first set saying which element that is,
//     the others that they are tried for it instead); in the tree, a node "S2" under the set taken before it, the solution
//     when it covers the last element within K;
//   - a Reject when the sets taken go over K (in the tree, the set taken last is pruned, noted "over K = 2"), or when an
//     uncovered element is in no set left (a pruned node named after the element, noted "in no set");
//   - a Backtrack per set it takes back (that node fails, or stays pruned when it went over K);
//   - the Done, with the cover or why there is none; it names the answer's path in the tree, root first, or marks the root
//     failed.
// In the tree, a node is a set taken, and its children are the sets tried for the next element.
class HeuristicSolver : ISolver<SETCOVER, ChosenSets>, IIncrementalSolver, ISearchTreeSolver {

    // --- Fields ---
    public string solverName { get; } = "Set Cover Algorithm X Bounded Search";
    public string solverDefinition { get; } = "Builds a sparse bipartite row/column representation of the"
    + " subsets-vs-universal-elements matrix. At each step, selects the column (universal element) with the"
    + " fewest covering rows (subsets) remaining, then branches over each row that covers it, in descending"
    + " order of row size. Selecting a row removes its columns (and all rows sharing them) from the matrix;"
    + " backtracking restores them. Recursion terminates successfully when no columns remain, and is bounded"
    + " to reject any partial solution exceeding K selected sets. Implements Algorithm X's selection and"
    + " backtracking logic using plain dictionaries/lists rather than the dancing-links doubly-linked-list"
    + " structure, so it lacks DLX's O(1) undo per step.";
    public string source { get; } = "Knuth, D. E. (2000). Dancing links. In Millennium Perspectives in Computer"
    + " Science, 187-214. arXiv:cs/0011047.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://arxiv.org/abs/cs/0011047";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Despite the class name, this is NOT a heuristic: it is exact bounded
    // backtracking (min-column-selection + push/pop stack) that gives no-worse-than-optimal-of-size-K
    // guarantees when it terminates. Tagged Backtracking, not Heuristic -- catching this misleading
    // class name is exactly what a declared (not derived) tag system is for.
    public SolverType solverType { get; } = SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Exact backtracking bounded to depth K: branching factor is bounded
    // by the number of subsets s covering the current minimum column, and recursion is cut off
    // once the partial solution exceeds K sets. Per-node cost is dominated by the X.OrderBy scan
    // over remaining columns (O(u log u), u = |universal|).
    public MathematicalFunction timeComplexity { get; } = new("s^K * u log u", ", s = |subsets|, u = |universal|, K = target cover size");

    // --- Methods Including Constructors ---
    public HeuristicSolver() {

    }

    // A step of the search: its stack of sets taken (positions in S, as strings; the last taken on top), and the
    // search-tree node it is about, at Depth (0 for the root, d for the d-th set taken on the branch), left in state Tree:
    // the set Row (null: the root), or the element Element that is in no set.
    private readonly record struct Move(Stack<string> Taken, int Depth, SearchNodeState Tree, string? Row = null, string? Element = null);

    // How the steps show a move. The search keeps its own tree (a "start" root, a set over K and an element in no set as
    // pruned nodes, a set failed when it is taken back), so it gives RunSteps the tree node of each step rather than using
    // SearchSteps.
    private static RunSteps<Move, ChosenSets> NewSteps(StepRecorder<ChosenSets> rec, int K, DepthFirstTree tree) => new(rec,
        // What every picture shows: the sets taken, and the set taken last.
        partial: m => Picked(m.Taken),
        focus: m => m.Taken.Count > 0 ? [BipartiteFrames.SetId(int.Parse(m.Taken.Peek()))] : [],
        // The Search Tree picture's node only: the set taken ("S2"), noted when it goes over K, or the element in no set.
        tree: m => m.Element != null ? tree.Step(m.Depth, m.Element, m.Tree, "in no set")
            : tree.Step(m.Depth, m.Row == null ? "" : BipartiteFrames.SetId(int.Parse(m.Row)), m.Tree, m.Tree == SearchNodeState.Pruned ? $"over K = {K}" : null));

    public string solve(SETCOVER setCover) => Solve(setCover, StepRecorder<ChosenSets>.Off);

    // Algorithm X with a bound: X maps each uncovered element (column) to the sets (rows) that contain it, Y each set to its
    // elements. The search takes, one at a time, a set for the uncovered element in the fewest sets, and backs up when it
    // goes over K sets.
    public string Solve(SETCOVER setCover, StepRecorder<ChosenSets> rec) {
        // A branch takes each set at most once and stops one set past K; an element in no set is one deeper than the sets
        // taken within K.
        var tree = new DepthFirstTree(Math.Max(0, Math.Min(setCover.K, setCover.subsets.Count)) + 1);
        var steps = NewSteps(rec, setCover.K, tree);

        Dictionary<string, List<string>> Y = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> X = new Dictionary<string, List<string>>();

        for (int i = 0; i < setCover.subsets.Count; i++) {
            Y.Add(i.ToString(), new List<string>());
        }

        for (int i = 0; i < setCover.universal.Count; i++) {
            X.Add(setCover.universal[i], new List<string>());
        }

        for (int i = 0; i < setCover.subsets.Count; i++) {
            foreach (var j in setCover.subsets[i]) {
                X[j].Add(i.ToString());
                Y[i.ToString()].Add(j);
            }
        }

        Stack<string> selectedSets = new Stack<string>();
        bool foundSolution = false;
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        iterate(Y, ref X, ref selectedSets, ref foundSolution, setCover.K, setCover, tree, steps);
        stopwatch.Stop();

        Console.WriteLine(stopwatch.ElapsedMilliseconds);

        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        if (selectedSets.Any()) {
            // The answer's path in the tree: the sets in the order taken (the stack lists the last taken first).
            steps.Done(Picked(selectedSets), true,
                $"{BipartiteFrames.Braces(Picked(selectedSets).Sets)} covers every element with {Words.Count(selectedSets.Count, "set")}, within K = {setCover.K}.",
                tree: () => tree.AnswerPath(selectedSets.Reverse().Select(row => BipartiteFrames.SetId(int.Parse(row))).ToList()));
            return solutionToCertificate(selectedSets, setCover);
        }

        // With nothing selected, a solution was found only when U is empty: then {} is the cover.
        if (foundSolution)
            steps.Done(new ChosenSets([]), true, $"U is empty, so no sets are needed.", tree: () => tree.AnswerPath([]));
        else
            steps.Done(new ChosenSets([]), false, $"Every branch was undone. No cover of at most K = {setCover.K} sets exists.", tree: () => tree.NoAnswer());
        return "{}";
    }

    // The sets on the stack, by id (S1, S2, ...).
    private static ChosenSets Picked(Stack<string> solution) => new(solution.Select(row => BipartiteFrames.SetId(int.Parse(row))));

    private void iterate(Dictionary<string, List<string>> Y, ref Dictionary<string, List<string>> X, ref Stack<string> solution, ref bool foundSolution, int K,
        SETCOVER setCover, DepthFirstTree tree, RunSteps<Move, ChosenSets> steps) {
        if (timerHasExpired) return; // stop at the time limit; Solve() reports it
        // Over the bound: this branch can't give a cover of at most K sets.
        if (solution.Count() > K) {
            steps.Reject(new(solution, solution.Count, SearchNodeState.Pruned, solution.Count > 0 ? solution.Peek() : null),
                $"That makes {Words.Count(solution.Count, "set")}, more than K = {K}, so undo it.");
            return;
        }
        if (!X.Keys.Any()) foundSolution = true;
        if (foundSolution == true) return;
        else {
            // Branch on the uncovered element in the fewest sets, trying its biggest sets first.
            string minimumColumn = X.OrderBy(kv => kv.Value.Count).First().Key;
            var rows = X[minimumColumn].OrderByDescending(e => Y[e].Count).ToList();
            if (rows.Count == 0) {
                // In the tree, the element is a dead end under the set taken last.
                tree.Branch(solution.Count + 1);
                steps.Reject(new(solution, solution.Count + 1, SearchNodeState.Pruned, Element: minimumColumn),
                    $"No set contains {minimumColumn}, so no cover exists.", focus: () => [BipartiteFrames.ElementId(minimumColumn)]);
            }
            bool first = true;
            foreach (var row in rows) {
                solution.Push(row);
                Stack<List<string>> columns = select(Y, ref X, row);
                int index = int.Parse(row);
                tree.Branch(solution.Count);
                // In the tree, the set that covers the last element within K is the solution.
                var taken = new Move(solution, solution.Count, X.Count == 0 && solution.Count <= K ? SearchNodeState.Solution : SearchNodeState.Explored, row);
                if (first)
                    steps.Accept(taken, $"{minimumColumn} is the uncovered element in the fewest sets ({rows.Count}). Take {BipartiteFrames.SetText(setCover.subsets, index)}.");
                else
                    steps.Accept(taken, $"Take {BipartiteFrames.SetText(setCover.subsets, index)} for {minimumColumn} instead.");
                first = false;
                iterate(Y, ref X, ref solution, ref foundSolution, K, setCover, tree, steps);
                if (foundSolution == true || timerHasExpired) return;
                deselect(Y, ref X, row, ref columns);
                solution.Pop();
                // In the tree, the set fails, unless it went over K (the tree keeps it pruned, with its note).
                steps.Backtrack(new(solution, solution.Count + 1, SearchNodeState.Failed, row),
                    $"Undo {BipartiteFrames.SetId(index)}: no cover within K = {K} follows from it.", focus: () => [BipartiteFrames.SetId(index)]);
            }
        }
    }

    private Stack<List<string>> select(Dictionary<string, List<string>> Y, ref Dictionary<string, List<string>> X, string row) {
        Stack<List<string>> columns = new Stack<List<string>>();
        foreach (var j in Y[row]) {
            if (X.Keys.Contains(j)) {
                columns.Push(X[j]);
                X.Remove(j);
            } else {
                columns.Push(new List<string>());
            }
        }

        return columns;
    }

    private void deselect(Dictionary<string, List<string>> Y, ref Dictionary<string, List<string>> X, string row, ref Stack<List<string>> columns) {
        List<string> reversed = new List<string>(Y[row]);
        reversed.Reverse();
        foreach (var j in reversed) {
            // Every column of the row was pushed, in order; an empty list stands for one that was already covered, so it
            // stays out. Popping it too keeps the stack in step with the columns.
            List<string> column = columns.Pop();
            if (column.Any()) X.Add(j, column);
        }
    }

    public string solutionToCertificate(Stack<string> selectedSets, SETCOVER setCover) {
        string solution = "{";
        foreach (var i in selectedSets) {
            solution += "{";
            foreach (var j in setCover.subsets[Int32.Parse(i)]) {
                solution += j + ",";
            }
            solution = solution.TrimEnd(',') + "},";
        }
        return solution.TrimEnd(',') + "}";
    }
}
