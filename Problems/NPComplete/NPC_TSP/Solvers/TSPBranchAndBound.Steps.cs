using API.Interfaces.Steps;
using static API.Interfaces.Graphs.GraphTourFrames;
using static API.Problems.NPComplete.NPC_TSP.Steps.TSPTourSteps;

namespace API.Problems.NPComplete.NPC_TSP.Solvers;

// The steps Traveling Salesperson Branch and Bound records, in the order the search runs (best first, so the path on
// screen jumps between branches). The greedy tour the search starts from is the first step; the greedy solver's own steps
// are not recorded. Then a Try for each path taken off the queue to be expanded; for each way of extending it, an Accept
// when the branch is queued, or a Reject when there is no such edge or its lower bound is already no better than the best
// tour so far; a Reject for a queued path dropped because the best tour so far has improved since it was queued; and for a
// complete tour, an Accept if it beats the best so far or a Reject if not.
//
// Each step also names its node of the search tree, for the Search Tree picture. The root, the start city, comes queued
// with the first step, and the greedy tour with it as a leaf of its own ("greedy tour", the solution so far, so the best
// tour every bound is checked against is in the tree). A path taken off the queue is explored, or pruned if the best tour
// has caught up with its bound; each extension is a node under its path, queued with its lower bound or pruned (no such
// edge, or its bound is no better); a complete tour is the solution (the best so far) or failed. The Done names the best
// tour's path again, root first, or the greedy tour's leaf when the search never beat it.
partial class TSPBranchAndBound {
    private sealed class Steps(TSP problem, StepRecorder<Tour> rec) {
        private List<string> Cities => problem.nodes;
        // The original cost matrix, for the weights in captions.
        private double[,] _costs = new double[0, 0];
        // The root is node 0; every other node, the greedy tour's leaf and each extension considered, gets the next id.
        private int _nextNode = 1;
        // The greedy tour's leaf, the answer when the search never beats it.
        private SearchNode? _greedyNode;
        // The best tour's path in the tree, once the search beats the greedy tour.
        private SearchNode[]? _bestNodePath;

        public void OutOfTime() => rec.Done(new Tour([]), false, "Ran out of time before the search finished.");

        public void NoCities() => rec.Done(new Tour([]), false, "There are no cities to tour.");

        // The first step: the greedy tour, the best so far, or the start city when the greedy solver found none.
        public void Started(SearchState root, List<int>? greedy, double greedyCost, double[,] costs) {
            _costs = costs;
            var rootNode = new SearchNode(0, null, Cities[0], SearchNodeState.Queued) { Bound = $"lb {Num(root.bound)}" };
            root.treePath = [rootNode];
            if (greedy is not null) {
                var greedyNode = new SearchNode(_nextNode++, rootNode.NodeId, "greedy tour", SearchNodeState.Solution) { Bound = $"tour {Num(greedyCost)}" };
                _greedyNode = greedyNode;
                rec.Accept(() => new(new Tour(Names(greedy, close: true)),
                    $"Start from the greedy tour {Arrows(Names(greedy, close: true))}: total {Num(greedyCost)}, the best so far.") {
                    Focus = EdgeIds(problem, Names(greedy, close: true)),
                    Tree = [rootNode, greedyNode],
                });
            } else {
                rec.Try(() => new(new Tour([Cities[0]]),
                    $"The greedy solver found no tour to start from, so there is no best so far. Start at {Cities[0]}.") {
                    Focus = [Cities[0]],
                    Tree = [rootNode],
                });
            }
        }

        // A queued path whose lower bound the best tour has caught up with since it was queued.
        public void Dropped(SearchState state, double best) =>
            rec.Reject(() => new(new Tour(Names(state.path!)),
                $"Drop the path {Arrows(Names(state.path!))}: its lower bound {Num(state.bound)} is no better than the best tour so far ({Num(best)}).") {
                Focus = [Cities[state.path![^1]]],
                Tree = [Node(state) with { State = SearchNodeState.Pruned, Bound = $"lb {Num(state.bound)} ≥ {Num(best)}" }],
            });

        public void BetterTour(SearchState state, double tourCost, double previousBest) {
            _bestNodePath = state.treePath;
            var (last, first) = ClosingHop(state);
            rec.Accept(() => new(new Tour(Names(state.path!, close: true)),
                $"Close the tour {last} → {first}: total {Num(tourCost)}, the best so far{(double.IsPositiveInfinity(previousBest) ? "" : $" (it beats {Num(previousBest)})")}.") {
                Focus = [last, first],
                Tree = [Node(state) with { State = SearchNodeState.Solution, Bound = $"tour {Num(tourCost)}" }],
            });
        }

