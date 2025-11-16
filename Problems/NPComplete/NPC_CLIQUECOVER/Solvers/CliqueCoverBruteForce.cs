using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_CLIQUECOVER.Solvers;
class CliqueCoverBruteForce : ISolver<CLIQUECOVER> {

    // --- Fields ---
    public string solverName {get;} = "Clique Cover Brute Force Solver";
    public string solverDefinition {get;} = "This is a brute force solver for the NP-Complete Clique Cover problem";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Andrija Sevaljevic" };

    // --- Methods Including Constructors ---
    public CliqueCoverBruteForce()
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


    public string solve(CLIQUECOVER clique)
    {

        if (clique.K > clique.nodes.Count)
        {
            return "{}";
        }

        List<int> binary = new List<int>();
        foreach (var i in clique.nodes)
        {
            binary.Add(0);
        }

        while (binary.Count(n => n == (clique.K - 1)) < clique.nodes.Count)
        {
            string certificate = BinaryToCertificate(binary, clique.nodes, clique.K);
            if (clique.defaultVerifier.verify(clique, certificate))
            {
                return certificate;
            }
            nextBinary(binary,clique.K);

        }

        return "{}";
    }
}
