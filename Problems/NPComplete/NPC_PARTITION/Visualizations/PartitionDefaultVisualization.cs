using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using SPADE;
using API.Problems.NPComplete.NPC_PARTITION.Solvers;
using API.Problems.NPComplete.NPC_PARTITION.Verifiers;

namespace API.Problems.NPComplete.NPC_PARTITION.Visualizations;

class PartitionDefaultVisualization : IVisualization<PARTITION, API_SET> {
    public string visualizationName { get; } = "Partition Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Partition";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public VisualizationType visualizationType { get; } = VisualizationType.SetD3;
    public ISolver solver { get; } = new PartitionBruteForce();

    // --- Methods Including Constructors ---
    public PartitionDefaultVisualization() {

    }
    // Every number of S, in order, repeats included (#628): a later copy of a number gets its position in its id ("r-12-5"
    // for the 12 at position 5), see API_SET.KeepingRepeats.
    public API_SET visualize(PARTITION partition) => API_SET.KeepingRepeats(partition.S);
    public API_JSON SolvedVisualization(PARTITION partition, string solution) {
        API_SET ec = visualize(partition);
        // "{}" (no split exists) or anything else that is not two sides: there is no answer to draw.
        int[]? side = PartitionVerifier.SidesByPosition(partition, solution);
        if (side == null || ec.data.list == null || ec.data.list.Count != side.Length) return ec;

        for (int i = 0; i < side.Length; i++) {
            ec.data.list[i].color = side[i] == 0 ? "Solution" : "SolutionAlt";
        }

        return ec;
    }
}