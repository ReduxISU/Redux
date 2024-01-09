using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_CLIQUECOVER.Solvers;
class CliqueCoverBruteForce : ISolver
{

    // --- Fields ---
    private string _solverName = "Clique Cover Brute Force";
    private string _solverDefinition = "This is a brute force solver for the NP-Complete Clique Cover problem";
    private string _source = "This solver was contributed by Andrija Sevaljevic";
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

    /// <summary>
    /// Given Clique instance in string format and solution string, outputs a solution dictionary with 
    /// true values mapped to nodes that are in the solution set else false. 
    /// </summary>
    /// <param name="problemInstance"></param>
    /// <param name="solutionString"></param>
    /// <returns></returns>
    // public Dictionary<string,bool> getSolutionDict(string problemInstance, string solutionString){

    //     Dictionary<string, bool> solutionDict = new Dictionary<string, bool>();
    //     GraphParser gParser = new GraphParser();
    //     CliqueGraph cGraph = new CliqueGraph(problemInstance, true);
    //     List<string> problemInstanceNodes = cGraph.nodesStringList;
    //     List<string> solvedNodes = gParser.getNodesFromNodeListString(solutionString);

    //     // Remove solvedNodes from instanceNodes
    //     foreach(string node in solvedNodes){
    //     problemInstanceNodes.Remove(node);
    //     // Console.WriteLine("Solved nodes: "+node);
    //     solutionDict.Add(node, true);
    //    }
    //     // Add solved nodes to dict as {name, true}
    //     // Add remaining instance nodes as {name, false}

    //     foreach(string node in problemInstanceNodes){

    //             solutionDict.Add(node, false);
    //     }


    //     return solutionDict;
    // }
}
