using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_WEIGHTEDCUT.Solvers;
class WeightedCutBruteForce : ISolver
{

    // --- Fields ---
    private string _solverName = "Weighted Cut Brute Force";
    private string _solverDefinition = "This is a brute force solver for the Weighted Cut problem";
    private string _source = "";
    private string[] _contributors = { "Andrija Sevaljevic" };

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
    public string[] contributors
    {
        get
        {
            return _contributors;
        }
    }

    public WeightedCutBruteForce()
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
    private string indexListToCertificate(List<int> indecies, List<string> nodes)
    {
        // Console.WriteLine("indecies: ", indecies.ToString());
        string certificate = "";
        foreach (int i in indecies)
        {
            certificate += nodes[i] + ",";
        }
        certificate = certificate.TrimEnd(',');
        // Console.WriteLine("returned statement: {"+certificate+"}");
        return "{" + certificate + "}";
    }

    private List<string> parseCertificate(string certificate)
    {

        List<string> nodeList = GraphParser.parseNodeListWithStringFunctions(certificate);
        return nodeList;
    }

    // Function below turns certificate into list of edges
    private string certificateToEdges(WEIGHTEDCUT cut, string certificate)
    {
        List<string> nodeList = parseCertificate(certificate);
        certificate = "{";
        foreach (var i in nodeList)
        {
            foreach (var j in cut.nodes)
            {
                if (!nodeList.Contains(j))
                {
                    int weight = 0;
                    bool edgeFound1 = cut.edges.Any(edge => edge.source == j && edge.destination == i);
                    bool edgeFound2 = cut.edges.Any(edge => edge.source == i && edge.destination == j);
                    if (edgeFound1 && !i.Equals(j))
                    { // checks if edge exists
                        foreach(var edge in cut.edges) {
                            if(edge.source == j && edge.destination == i) {
                                weight = edge.weight;
                            }
                        }
                        certificate += "{" + i + "," + j + "," + weight + "},"; // adds edge
                    }
                    else if (edgeFound2 && !i.Equals(j))
                    { // checks if edge exists
                        foreach(var edge in cut.edges) {
                            if(edge.source == i && edge.destination == j) {
                                weight = edge.weight;
                            }
                        }
                        certificate += "{" + i + "," + j+ "," + weight + "},"; // adds edge
                    }
                }
            }
        }
        certificate = certificate.TrimEnd(',');
        certificate += "}";
        return certificate;

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
    public string solve(WEIGHTEDCUT cut)
    {
        for (int i = 0; i < cut.nodes.Count; i++)
        {
            List<int> combination = new List<int>();
            for (int j = 0; j <= i; j++)
            {
                combination.Add(j);
            }
            long reps = factorial(cut.nodes.Count) / (factorial(i + 1) * factorial(cut.nodes.Count - i - 1));
            for (int k = 0; k < reps; k++)
            {
                string certificate = indexListToCertificate(combination, cut.nodes);
                certificate = certificateToEdges(cut, certificate);
                if (cut.defaultVerifier.verify(cut, certificate))
                {
                    return certificate;
                }
                combination = nextComb(combination, cut.nodes.Count);
            }
        }
        return "{}";
    }
}

