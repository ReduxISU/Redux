using API.Interfaces.Steps;
using static API.Interfaces.Graphs.GroupedGraphFrames;

namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

// The steps 3SAT Backtracking records. For each value of the variable being decided: a Try, then an Accept (no clause is
// falsified, so the branch is queued, or every clause now has a true literal) or a Reject (a clause has no true literal
// left). The search takes the most promising queued branch next; when that is not a branch it just made, a Backtrack says
// where it went back to. The Done gives the answer, and says which variables the search never had to set (the answer sets
// them to False).
//
// Each step also names its node of the search tree, for the Search Tree picture. The root "start" comes queued with the
// first Try; each value tried is a node ("x2=F") under the state it decides: queued (with how many clauses still have no
// true literal, "2 clauses left"), pruned (with the clause it makes false), or the solution (with the variables it never
// had to set, "x3 free"). A Backtrack takes the node it goes back to off the queue, and the Done names the answer's path.
partial class Sat3BacktrackingSolver {
    private sealed class Steps(SAT3 sat3, StepRecorder<Assignment> rec) {
        // Each state's node of the search tree. The root is node 0; each value tried makes the next node.
        private readonly Dictionary<SAT3PQObject, SearchNode> _nodes = new(ReferenceEqualityComparer.Instance);
        // The root and every queued node, to walk up from the answer.
        private readonly Dictionary<int, SearchNode> _byId = new();
        private int _nextNode = 1;
        // The node of the value being tried: made by Tried, then queued, solved or pruned.
        private SearchNode _child = null!;
        // The children of the last state that were queued, to tell a Backtrack from going on with a branch just made.
        private readonly List<SAT3PQObject> _justQueued = new();
        private bool _firstState = true;
        // The answer's nodes, root first; null until the search finds it.
        private SearchNode[]? _answerPath;

        public void Root(SAT3PQObject root) {
            _nodes[root] = new SearchNode(0, null, "start", SearchNodeState.Queued);
            _byId[0] = _nodes[root];
        }

        // The search takes this state off the queue. A Backtrack when it is not one of the branches just made.
        public void Expanding(SAT3PQObject state) {
            if (!_firstState && !_justQueued.Contains(state)) {
                bool deadEnd = _justQueued.Count == 0;
                SearchNode node = _nodes[state] with { State = SearchNodeState.Explored, Bound = null };
                rec.Backtrack(() => new(ToAssignment(state.varStates),
                    (deadEnd ? "Neither value of the last variable works. " : "")
                    + $"Go back to {Assigned(state.varStates)}, the most promising branch still open: {state.nextVar} is next.") {
                    Focus = [VariableId(state.nextVar)],
                    Tree = [node],
                });
            }
            _firstState = false;
            _justQueued.Clear();
        }

        public void Tried(SAT3PQObject state, bool value) {
            SearchNode parent = _nodes[state];
            var child = new SearchNode(_nextNode++, parent.NodeId, $"{state.nextVar}={(value ? "T" : "F")}", SearchNodeState.Explored);
            _child = child;
            bool withRoot = _nextNode == 2;
            rec.Try(() => new(Partial(state, value), state.varStates.Count == 0
                ? $"Try {state.nextVar} = {value}."
                : $"Try {state.nextVar} = {value} after {Assigned(state.varStates)}.") {
                Focus = Focus(state),
                Tree = withRoot ? [parent, child] : [child],
            });
        }

        public void Queued(SAT3PQObject state, bool value, SAT3PQObject queued) {
            var child = _child;
            _justQueued.Add(queued);
            _nodes[queued] = child with { State = SearchNodeState.Queued };
            _byId[child.NodeId] = child;
            int left = queued.SATState.clauses.Count;
            rec.Accept(() => new(Partial(state, value),
                $"Keep {state.nextVar} = {value}: no clause has every literal false yet, so this branch is queued.") {
                Focus = Focus(state),
                Tree = [child with { State = SearchNodeState.Queued, Bound = $"{left} {(left == 1 ? "clause" : "clauses")} left" }],
            });
        }

