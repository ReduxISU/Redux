using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using SPADE;

namespace API.Problems.NPComplete.NPC_PARTITION.Visualizations;

class PartitionDefaultVisualization : IVisualization<PARTITION>
{
    public string visualizationName { get; } = "Partition Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Partition";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public string visualizationType { get; } = "Set D3";

    // --- Methods Including Constructors ---
    public PartitionDefaultVisualization()
    {

    }
    public API_JSON visualize(PARTITION partition)
    {
        return new API_SET(new UtilCollection(partition.instance));
    }
    public API_JSON SolvedVisualization(PARTITION partition, string solution)
    {
        //return new API_SET(new UtilCollection(partition.instance));
        API_SET ec = new API_SET(new UtilCollection(partition.instance));
        UtilCollection sol = new(solution);
        List<UtilCollection> groups = new();
        foreach (UtilCollection e in sol)
        {
            groups.Add(e);
        }

        foreach (var e in ec.data.list)
        {
            if (groups[0].Contains(new(e.value)))
            {
                e.color = "Solution";
            }
            else
            {
                e.color = "SolutionAlt";
            }
        }

        return ec;
    }
}