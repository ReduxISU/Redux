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
    // Every number of S, in order, repeats included (#628). A number keeps the id SPADE's set gives it ("r-12"); a second or
    // later copy of a number gets its position in S as well ("r-12-5" for the 12 at position 5), so every copy has its own
    // id and the ids do not change when other numbers are added after it.
    public API_SET visualize(PARTITION partition) {
        API_SET set = new API_SET(new UtilCollection(partition.instance));
        List<API_UtilCollection>? list = set.data.list;
        if (list == null) return set;
        var first = new Dictionary<string, API_UtilCollection>();
        foreach (API_UtilCollection e in list) {
            if (e.value != null) first.TryAdd(e.value, e);
        }
        var seen = new HashSet<string>();
        var copies = new List<API_UtilCollection>();
        for (int i = 0; i < partition.S.Count; i++) {
            string number = partition.S[i];
            if (!first.TryGetValue(number, out API_UtilCollection? e)) return set;
            copies.Add(seen.Add(number) ? e : new API_UtilCollection(new UtilCollection(number), $"{e.id}-{i}"));
        }
        list.Clear();
        list.AddRange(copies);
        return set;
    }
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