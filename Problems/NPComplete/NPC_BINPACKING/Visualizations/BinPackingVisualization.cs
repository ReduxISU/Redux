using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Packing;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_BINPACKING.Solvers;
using SPADE;

namespace API.Problems.NPComplete.NPC_BINPACKING.Visualizations;

/// <summary>
/// Bin Packing as the Packing picture (format=frames): the item sizes, and the bins of capacity C (at most K), step by step
/// for the selected solver. The list format draws the list of sizes, all marked Solution once they are packed. It is the
/// problem's default visualization.
/// </summary>
class BinPackingVisualization : IVisualization<BINPACKING, API_SET>, IPackingVisualization<BINPACKING> {
    public string visualizationName { get; } = "Bin Packing Visualization";
    public string visualizationDefinition { get; } = "Draws the item sizes and the bins, each with its capacity C, and shows the selected solver putting each item in a bin.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.SetD3;
    public ISolver solver { get; } = new BinPackingBruteForce();

    // The Packing picture (format=frames only). Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(Placement);
    PackingSpec IPackingVisualization<BINPACKING>.Describe(BINPACKING problem) => Spec(problem);
    Placement IPackingVisualization<BINPACKING>.ParseAnswer(BINPACKING problem, string answer) => ParseAnswer(problem, answer);

    /// <summary>The instance as the Packing picture describes it: every size of S is an item; C is each bin's limit and K the
    /// number of bins.</summary>
    internal static PackingSpec Spec(BINPACKING problem) =>
        new(problem.S.Select(x => new PackingItemInput(x, null, PackingFrames.Num(x))).ToList(), PackingRule.BinPacking) {
            Limit = problem.C,
            Bins = problem.K,
        };

    // An answer ((4,6),(7,3)) as a placement: the first bin is container 0, and so on.
    private static Placement ParseAnswer(BINPACKING problem, string answer) =>
        PackingFrames.FromGroups(problem.S.Select(PackingFrames.Num).ToList(), PackingFrames.Groups(answer, nested: true));

    public BinPackingVisualization() { }

    public API_SET visualize(BINPACKING problem) => new(new UtilCollection("(" + string.Join(",", problem.S) + ")"));

    public API_JSON SolvedVisualization(BINPACKING problem, string solution) {
        API_SET list = visualize(problem);
        var placement = ParseAnswer(problem, solution);
        var entries = list.data.list ?? [];
        for (int i = 0; i < entries.Count; i++)
            if (placement.Of(i) >= 0) entries[i].color = "Solution";
        return list;
    }
}