        public void Satisfied(SAT3PQObject state, bool value, SAT3PQObject solution) {
            var child = _child;
            var solved = child with { State = SearchNodeState.Solution, Bound = FreeNote(FreeVariables(solution.varStates)) };
            var path = new List<SearchNode> { solved };
            for (int? up = child.ParentId; up is int id; up = _byId[id].ParentId)
                path.Insert(0, _byId[id] with { State = SearchNodeState.Explored, Bound = null });
            _answerPath = [.. path];
            rec.Accept(() => new(Partial(state, value), $"Keep {state.nextVar} = {value}: every clause now has a true literal.") {
                Focus = Focus(state),
                Tree = [solved],
            });
        }

        public void Pruned(SAT3PQObject state, bool value) {
            var child = _child;
            rec.Reject(() => {
                int clause = FalsifiedClause(state, value);
                string[] focus = Focus(state);
                return clause < 0
                    ? new(Partial(state, value), $"{state.nextVar} = {value} fails: a clause is left with no true literal.") {
                        Focus = focus,
                        Tree = [child with { State = SearchNodeState.Pruned }],
                    }
                    : new(Partial(state, value),
                        $"{state.nextVar} = {value} fails: clause C{clause + 1} {ClauseText(sat3.clauses[clause])} has no true literal left.") {
                        Focus = [focus[0], ClauseId(clause)],
                        Tree = [child with { State = SearchNodeState.Pruned, Bound = $"C{clause + 1} false" }],
                    };
            });
        }

        public void NoSolution() =>
            rec.Done(new Assignment([]), false, "Every branch was rejected, so there is no solution.");

        // The answer: the variables the search decided, and the free ones it never had to set, which the answer sets to
        // False. The Done names the answer's path, root first: past the cap, its ancestors' steps may not have been kept.
        public void Solved(Dictionary<string, bool> decided, Dictionary<string, bool> answer) {
            var path = _answerPath;
            rec.Done(ToAssignment(answer), true, $"Every clause has a true literal under {Assigned(decided)}.{FreeText(FreeVariables(decided))}",
                path is null ? null : () => path);
        }

        // The assignment so far with the value being tried added.
        private static Assignment Partial(SAT3PQObject state, bool value) =>
            ToAssignment(new Dictionary<string, bool>(state.varStates) { [state.nextVar] = value });

        private static string[] Focus(SAT3PQObject state) => [VariableId(state.nextVar)];

        // The first clause with every literal false once the state's variable takes the value.
        private int FalsifiedClause(SAT3PQObject state, bool value) =>
            FirstFalsifiedClause(sat3.clauses, new Dictionary<string, bool>(state.varStates) { [state.nextVar] = value });

        // The variables of the formula the search never had to set, in the order they first appear.
        private List<string> FreeVariables(IReadOnlyDictionary<string, bool> decided) =>
            sat3.literals.Select(l => l.TrimStart('!')).Distinct().Where(v => !decided.ContainsKey(v)).ToList();

        // What the Done says about the free variables: either value works, and the answer sets them to False.
        private static string FreeText(List<string> free) => free.Count switch {
            0 => "",
            1 => $" {free[0]} is free (either value works), so the answer sets it to False.",
            _ => $" {string.Join(", ", free.SkipLast(1))} and {free[^1]} are free (either value works), so the answer sets them to False.",
        };

        // The solution node's note: the variables it never had to set ("x3 free"), or how many when there are more than two.
        private static string? FreeNote(List<string> free) => free.Count switch {
            0 => null,
            <= 2 => $"{string.Join(", ", free)} free",
            _ => $"{free.Count} free",
        };

        private static Assignment ToAssignment(Dictionary<string, bool> values) =>
            new(values.Select(p => new KeyValuePair<string, string>(p.Key, p.Value.ToString())));
    }
}
