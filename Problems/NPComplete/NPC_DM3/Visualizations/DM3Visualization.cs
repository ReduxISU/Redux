using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Tables;
using API.Interfaces.Steps;
using API.Interfaces.Tripartite;
using API.Problems.NPComplete.NPC_DM3.Solvers;

namespace API.Problems.NPComplete.NPC_DM3.Visualizations;

/// <summary>
/// 3-Dimensional Matching as the Tripartite Matching picture (format=frames): X, Y and Z in three columns, each triple of M a
/// line through its three elements, set by set as the selected solver tries them. The list format is a table with one row
/// per triple and, once solved, whether it is in the matching. The problem's default visualization is still
/// <c>DummyVisualization</c>.
/// </summary>
class DM3Visualization : IVisualization<DM3, API_TableJSON>, ITripartiteVisualization<DM3> {
    public string visualizationName { get; } = "3-Dimensional Matching Tripartite Visualization";
    public string visualizationDefinition { get; } = "Draws X, Y and Z as three columns and each triple of M as a line through one element of each. A perfect matching picks triples that together use every element exactly once; a set of triples that uses an element twice fails.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.DynamicTable;
    public ISolver solver { get; } = new ThreeDimensionalMatchingBruteForce();

    // The Tripartite Matching picture (format=frames only). Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(ChosenTriples);
    TripartiteSpec ITripartiteVisualization<DM3>.Describe(DM3 problem) => Spec(problem);
    ChosenTriples ITripartiteVisualization<DM3>.ParseAnswer(DM3 problem, string answer) => TripartiteFrames.ParseMatching(Spec(problem), answer);

    /// <summary>The instance as the Tripartite Matching picture describes it: X, Y and Z, and M's triples as positions in them.</summary>
    internal static TripartiteSpec Spec(DM3 problem) =>
        new(problem.X, problem.Y, problem.Z,
            problem.M.Select(t => new TripartiteTriple(problem.X.IndexOf(t[0]), problem.Y.IndexOf(t[1]), problem.Z.IndexOf(t[2]))).ToList());

    public DM3Visualization() { }

    // The list format: one row per triple of M, in instance order, marked once there is an answer.
    private static API_TableJSON Table(DM3 problem, ChosenTriples answer) {
        var spec = Spec(problem);
        var chosen = new HashSet<string>(answer.Triples, StringComparer.Ordinal);
        var table = new API_TableJSON { title = $"{spec.Triples.Count} triples over |X| = {spec.X.Count}, |Y| = {spec.Y.Count}, |Z| = {spec.Z.Count}" };
        foreach (var (key, label) in new[] { ("triple", "Triple"), ("x", "X"), ("y", "Y"), ("z", "Z"), ("chosen", "Chosen") })
            table.columns.Add(new TableColumn { key = key, label = label });
        for (int i = 0; i < spec.Triples.Count; i++) {
            var t = spec.Triples[i];
            string id = TripartiteFrames.TripleId(i);
            bool isChosen = chosen.Contains(id);
            table.rows.Add(new TableRow {
                id = id,
                cells = new Dictionary<string, string> {
                    ["triple"] = id,
                    ["x"] = spec.X[t.X],
                    ["y"] = spec.Y[t.Y],
                    ["z"] = spec.Z[t.Z],
                    ["chosen"] = answer.Triples.Length == 0 ? "" : isChosen ? "yes" : "no",
                },
                color = isChosen ? "Solution" : null,
            });
        }
        return table;
    }

    public API_TableJSON visualize(DM3 problem) => Table(problem, new ChosenTriples([]));

    public API_JSON SolvedVisualization(DM3 problem, string solution) => Table(problem, TripartiteFrames.ParseMatching(Spec(problem), solution));
}
