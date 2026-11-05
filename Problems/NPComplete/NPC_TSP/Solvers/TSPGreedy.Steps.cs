using API.Interfaces.Steps;
using static API.Interfaces.Graphs.GraphTourFrames;

namespace API.Problems.NPComplete.NPC_TSP.Solvers;

// The steps Traveling Salesperson Greedy records. For each start city: a Try to start there, an Accept for every
// nearest-neighbour move (with the running path cost), then either a Reject (a dead end, a closing edge the graph does
// not have, or a closed tour that is over K or no better than the best so far) or an Accept for a closed tour that is the
// best so far. The Done gives the best tour.
partial class TSPGreedy {
    private sealed class Steps(TSP tsp, StepRecorder<Tour> rec) {
        public void NoCities() => rec.Done(new Tour([]), false, "There are no cities to tour.");

        public void Started(List<string> route, string start) =>
            rec.Try(() => new(new Tour(route), $"Start at {start}.") { Focus = [start] });

        public void DeadEnd(List<string> route, string stuckAt, string start) =>
            rec.Reject(() => new(new Tour(route),
                $"Dead end at {stuckAt}: it has no edge to a city that is not already on the path, so starting from {start} fails.") {
                Focus = [stuckAt],
            });

        public void Moved(List<string> route, string from, string to, int weight, int costSoFar) =>
            rec.Accept(() => new(new Tour(route),
                $"Go {from} → {to} (weight {weight}, path cost {costSoFar}): the nearest city not yet visited.") {
                Focus = [to, EdgeId(false, from, to)],
            });

        public void NoWayBack(List<string> route, string last, string start) =>
            rec.Reject(() => new(new Tour(route.Append(start)),
                $"Close the tour {last} → {start}: there is no such edge, so starting from {start} fails.") {
                Focus = [last, start],
            });

        // The closed route (it ends at its start) is the best tour so far.
        public void BestSoFar(List<string> route, string start, int total, int previousBest) =>
            rec.Accept(() => new(new Tour(route),
                $"Close the tour {route[^2]} → {start}: total {total}, the best so far{(previousBest == int.MaxValue ? "" : $" (it beats {previousBest})")}.") {
                Focus = [route[^2], start],
            });

        // The closed route is over K, or no better than the best so far.
        public void NotBest(List<string> route, string start, int total, int best, bool withinK) =>
            rec.Reject(() => new(new Tour(route), withinK
                ? $"Close the tour {route[^2]} → {start}: total {total} is no better than the best so far ({best})."
                : $"Close the tour {route[^2]} → {start}: total {total} is more than K = {tsp.K}.") {
                // The route was built hop by hop, so what failed is the closing hop, not the whole tour.
                Focus = [route[^2], start, EdgeId(false, route[^2], start)],
            });

        public void Finished(string bestCertificate, int bestTotal) {
            if (bestCertificate == "{}") {
                rec.Done(new Tour([]), false, $"No start gave a tour within K = {tsp.K}.");
            } else {
                rec.Done(ParseTour(bestCertificate), true,
                    $"The best tour is {Arrows(ParseTour(bestCertificate).Order)}, total {bestTotal}.");
            }
        }
    }
}
