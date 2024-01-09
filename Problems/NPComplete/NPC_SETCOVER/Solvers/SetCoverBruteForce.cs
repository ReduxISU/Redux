using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_SETCOVER.Solvers;
class SetCoverBruteForce : ISolver
{

    // --- Fields ---
    private string _solverName = "Set Cover Brute Force";
    private string _solverDefinition = "This is a brute force solver for the NP-Complete Set Cover problem";
    private string _source = "Andrija Sevaljevic";
    private string[] _contributers = { "Andrija Sevaljevic" };


    // --- Properties ---
    public string solverName
    {
        get
        {
            return _solverName;
        }
    }
    public string solverDefinition
    {
        get
        {
            return _solverDefinition;
        }
    }
    public string source
    {
        get
        {
            return _source;
        }
    }
    public string[] contributers
    {
        get
        {
            return _contributers;
        }
    }
    // --- Methods Including Constructors ---
    public SetCoverBruteForce()
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
    private string indexListToCertificate(List<int> indecies, List<string> subsets)
    {

        string certificate = "{";
        foreach (int i in indecies)
        {
            certificate += subsets[i] + "},{";
        }
        certificate = certificate.TrimEnd('{');
        certificate = certificate.TrimEnd(',');

        return "{" + certificate + "}";
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
    public string solve(SETCOVER setCover)
    {
        for (int j = 0; j < setCover.K; j++)
        {
            List<int> combination = new List<int>();
            for (int i = 0; i <= j; i++)
            {
                combination.Add(i);
            }
            long reps = factorial(setCover.subsets.Count) / (factorial(j) * factorial(setCover.subsets.Count - j));
            for (int i = 0; i < reps; i++)
            {
                string certificate = indexListToCertificate(combination, setCover.subsets);
                if (setCover.defaultVerifier.verify(setCover, certificate))
                {
                    return certificate;
                }
                combination = nextComb(combination, setCover.subsets.Count);

            }
        }
        return "{}";
    }

}
