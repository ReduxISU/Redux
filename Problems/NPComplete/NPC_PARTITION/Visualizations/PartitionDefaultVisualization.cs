using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.Packing;
using API.Interfaces.Steps;
using SPADE;
using API.Problems.NPComplete.NPC_PARTITION.Solvers;
using API.Problems.NPComplete.NPC_PARTITION.Verifiers;

namespace API.Problems.NPComplete.NPC_PARTITION.Visualizations;

class PartitionDefaultVisualization : IVisualization<PARTITION, API_SET>, IPackingVisualization<PARTITION> {
    public string visualizationName { get; } = "Partition Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Partition";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public VisualizationType visualizationType { get; } = VisualizationType.SetD3;
    public ISolver solver { get; } = new PartitionBruteForce();

    // The Packing picture (format=frames only; /info keeps SetD3). Implemented explicitly so it is not part of /info.
    // The list format gets none of the typed steps (it never drew steps), so it is unchanged.
    Type? IVisualization.StepShape => typeof(Placement);
    PackingSpec IPackingVisualization<PARTITION>.Describe(PARTITION problem) => Spec(problem);
    Placement IPackingVisualization<PARTITION>.ParseAnswer(PARTITION problem, string answer) =>
        PackingFrames.FromGroups(problem.S.Select(PackingFrames.Key).ToList(), PackingFrames.Groups(answer, nested: true));

    /// <summary>The instance as the Packing picture describes it: every number of S is an item.</summary>
    internal static PackingSpec Spec(PARTITION problem) =>
        new(problem.S.Select(x => new PackingItemInput(int.Parse(x), null, x)).ToList(), PackingRule.Partition);

    // --- Methods Including Constructors ---
    public PartitionDefaultVisualization() {

    }
    // Every number of S, in order, repeats included (#628, API_SET.KeepingRepeats), each with the Packing picture's item id by
    // position (I1, I2, ...), the ids the gadgets of Partition's reductions name.
    public API_SET visualize(PARTITION partition) => API_SET.KeepingRepeats(partition.S).UseItemIds();
    public API_JSON SolvedVisualization(PARTITION partition, string solution) {
        API_SET ec = visualize(partition);
        // "{}" (no split exists) or anything else that is not two sides: there is no answer to draw (#796).
        int[]? side = PartitionVerifier.SidesByPosition(partition, solution);
        if (side == null || ec.data.list == null || ec.data.list.Count != side.Length) return ec;

        for (int i = 0; i < side.Length; i++) {
            ec.data.list[i].color = side[i] == 0 ? "Solution" : "SolutionAlt";
        }

        return ec;
    }
}