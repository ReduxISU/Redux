using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Packing;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_KNAPSACK.Solvers;
using SPADE;

namespace API.Problems.NPComplete.NPC_KNAPSACK.Visualizations;

/// <summary>
/// Knapsack as the Packing picture (format=frames): the items with their weights and values, and the knapsack with its weight
/// limit W and value goal V, step by step for the selected solver. The list format draws the set of items, the chosen ones
/// marked Solution. It is the problem's default visualization.
/// </summary>
class KnapsackVisualization : IVisualization<KNAPSACK, API_SET>, IPackingVisualization<KNAPSACK>, IOptInVisualization {
    public string visualizationName { get; } = "Knapsack Visualization";
    public string visualizationDefinition { get; } = "Draws the items with their weights and values, and the knapsack's weight against W and value against V, and shows the selected solver choosing the items.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.SetD3;
    public ISolver solver { get; } = new KnapsackBruteForce();

    // The Packing picture (format=frames only). Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(Placement);
    PackingSpec IPackingVisualization<KNAPSACK>.Describe(KNAPSACK problem) => Spec(problem);
    Placement IPackingVisualization<KNAPSACK>.ParseAnswer(KNAPSACK problem, string answer) =>
        PackingFrames.FromGroups(problem.items.ToList().Select(PackingFrames.Key).ToList(), PackingFrames.Groups(answer, nested: false));

    /// <summary>The instance as the Packing picture describes it: every (weight, value) pair is an item, in the order of
    /// <c>items</c>; W is the limit and V the goal.</summary>
    internal static PackingSpec Spec(KNAPSACK problem) =>
        new(problem.items.ToList().Select(i => new PackingItemInput(i[0].parseInt(), i[1].parseInt(), PackingFrames.Key(i))).ToList(), PackingRule.Knapsack) {
            Limit = problem.W,
            AtLeast = problem.V,
        };

    public KnapsackVisualization() { }

    public API_SET visualize(KNAPSACK problem) => new(problem.items);

    public API_JSON SolvedVisualization(KNAPSACK problem, string solution) {
        API_SET set = visualize(problem);
        var chosen = PackingFrames.Groups(solution, nested: false).SelectMany(g => g).ToHashSet();
        foreach (var item in set.data.list ?? [])
            if (chosen.Contains(PackingFrames.Key(item.id.Substring(2))))
                foreach (var number in item.list ?? []) number.color = "Solution";
        return set;
    }
}
