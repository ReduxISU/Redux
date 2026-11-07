using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SETCOVER.Visualizations;
using System.Numerics;

namespace API.Problems.NPComplete.NPC_SETCOVER.Solvers;

// The steps Greedy Set Cover records:
//   - an Accept per set it picks, the one covering the most elements still uncovered, saying how many;
//   - a Reject when what is left to cover is in no set at all, or when the cover it built uses more than K sets;
//   - the Done, with the cover or why there is none.
// Incremental: a picked set stays picked.
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

    // How the steps show the cover so far, given by the positions in S of the sets picked, in the order they were picked.
    private static RunSteps<List<int>, ChosenSets> NewSteps(StepRecorder<ChosenSets> rec) => new(rec,
        // What every picture shows: the sets picked so far, and the set just picked.
        partial: chosen => new ChosenSets(chosen.Select(SetPieces.SetId)),
        focus: chosen => chosen.Count > 0 ? [SetPieces.SetId(chosen[^1])] : []);

    public string solve(SETCOVER setCover) => Solve(setCover, StepRecorder<ChosenSets>.Off);

    // Until every element is covered, picks the set that covers the most elements still uncovered (the first such set on a
    // tie). Stops early when no set covers anything new.
    public string Solve(SETCOVER setCover, StepRecorder<ChosenSets> rec) {

        HashSet<string> covered = new HashSet<string>();
        HashSet<string> universal = new HashSet<string>(setCover.universal);
        List<int> chosenIndices = new List<int>();
        var steps = NewSteps(rec);

        while (covered.Count < universal.Count) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // stop at the time limit

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
            steps.Accept(chosenIndices,
                $"Pick {SetPieces.SetText(setCover.subsets, bestIndex)}: it covers {bestNewCount} new {(bestNewCount == 1 ? "element" : "elements")}, the most of any set left.");
        }

        if (covered.Count < universal.Count) {
            // No set covers anything still uncovered, so what is left is in no set at all (named only for a kept step).
            string[] Missing() => setCover.universal.Where(e => !covered.Contains(e)).ToArray();
            steps.Reject(chosenIndices, $"{SetPieces.List(Missing())} {(Missing().Length == 1 ? "is" : "are")} in no set, so no cover exists.",
                focus: () => Missing().Select(SetPieces.ElementId).ToArray());
            steps.Done(new ChosenSets([]), false, $"No cover exists: {SetPieces.List(Missing())} {(Missing().Length == 1 ? "is" : "are")} in no set.");
            return "{}";
        }
        if (chosenIndices.Count > setCover.K) {
            // The step is about the sets past the K-th, the ones the cover has too many of.
            steps.Reject(chosenIndices, $"Greedy's cover uses {chosenIndices.Count} sets, more than K = {setCover.K}.",
                focus: () => chosenIndices.Skip(Math.Max(setCover.K, 0)).Select(SetPieces.SetId).ToArray());
            steps.Done(new ChosenSets([]), false,
                $"Greedy's cover uses {chosenIndices.Count} sets, more than K = {setCover.K}. Greedy doesn't always find the smallest cover, so the brute force may still find one.");
            return "{}";
        }
        steps.Done(new ChosenSets(chosenIndices.Select(SetPieces.SetId)), true,
            $"Every element is covered with {Words.Count(chosenIndices.Count, "set")}, within K = {setCover.K}.");

        string solution = "{";
        foreach (var i in chosenIndices) {
            solution += "{" + string.Join(",", setCover.subsets[i]) + "},";
        }
        return solution.TrimEnd(',') + "}";
    }
}
