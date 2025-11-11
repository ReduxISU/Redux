using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using System.Numerics;


namespace API.Problems.NPComplete.NPC_VERTEXCOVER.Solvers;
class VertexCoverBruteForce : ISolver<VERTEXCOVER> {

    // --- Fields ---
    public string solverName {get;} = "Vertex Cover Brute Force Solver";
    public string solverDefinition {get;} = "This solver simply tests combinations of nodes of size k until a solution is found, or all combinations are tested.";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Caleb Eardley"};

    // --- Methods Including Constructors ---
    public VertexCoverBruteForce() {

    }



    private BigInteger factorial(BigInteger x){
        BigInteger y = 1;
        for(BigInteger i=1; i<=x; i++){
            y *= i;
        }
        return y;
    }
    private string indexListToCertificate(List<int> indecies, List<string> nodes){
        string certificate = "";
        foreach(int i in indecies){
            certificate += ","+nodes[i];
        }
        return "{" + certificate.Substring(1) + "}";
    }
    private List<int> nextComb(List<int> combination, int size){
        for(int i=combination.Count-1; i>=0; i--){
            if(combination[i]+1 <= (i + size - combination.Count)){
                combination[i] += 1;
                for(int j = i+1; j < combination.Count; j++){
                    combination[j] = combination[j-1]+1;
                }
                return combination;
            }
        }
        return combination;
    }
/// <summary>
/// Solves a VERTEXCOVER instance input.
/// </summary>
/// <param name="G"> G is an undirected graph instance string</param>
/// <returns>
///  Subset of nodes that cover whole graph. 
/// </returns>
    public string solve(VERTEXCOVER G){
        List<int> combination = new List<int>();
        for(int i=0; i<G.K; i++){
            combination.Add(i);
        }
        BigInteger reps = factorial(G.nodes.Count) / (factorial(G.K) * factorial(G.nodes.Count - G.K));
        for(int i=0; i<reps; i++){
            string certificate = indexListToCertificate(combination,G.nodes);
            if(G.defaultVerifier.verify(G, certificate)){
                return certificate;
            }
            combination = nextComb(combination, G.nodes.Count);

        }
        return "{}";
    }


    /// <summary>
    ///  Copy of CliqueBruteForceMethod except with list input, note that we might want to encapsulate and have these solvers all implement a "nodeSolution" method. 
    /// </summary>
    /// <param name="problemInstance"></param>
    /// <param name="solutionString"></param>
    /// <returns></returns>
    public Dictionary<string,bool> getSolutionDict(string problemInstance, string solutionString){
        Dictionary<string, bool> solutionDict = new Dictionary<string, bool>();
        // GraphParser gParser = new GraphParser();
        VERTEXCOVER vertexcover = new VERTEXCOVER(problemInstance);
        List<string> problemInstanceNodes = vertexcover.nodes;
        // List<string> solvedNodes = gParser.getNodesFromNodeListString(solutionString);
        List<string> solvedNodes = GraphParser.parseNodeListWithStringFunctions(solutionString);

        // Remove solvedNodes from instanceNodes
        foreach(string node in solvedNodes){
            problemInstanceNodes.Remove(node);
        //  Console.WriteLine("Solved nodes: "+node);
            solutionDict.Add(node, true);
       }
        // Add solved nodes to dict as {name, true}
        // Add remaining instance nodes as {name, false}

        foreach(string node in problemInstanceNodes){
          
                solutionDict.Add(node, false);
        }

        return solutionDict;
    }

}
