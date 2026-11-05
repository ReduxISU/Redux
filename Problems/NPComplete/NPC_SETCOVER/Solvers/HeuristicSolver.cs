using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System.Diagnostics;

namespace API.Problems.NPComplete.NPC_SETCOVER.Solvers;

class HeuristicSolver : ISolver<SETCOVER, ChosenSets>, IIncrementalSolver {

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

    public string solve(SETCOVER setCover) => Solve(setCover, StepRecorder<ChosenSets>.Off);

    // Steps: take a set for the uncovered element in the fewest sets, go over budget, undo a set. Recorder state lives only
    // in this call.
    public string Solve(SETCOVER setCover, StepRecorder<ChosenSets> rec) {

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
        iterate(Y, ref X, ref selectedSets, ref foundSolution, setCover.K, setCover, rec);
        stopwatch.Stop();

        Console.WriteLine(stopwatch.ElapsedMilliseconds);

        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        if (selectedSets.Any()) {
            rec.Done(Picked(selectedSets), true,
                $"{BipartiteFrames.Braces(Picked(selectedSets).Sets)} covers every element with {selectedSets.Count} {(selectedSets.Count == 1 ? "set" : "sets")}, within K = {setCover.K}.");
            return solutionToCertificate(selectedSets, setCover);
        }

        // With nothing selected, a solution was found only when U is empty: then {} is the cover.
        rec.Done(new ChosenSets([]), foundSolution,
            foundSolution ? "U is empty, so no sets are needed." : $"Every branch was undone. No cover of at most K = {setCover.K} sets exists.");
        return "{}";
    }

    // The sets on the stack, by id (S1, S2, ...).
    private static ChosenSets Picked(Stack<string> solution) => new(solution.Select(row => BipartiteFrames.SetId(int.Parse(row))));

    private void iterate(Dictionary<string, List<string>> Y, ref Dictionary<string, List<string>> X, ref Stack<string> solution, ref bool foundSolution, int K,
        SETCOVER setCover, StepRecorder<ChosenSets> rec) {
        if (timerHasExpired) return; // #935: stop at the time limit; Solve() reports it
        if (solution.Count() > K) {
            var over = solution;
            rec.Reject(() => Picked(over), () => $"That makes {over.Count} {(over.Count == 1 ? "set" : "sets")}, more than K = {K}, so undo it.",
                () => over.Count > 0 ? [BipartiteFrames.SetId(int.Parse(over.Peek()))] : []);
            return;
        }
        if (!X.Keys.Any()) foundSolution = true;
        if (foundSolution == true) return;
        else {
            string minimumColumn = X.OrderBy(kv => kv.Value.Count).First().Key;
            var rows = X[minimumColumn].OrderByDescending(e => Y[e].Count).ToList();
            var stack = solution;
            if (rows.Count == 0)
                rec.Reject(() => Picked(stack), () => $"No set contains {minimumColumn}, so no cover exists.", BipartiteFrames.ElementId(minimumColumn));
            bool first = true;
            foreach (var row in rows) {
                solution.Push(row);
                Stack<List<string>> columns = select(Y, ref X, row);
                int index = int.Parse(row);
                bool isFirst = first;
                first = false;
                rec.Accept(() => Picked(stack),
                    () => isFirst
                        ? $"{minimumColumn} is the uncovered element in the fewest sets ({rows.Count}). Take {BipartiteFrames.SetText(setCover.subsets, index)}."
                        : $"Take {BipartiteFrames.SetText(setCover.subsets, index)} for {minimumColumn} instead.",
                    BipartiteFrames.SetId(index));
                iterate(Y, ref X, ref solution, ref foundSolution, K, setCover, rec);
                if (foundSolution == true || timerHasExpired) return;
                deselect(Y, ref X, row, ref columns);
                solution.Pop();
                rec.Backtrack(() => Picked(stack), () => $"Undo {BipartiteFrames.SetId(index)}: no cover within K = {K} follows from it.", BipartiteFrames.SetId(index));
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
            // stays out. Popping it too keeps the stack in step with the columns (#774).
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
