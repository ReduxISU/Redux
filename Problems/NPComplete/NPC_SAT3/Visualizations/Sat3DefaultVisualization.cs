using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_SAT3;

class Sat3DefaultVisualization : IVisualization<SAT3>
{
    public string visualizationName { get; } = "3SAT visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for 3SAT";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Kaden Marchetti" };
    public string visualizationType { get; } = "Boolean Satisfiability";

    // --- Methods Including Constructors ---
    public Sat3DefaultVisualization()
    {

    }
    public API_JSON visualize(SAT3 instance)
    {
        return new API_SAT(instance);
    }

    public API_JSON SolvedVisualization(SAT3 instance, string solution)
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
