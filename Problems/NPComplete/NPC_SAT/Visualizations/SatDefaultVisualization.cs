using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_SAT;

class SatDefaultVisualization : IVisualization<SAT>
{
    public string visualizationName { get; } = "SAT visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for SAT";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public string visualizationType { get; } = "Boolean Satisfiability";

    // --- Methods Including Constructors ---
    public SatDefaultVisualization()
    {

    }
    public API_JSON visualize(SAT instance)
    {
        return new API_SAT(instance);
    }

    public API_JSON SolvedVisualization(SAT instance, string solution)
    {
        List<string> items = solution.TrimStart('(').TrimEnd(')').Split(",").ToList();
        HashSet<string> highlight = new();
        foreach (string item in items)
        {
            List<string> split = item.Split(":").ToList();
            if (split[1] == "True")
                highlight.Add(split[0]);
            else
                highlight.Add("!" + split[0]);
        }

        API_SAT sat = new API_SAT(instance);

        foreach (var clause in sat.clauses)
            foreach (var literal in clause.literals)
            {
                if (highlight.Contains(literal.literal))
                {
                    literal.color = "Solution";
                }
            }

        return sat;
    }
}
