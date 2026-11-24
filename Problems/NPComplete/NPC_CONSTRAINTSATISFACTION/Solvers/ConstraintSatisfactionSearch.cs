using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Solvers;

// Shared depth-first engine for input-order and intelligent backtracking. Explicit
// frames replace recursive calls, keeping deep searches off the runtime call stack.
// The problem and its original domains are never modified by pruning.
internal static class ConstraintSatisfactionSearch {
    // One suspended search level: which variable is being assigned, the candidates
    // available under its ancestors' assignments, and the next candidate to attempt.
    // Standard search references the original domain; forward checking keeps a snapshot.
    private sealed class Frame(int variable, IReadOnlyList<decimal> candidates) {
        internal int Variable { get; } = variable;
        internal IReadOnlyList<decimal> Candidates { get; } = candidates;
        internal int Next { get; set; }
    }

    // Searches for the first solution. Without forward checking, useMrv chooses between
    // input order and smallest original domain. With forward checking, selection uses
    // the smallest viable candidate list. Exhaustion and timeout use Redux sentinels.
    internal static string Solve(CONSTRAINTSATISFACTION problem, bool forwardChecking, Func<bool> expired, bool useMrv = false) {
        // Pass the same cooperative cancellation check into preparation, evaluation,
        // and serialization so long expressions remain cancellable outside the DFS loop.
        void CheckTime() { if (expired()) throw new OperationCanceledException(); }
        try {
            CheckTime();
            var model = new ConstraintSatisfactionModel(problem, CheckTime);
            // Null means unassigned; zero is an ordinary domain value. At the root,
            // only constant predicates are ready. Check them even for empty problems.
            var values = new decimal?[problem.X.Count];
            if (!model.Consistent(values, CheckTime)) return "{}";
            if (values.Length == 0) return "()";
            var frames = new Stack<Frame>();
            Frame? first = Select();
            if (first == null) return "{}";
            frames.Push(first);
            while (frames.Count > 0) {
                CheckTime();
                Frame frame = frames.Peek();
                // A resumed frame still holds its last attempted value. Clear it before
                // advancing or popping so descendants never see stale assignments.
                values[frame.Variable] = null;
                if (frame.Next == frame.Candidates.Count) {
                    // The parent remains suspended with its next index already advanced.
                    // Its own previous value will be cleared on the next loop iteration.
                    frames.Pop();
                    continue;
                }
                // Reject a candidate if a ready predicate fails. Unready predicates defer
                // naturally; no guessed value is supplied for an unassigned dependency.
                values[frame.Variable] = frame.Candidates[frame.Next++];
                if (!model.Consistent(values, CheckTime)) continue;
                if (frames.Count == values.Length) {
                    CheckTime();
                    return ConstraintSatisfactionModel.Certificate(problem, values, CheckTime);
                }
                // A null child is a forward-checking domain wipeout. Leave this frame
                // on the stack and try its next candidate instead of descending.
                Frame? child = Select();
                if (child != null) frames.Push(child);
            }
            return "{}";

            // Select a new variable under the current ancestors. Temporary probes are
            // removed before returning; only assignments represented by frames persist.
            Frame? Select() {
                Frame? best = null;
                for (int i = 0; i < values.Length; i++) {
                    CheckTime();
                    if (values[i].HasValue) continue;
                    if (!forwardChecking) {
                        // Standard search returns the first unassigned variable. MRV
                        // without probing compares original domain sizes instead.
                        var frame = new Frame(i, problem.D[i]);
                        if (!useMrv) return frame;
                        if (best == null || frame.Candidates.Count < best.Candidates.Count) best = frame;
                        continue;
                    }
                    // Probe one future variable at a time. This prunes only constraints
                    // whose remaining dependencies become ready with that one value;
                    // it does not establish arc consistency between future variables.
                    var candidates = new List<decimal>();
                    foreach (decimal candidate in problem.D[i]) {
                        CheckTime();
                        values[i] = candidate;
                        if (model.Consistent(values, CheckTime)) candidates.Add(candidate);
                    }
                    values[i] = null;
                    // Recomputed at every search node, so backtracking restores domains implicitly.
                    if (candidates.Count == 0) return null;
                    // Strict improvement preserves input order as the MRV tie breaker.
                    if (best == null || candidates.Count < best.Candidates.Count)
                        best = new Frame(i, candidates);
                }
                return best;
            }
        } catch (OperationCanceledException) when (expired()) {
            // Convert only our expired-timer cancellation. Parse/arithmetic errors and
            // unrelated cancellation must propagate, never masquerade as no solution.
            return SolveRun.TimedOutAnswer;
        }
    }
}
