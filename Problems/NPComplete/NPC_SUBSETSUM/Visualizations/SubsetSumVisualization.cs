using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Packing;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SUBSETSUM.Solvers;
using SPADE;

namespace API.Problems.NPComplete.NPC_SUBSETSUM.Visualizations;

/// <summary>
/// Subset Sum as the Packing picture (format=frames): the numbers as items and the chosen ones in one container with the
/// target line T, step by step for the selected solver. The list format draws the set of numbers, the chosen ones marked
/// Solution. It is the problem's default visualization.
/// </summary>
class SubsetSumVisualization : IVisualization<SUBSETSUM, API_SET>, IPackingVisualization<SUBSETSUM> {
    public string visualizationName { get; } = "Subset Sum Visualization";
    public string visualizationDefinition { get; } = "Draws the numbers as items and the chosen ones as a bar against the target T, and shows the selected solver trying subsets until one adds up to exactly T.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.SetD3;
    public ISolver solver { get; } = new SubsetSumBruteForce();

    // The Packing picture (format=frames only). Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(Placement);
    PackingSpec IPackingVisualization<SUBSETSUM>.Describe(SUBSETSUM problem) => Spec(problem);
    Placement IPackingVisualization<SUBSETSUM>.ParseAnswer(SUBSETSUM problem, string answer) =>
        PackingFrames.FromGroups(problem.S.Select(PackingFrames.Key).ToList(), PackingFrames.Groups(answer, nested: false));

    /// <summary>The instance as the Packing picture describes it: every number of S is an item, and T is the target.</summary>
    internal static PackingSpec Spec(SUBSETSUM problem) =>
        new(problem.S.Select(x => new PackingItemInput(int.Parse(x), null, x)).ToList(), PackingRule.SubsetSum) { Target = problem.T };

    public SubsetSumVisualization() { }

    // Every number of S, in order, repeats included (#628): a later copy gets its position in its id (r-7-1).
    public API_SET visualize(SUBSETSUM problem) => API_SET.KeepingRepeats(problem.S);

    // The chosen numbers by position (#628): {7} for S = {7,7,1} marks the first 7 only, {7,7} both.
    public API_JSON SolvedVisualization(SUBSETSUM problem, string solution) {
        API_SET set = visualize(problem);
        Placement chosen = PackingFrames.FromGroups(problem.S.Select(PackingFrames.Key).ToList(), PackingFrames.Groups(solution, nested: false));
        var list = set.data.list ?? [];
        if (list.Count != problem.S.Count) return set;
        for (int i = 0; i < list.Count; i++)
            if (chosen.Of(i) == 0) list[i].color = "Solution";
        return set;
    }
}
