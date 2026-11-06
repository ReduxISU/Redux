using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_EXACTCOVER.Solvers;
using SPADE;

namespace API.Problems.NPComplete.NPC_EXACTCOVER.Visualizations;

class ExactCoverDefaultVisualization : IVisualization<EXACTCOVER, API_SET>, IBipartiteVisualization<EXACTCOVER> {
    public string visualizationName { get; } = "Exact Cover Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Exact Cover";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Russell Phillips", "Andrija Sevaljevic", "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.SetD3;
    public ISolver solver { get; } = new DancingLinks();

    // --- Methods Including Constructors ---
    public ExactCoverDefaultVisualization() {

    }
    // The pieces get the Bipartite Graph picture's ids (e:{x}, S{j}), the ids the gadgets of Exact Cover's reductions name.
    public API_SET visualize(EXACTCOVER exactCover) {
        return new API_SET(new UtilCollection(exactCover.instance)).UseBipartiteIds(Spec(exactCover).Sets);
    }
    public API_JSON SolvedVisualization(EXACTCOVER exactCover, string solution) {
        API_SET ec = new API_SET(new UtilCollection(exactCover.instance));

        // The instance is the ordered pair (X, S); ec.data.list[1] is the
        // collection S, whose elements are the subsets we may colour.
        List<API_UtilCollection>? subsets = ec.data.list?[1].list;
        if (subsets is null) {
            return ec.UseBipartiteIds(Spec(exactCover).Sets);
        }

        // Each subset in S was given the id "r-1-" + uc.ToString() (see
        // API_UtilCollection). Parsing the solution with SPADE yields the same
        // ToString() form, so the chosen subsets match those ids by construction.
        foreach (UtilCollection chosen in new UtilCollection(solution)) {
            string id = "r-1-" + chosen.ToString();
            API_UtilCollection? set = subsets.FirstOrDefault(s => s.id == id);
            if (set is null) {
                continue;
            }

            set.color = "Solution";
            foreach (var elem in set.list ?? Enumerable.Empty<API_UtilCollection>()) {
                elem.color = "Solution";
            }
        }

        return ec.UseBipartiteIds(Spec(exactCover).Sets);
    }

    // --- Bipartite Graph picture (format=frames only; the list-format methods above are unchanged) ---
    Type? IVisualization.StepShape => typeof(ChosenSets);

    BipartiteSpec IBipartiteVisualization<EXACTCOVER>.Describe(EXACTCOVER exactCover) => Spec(exactCover);

    /// <summary>Exact Cover as a Bipartite Graph: pick sets, every element exactly once. Solvers use it for captions.</summary>
    internal static BipartiteSpec Spec(EXACTCOVER exactCover) =>
        new(exactCover.X, exactCover.S, null, BipartitePick.Sets, BipartiteRule.ExactlyOnce);
}