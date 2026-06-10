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
        //return new API_SET(new UtilCollection(exactCover.instance));
        API_SET ec = new API_SET(new UtilCollection(exactCover.instance));

        List<string> results = solution
            .Trim('{', '}')
            .Split("},{")
            .Select(x => x.Trim('{', '}'))
            .Select(x => "{" + x + "}")
            .ToList();

        foreach (var id in results)
        {
            foreach (var set in ec.data.list[1].list)
            {
                if (set.id == "r-1-" + id)
                {
                    set.color = "Solution";
                    foreach (var elem in set.list)
                    {
                        elem.color = "Solution";
                    }
                    break;
                }
            }
        }

        return ec;
    }
}