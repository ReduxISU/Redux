using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_STEINERTREE.Solvers;
class SteinerTreeBruteForce : ISolver<STEINERTREE> {

    // --- Fields ---
    public string solverName {get;} = "Steiner Tree Brute Force Solver";
    public string solverDefinition {get;} = "This is a brute force solver for the NP-Complete Steiner Tree problem";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }

    // --- Methods Including Constructors ---
    public SteinerTreeBruteForce()
    {

    }

    private long factorial(long x)
    {
        long y = 1;
        for (long i = 1; i <= x; i++)
        {
            y *= i;
        }
        return y;
    }

    private List<int> nextComb(List<int> combination, int size)
    {
        for (int i = combination.Count - 1; i >= 0; i--)
        {
            if (combination[i] + 1 <= (i + size - combination.Count))
            {
                combination[i] += 1;
                for (int j = i + 1; j < combination.Count; j++)
                {
                    combination[j] = combination[j - 1] + 1;
                }
                return combination;
            }
        }
        return combination;
    }

    private string indexListToCertificate(List<int> binary, List<KeyValuePair<string, string>> edges)
    {
        string certificate = "";
        foreach (int i in binary)
        {
            certificate += "{" + edges[i].Key + ',' + edges[i].Value + "},";
        }
        return "{" + certificate.TrimEnd(',') + "}";
    }

    public string solve(STEINERTREE steiner)
    {

        for (int i = steiner.terminals.Count - 1; i <= steiner.K; i++)
        {
            List<int> combination = new List<int>();
            for (int j = 0; j < i; j++)
            {
                combination.Add(j);
            }

            long reps = factorial(steiner.edges.Count) / (factorial(i + 1) * factorial(steiner.edges.Count - i - 1));
            for (int k = 0; k < reps; k++)
            {
                string certificate = indexListToCertificate(combination, steiner.edges);
                if (steiner.defaultVerifier.verify(steiner, certificate))
                {
                    return certificate;
                }
                combination = nextComb(combination, steiner.edges.Count);

            }

        }
        return "{}";
    }

}
