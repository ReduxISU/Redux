using API.Interfaces.Steps;
using static API.Interfaces.Graphs.GraphTourFrames;
using static API.Problems.NPComplete.NPC_TSP.Steps.TSPTourSteps;

namespace API.Problems.NPComplete.NPC_TSP.Solvers;

// The steps Traveling Salesperson Brute Force records: one per complete permutation, in the order the solver tries them
// (the same permutation can come round as several rotations and reversals; that is what a brute force does). A
// permutation that is not a tour within K is a Reject that says why: the first missing edge, or a total over K. The first
// one that is within K is the answer, an Accept, so the run stops there. There are n! permutations, far more than the
// recorder's cap on all but tiny instances, so only the first ones are kept and the Done says how many are not shown.
partial class TSPBruteForce {
    private sealed class Steps(TSP tsp, StepRecorder<Tour> rec) {
        // There is one empty "permutation", whose certificate is "{}"; the answer is "{}" either way, as it always was.
        public void NoCities() {
            bool ok = false;
            try { ok = tsp.defaultVerifier.verify(tsp, "{}"); } catch { }
            rec.Done(new Tour([]), ok, "There are no cities to tour.");
        }

        public void Found(string certificate, int tried) {
            rec.Accept(() => {
                var closed = ParseTour(certificate).Order;
                return new(ParseTour(certificate), $"Try {Arrows(closed)}: total {Num(Cost(tsp, closed) ?? 0)} is within K = {tsp.K}, so this is a tour.") {
                    Focus = EdgeIds(tsp, closed),
                };
            });
            rec.Done(ParseTour(certificate), true, $"Found a tour within K = {tsp.K} after trying {tried} {(tried == 1 ? "permutation" : "permutations")}.");
        }

        public void Rejected(string certificate) {
            rec.Reject(() => {
                var closed = ParseTour(certificate).Order;
                var missing = FirstMissingHop(tsp, closed);
                string why = missing is { } hop
                    ? $"there is no edge between {hop.From} and {hop.To}"
                    : $"total {Num(Cost(tsp, closed) ?? 0)} is more than K = {tsp.K}";
                return new(ParseTour(certificate), $"Try {Arrows(closed)}: {why}.") {
                    Focus = missing is { } h ? [h.From, h.To] : EdgeIds(tsp, closed),
                };
            });
        }

        public void NoneWithinK(int tried) =>
            rec.Done(new Tour([]), false, $"Tried all {tried} permutations of the cities. None is a tour within K = {tsp.K}.");
    }
}
