using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using System.Numerics;

namespace API.Problems.NPComplete.NPC_SETCOVER.Solvers;

class GreedySetCover : ISolver<SETCOVER>
{

    // --- Fields ---
    public string solverName { get; } = "Greedy Set Cover";
    public string solverDefinition { get; } = "Repeatedly selects the set that covers the largest number of"
    + " currently uncovered elements, adds it to the cover, and marks its elements as covered. Continues"
    + " until all elements are covered or no set covers any remaining element. Produces an approximate"
    + " cover of size at most H(n) times the optimal, where H(n) is the n-th harmonic number, though in"
    + " practice performance is typically much closer to optimal than this worst-case bound suggests.";
    public string source { get; } = "Johnson, D. S. (1974). \"Approximation algorithms for combinatorial problems.\" Journal of Computer and System Sciences, 9(3), 256–278.";
    public string sourceLink { get; } = "https://dl.acm.org/doi/pdf/10.1145/800125.804034";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Greedy;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;

    public string complexity { get; } = "O(n * m), n = |universe|, m = |subsets|";

    // --- Methods Including Constructors ---
    public GreedySetCover()
    {

    }

    public string solve(SETCOVER setCover)
    {

        HashSet<string> covered = new HashSet<string>();
        HashSet<string> universal = new HashSet<string>(setCover.universal);
        List<int> chosenIndices = new List<int>();

        while (covered.Count < universal.Count)
        {

            int bestIndex = -1;
            int bestNewCount = 0;

            for (int i = 0; i < setCover.subsets.Count; i++)
            {
                if (chosenIndices.Contains(i)) continue;

                int newCount = setCover.subsets[i].Count(e => !covered.Contains(e));
                if (newCount > bestNewCount)
                {
                    bestNewCount = newCount;
                    bestIndex = i;
                }
            }

            if (bestIndex == -1) break;

            chosenIndices.Add(bestIndex);
            covered.UnionWith(setCover.subsets[bestIndex]);
        }

        if (covered.Count < universal.Count || chosenIndices.Count > setCover.K)
        {
            return "{}";
        }

        string solution = "{";
        foreach (var i in chosenIndices)
        {
            solution += "{" + string.Join(",", setCover.subsets[i]) + "},";
        }
        return solution.TrimEnd(',') + "}";
    }
}
