using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_EXACTCOVER.Solvers;
using SPADE;

namespace API.Problems.NPComplete.NPC_EXACTCOVER.Visualizations;

class ExactCoverDefaultVisualization : IVisualization<EXACTCOVER>
{
    public string visualizationName { get; } = "Exact Cover Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Exact Cover";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Russell Phillips, Andrija Sevaljevic" };
    public string visualizationType { get; } = "Set D3";
    public ISolver solver { get; } = new DancingLinks();

    // --- Methods Including Constructors ---
    public ExactCoverDefaultVisualization()
    {

    }
    public API_JSON visualize(EXACTCOVER exactCover)
    {
        return new API_SET(new UtilCollection(exactCover.instance));
    }
    public API_JSON SolvedVisualization(EXACTCOVER exactCover, string solution)
    {
        API_SET ec = new API_SET(new UtilCollection(exactCover.instance));

        // The instance is the ordered pair (X, S); ec.data.list[1] is the
        // collection S, whose elements are the subsets we may colour.
        List<API_UtilCollection>? subsets = ec.data.list?[1].list;
        if (subsets is null)
        {
            return ec;
        }

        // Each subset in S was given the id "r-1-" + uc.ToString() (see
        // API_UtilCollection). Parsing the solution with SPADE yields the same
        // ToString() form, so the chosen subsets match those ids by construction.
        foreach (UtilCollection chosen in new UtilCollection(solution))
        {
            string id = "r-1-" + chosen.ToString();
            API_UtilCollection? set = subsets.FirstOrDefault(s => s.id == id);
            if (set is null)
            {
                continue;
            }

            set.color = "Solution";
            foreach (var elem in set.list ?? Enumerable.Empty<API_UtilCollection>())
            {
                elem.color = "Solution";
            }
        }

        return ec;
    }
}