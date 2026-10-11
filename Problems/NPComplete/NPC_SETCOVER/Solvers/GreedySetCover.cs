using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SETCOVER.Visualizations;
using System.Numerics;

namespace API.Problems.NPComplete.NPC_SETCOVER.Solvers;

class GreedySetCover : ISolver<SETCOVER, ChosenSets>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "Greedy Set Cover";
    public string solverDefinition { get; } = "Repeatedly selects the set that covers the largest number of"
    + " currently uncovered elements, adds it to the cover, and marks its elements as covered. Continues"
    + " until all elements are covered or no set covers any remaining element. Produces an approximate"
    + " cover of size at most H(n) times the optimal, where H(n) is the n-th harmonic number, though in"
    + " practice performance is typically much closer to optimal than this worst-case bound suggests.";
    public string source { get; } = "Johnson, D. S. (1974). \"Approximation algorithms for combinatorial problems.\" Journal of Computer and System Sciences, 9(3), 256–278.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://dl.acm.org/doi/pdf/10.1145/800125.804034";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Greedy;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;

    public MathematicalFunction timeComplexity { get; } = new("n * m", ", n = |universe|, m = |subsets|");

    // --- Methods Including Constructors ---
    public GreedySetCover() {

    }

    public string solve(SETCOVER setCover) => Solve(setCover, StepRecorder<ChosenSets>.Off);

    // Steps: one per set picked, then whether the cover is complete and fits in K. Recorder state lives only in this call.
    public string Solve(SETCOVER setCover, StepRecorder<ChosenSets> rec) {

        HashSet<string> covered = new HashSet<string>();
        HashSet<string> universal = new HashSet<string>(setCover.universal);
        List<int> chosenIndices = new List<int>();
        string[] Picked() => chosenIndices.Select(BipartiteFrames.SetId).ToArray();

        while (covered.Count < universal.Count) {

            int bestIndex = -1;
            int bestNewCount = 0;

            for (int i = 0; i < setCover.subsets.Count; i++) {
                if (chosenIndices.Contains(i)) continue;

                int newCount = setCover.subsets[i].Count(e => !covered.Contains(e));
                if (newCount > bestNewCount) {
                    bestNewCount = newCount;
                    bestIndex = i;
                }
            }

            if (bestIndex == -1) break;

            chosenIndices.Add(bestIndex);
            covered.UnionWith(setCover.subsets[bestIndex]);
            rec.Accept(() => new ChosenSets(Picked()),
                () => $"Pick {BipartiteFrames.SetText(setCover.subsets, bestIndex)}: it covers {bestNewCount} new {(bestNewCount == 1 ? "element" : "elements")}, the most of any set left.",
                BipartiteFrames.SetId(bestIndex));
        }

        if (covered.Count < universal.Count) {
            // No set covers anything still uncovered, so what is left is in no set at all.
            string[] Missing() => setCover.universal.Where(e => !covered.Contains(e)).ToArray();
            rec.Reject(() => new ChosenSets(Picked()),
                () => $"{BipartiteFrames.List(Missing())} {(Missing().Length == 1 ? "is" : "are")} in no set, so no cover exists.",
                () => Missing().Select(BipartiteFrames.ElementId).ToArray());
            rec.Done(new ChosenSets([]), false, $"No cover exists: {BipartiteFrames.List(Missing())} {(Missing().Length == 1 ? "is" : "are")} in no set.");
            return "{}";
        }
        if (chosenIndices.Count > setCover.K) {
            rec.Reject(() => new ChosenSets(Picked()),
                () => $"Greedy's cover uses {chosenIndices.Count} sets, more than K = {setCover.K}.",
                () => chosenIndices.Skip(Math.Max(setCover.K, 0)).Select(BipartiteFrames.SetId).ToArray());
            rec.Done(new ChosenSets([]), false,
                $"Greedy's cover uses {chosenIndices.Count} sets, more than K = {setCover.K}. Greedy doesn't always find the smallest cover, so the brute force may still find one.");
            return "{}";
        }
        rec.Done(new ChosenSets(Picked()), true,
            $"Every element is covered with {chosenIndices.Count} {(chosenIndices.Count == 1 ? "set" : "sets")}, within K = {setCover.K}.");

        string solution = "{";
        foreach (var i in chosenIndices) {
            solution += "{" + string.Join(",", setCover.subsets[i]) + "},";
        }
        return solution.TrimEnd(',') + "}";
    }
}
