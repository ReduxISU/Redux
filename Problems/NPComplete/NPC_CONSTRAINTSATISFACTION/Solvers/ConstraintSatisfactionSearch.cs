using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Solvers;

internal static class ConstraintSatisfactionSearch {
    private sealed class Frame(int variable, IReadOnlyList<int> candidates) {
        internal int Variable { get; } = variable;
        internal IReadOnlyList<int> Candidates { get; } = candidates;
        internal int Next { get; set; }
    }

    internal static string Solve(CONSTRAINTSATISFACTION problem, bool forwardChecking, Func<bool> expired) {
        void CheckTime() { if (expired()) throw new OperationCanceledException(); }
        try {
            CheckTime();
            var model = new ConstraintSatisfactionModel(problem, CheckTime);
            var values = new int?[problem.X.Count];
            if (!model.Consistent(values, CheckTime)) return "{}";
            if (values.Length == 0) return "{}";
            var frames = new Stack<Frame>();
            Frame? first = Select();
            if (first == null) return "{}";
            frames.Push(first);
            while (frames.Count > 0) {
                CheckTime();
                Frame frame = frames.Peek();
                values[frame.Variable] = null;
                if (frame.Next == frame.Candidates.Count) {
                    frames.Pop();
                    continue;
                }
                values[frame.Variable] = frame.Candidates[frame.Next++];
                if (!model.Consistent(values, CheckTime)) continue;
                if (frames.Count == values.Length) {
                    CheckTime();
                    return ConstraintSatisfactionModel.Certificate(problem, values);
                }
                Frame? child = Select();
                if (child != null) frames.Push(child);
            }
            return "{}";

            Frame? Select() {
                Frame? best = null;
                for (int i = 0; i < values.Length; i++) {
                    CheckTime();
                    if (values[i].HasValue) continue;
                    if (!forwardChecking) return new Frame(i, problem.D[i]);
                    var candidates = new List<int>();
                    foreach (int candidate in problem.D[i]) {
                        CheckTime();
                        values[i] = candidate;
                        if (model.Consistent(values, CheckTime)) candidates.Add(candidate);
                    }
                    values[i] = null;
                    // Recomputed at every search node, so backtracking restores domains implicitly.
                    if (candidates.Count == 0) return null;
                    if (best == null || candidates.Count < best.Candidates.Count)
                        best = new Frame(i, candidates);
                }
                return best;
            }
        } catch (OperationCanceledException) when (expired()) {
            return SolveRun.TimedOutAnswer;
        }
    }
}