        public void NoBetterTour(SearchState state, double tourCost, double best) {
            var (last, first) = ClosingHop(state);
            bool noWayBack = double.IsPositiveInfinity(tourCost);
            rec.Reject(() => new(new Tour(Names(state.path!, close: true)), noWayBack
                ? $"Close the tour {last} → {first}: there is no such edge."
                : $"Close the tour {last} → {first}: total {Num(tourCost)} is no better than the best so far ({Num(best)}).") {
                // The path was built hop by hop, so what failed is the closing hop, not the whole tour.
                Focus = noWayBack ? [last, first] : [last, first, EdgeId(false, last, first)],
                Tree = [Node(state) with { State = SearchNodeState.Failed, Bound = noWayBack ? "no way back" : $"tour {Num(tourCost)}" }],
            });
        }

        public void Expanding(SearchState state) =>
            rec.Try(() => new(new Tour(Names(state.path!)), $"Expand the path {Arrows(Names(state.path!))}: lower bound {Num(state.bound)}.") {
                Focus = [Cities[state.path![^1]]],
                Tree = [Node(state) with { State = SearchNodeState.Explored, Bound = null }],
            });

        public void NoEdge(SearchState state, int next) {
            var node = NewChild(state, next) with { State = SearchNodeState.Pruned, Bound = "no edge" };
            rec.Reject(() => new(new Tour(Names(state.path!).Append(Cities[next])),
                $"There is no edge between {Cities[state.path![^1]]} and {Cities[next]}.") {
                Focus = [Cities[next]],
                Tree = [node],
            });
        }

        // An extension whose lower bound is already no better than the best tour so far.
        public void Skipped(SearchState state, int next, double childBound, double best) {
            var node = NewChild(state, next);
            string last = Cities[state.path![^1]];
            rec.Reject(() => new(new Tour(Names(state.path!).Append(Cities[next])),
                $"Skip {Cities[next]} after {last}: the lower bound {Num(childBound)} is no better than the best tour so far ({Num(best)}).") {
                Focus = [Cities[next], EdgeId(false, last, Cities[next])],
                Tree = [node with { State = SearchNodeState.Pruned, Bound = $"lb {Num(childBound)} ≥ {Num(best)}" }],
            });
        }

        public void Queued(SearchState state, SearchState child) {
            int last = state.path![^1], next = child.path![^1];
            var node = NewChild(state, next) with { Bound = $"lb {Num(child.bound)}" };
            child.treePath = [.. state.treePath, node];
            rec.Accept(() => new(new Tour(Names(child.path!)),
                $"Branch {Cities[last]} → {Cities[next]} (weight {Num(_costs[last, next])}, path cost {Num(PathCost(child.path!))}): lower bound {Num(child.bound)}, queued.") {
                Focus = [Cities[next], EdgeId(false, Cities[last], Cities[next])],
                Tree = [node],
            });
        }

        // The answer. Past the cap, the last Accept kept need not be the one that found it, so the Done names its path in the
        // tree; when the search never beat the greedy tour, the answer is the greedy tour's leaf, and the caption says so.
        public void Found(List<int> best, double cost, string certificate) {
            var bestNodes = _bestNodePath;
            var greedyLeaf = _greedyNode;
            rec.Done(ParseTour(certificate), true,
                bestNodes is null
                    ? $"The cheapest tour is the greedy tour {Arrows(Names(best, close: true))}, total {Num(cost)}, within K = {problem.K}: the search found nothing cheaper."
                    : $"The cheapest tour is {Arrows(Names(best, close: true))}, total {Num(cost)}, within K = {problem.K}.",
                bestNodes is null ? (greedyLeaf is null ? null : () => [greedyLeaf]) : () => [
                    .. bestNodes[..^1].Select(x => x with { State = SearchNodeState.Explored, Bound = null }),
                    bestNodes[^1] with { State = SearchNodeState.Solution, Bound = $"tour {Num(cost)}" },
                ]);
        }

        public void OverK(double cost) =>
            rec.Done(new Tour([]), false, $"The cheapest tour costs {Num(cost)}, which is more than K = {problem.K}.");

        public void NoTour() => rec.Done(new Tour([]), false, "No tour visits every city once.");

        // The state's own node: the last of its path in the tree.
        private static SearchNode Node(SearchState state) => state.treePath[^1];

        // A new node for extending the state's path to city `next`, under the state's node.
        private SearchNode NewChild(SearchState state, int next) =>
            new(_nextNode++, Node(state).NodeId, Cities[next], SearchNodeState.Queued);

        // The hop that closes a complete path: from its last city back to its first.
        private (string Last, string First) ClosingHop(SearchState state) => (Cities[state.path![^1]], Cities[state.path[0]]);

        // The city names of a path, closed back to its start when `close` is set.
        private List<string> Names(List<int> path, bool close = false) {
            var names = path.Select(i => Cities[i]).ToList();
            if (close) names.Add(Cities[path[0]]);
            return names;
        }

        // The weight of a path's steps (not counting the way back to the start).
        private double PathCost(List<int> path) {
            double total = 0;
            for (int i = 0; i + 1 < path.Count; i++) total += _costs[path[i], path[i + 1]];
            return total;
        }
    }
}
