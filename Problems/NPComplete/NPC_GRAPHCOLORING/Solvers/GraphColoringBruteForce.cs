using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers;
class GraphColoringBruteForce : ISolver
{

    // --- Fields ---
    private string _solverName = "Graph Coloring Brute Force";
    private string _solverDefinition = "This is a brute force solver for the NP-Complete Graph Coloring problem";
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
    // --- Methods Including Constructors ---
    public GraphColoringBruteForce()
    {

    }


    private string BinaryToCertificate(List<int> binary, List<string> S, int K)
    {
        string certificate = "{";

        for (int j = 0; j < K; j++)
        {
            for (int i = 0; i < binary.Count; i++)
            {
                if (binary[i] == j)
                {
                    certificate += S[i] + ",";
                }
            }
            certificate = certificate.TrimEnd(',');
            certificate += "},{";
        }

        certificate = certificate.TrimEnd('{');

        return "{" + certificate.TrimEnd(',') + "}";

    }


    private void nextBinary(List<int> binary, int K)
    {
        for (int i = 0; i < binary.Count; i++)
        {
            if (binary[i] != K)
            {
                binary[i] += 1;
                return;
            }
            else if (binary[i] == K)
            {
                binary[i] = 0;
            }
        }
    }


    public string solve(GRAPHCOLORING gColor)
    {

        int numColors = gColor.K;
        if (gColor.K > gColor.nodes.Count) numColors = gColor.nodes.Count();
       

        List<int> binary = new List<int>();
        foreach (var i in gColor.nodes)
        {
            binary.Add(0);
        }

        while (binary.Count(n => n == (numColors - 1)) < gColor.nodes.Count)
        {
            string certificate = BinaryToCertificate(binary, gColor.nodes, numColors);
            if (gColor.defaultVerifier.verify(gColor, certificate))
            {
                return certificate;
            }
            nextBinary(binary,numColors);

        }

        return "{}";
    }
}
