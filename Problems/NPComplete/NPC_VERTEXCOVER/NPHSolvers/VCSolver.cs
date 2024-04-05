using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;



namespace API.Problems.NPComplete.NPC_VERTEXCOVER.NPHSolvers;
class VCSolverJanita : ISolver {

    // --- Fields ---
    private string _solverName = "Generic Solver";
    private string _solverDefinition = "This approximation solver is a naive solver for Vertex Cover that does not have a clear origination, although there have been many improvements upon it published. It returns the cover of size 2n when the optimal solution is n. This solver was sourced from the book: Cormen, Thomas H.; Leiserson, Charles E.; Rivest, Ronald L.; Stein, Clifford (2001) [1990]. 'Section 35.1: The vertex-cover problem'. Introduction to Algorithms (2nd ed.). MIT Press and McGraw-Hill. pp. 1024–1027. ISBN 0-262-03293-7.";
    private string _source = "Cormen, Thomas H.; Leiserson, Charles E.; Rivest, Ronald L.; Stein, Clifford (2001) [1990]. 'Section 35.1: The vertex-cover problem'. Introduction to Algorithms (2nd ed.). MIT Press and McGraw-Hill. pp. 1024–1027. ISBN 0-262-03293-7.";
    private string[] _contributors = { "Janita Aamir","Alex Diviney"};

    private string _complexity = "";

    // --- Properties ---
    public string solverName {
        get {
            return _solverName;
        }
    }
    public string solverDefinition {
        get {
            return _solverDefinition;
        }
    }
    public string source {
        get {
            return _source;
        }
    }
    public string[] contributors{
        get{
            return _contributors;
        }
    }

    // --- Methods Including Constructors ---
    public VCSolverJanita() {

    }


/// <summary>
/// Solves a VERTEXCOVER instance input.
/// </summary>
/// <param name="G"> G is an undirected graph instance string</param>
/// <returns>
///  Subset of nodes that cover whole graph. 
/// </returns>
/// <remarks>
/// Authored by Janita Aamir
/// Refactored to use a standard undirected graph object by Alex Diviney,
/// Refactored again to return a list of nodes instead of a list of key value pairs.
/// Some notes by Alex about potential applications of this solver. This solver grows more efficient the more tightly connected the graph is,
/// if the graph has n nodes and has a n-clique, this solver is maximally efficient. (I believe.) This has interesting implications in reducing from a clique problem then solving it.
/// </remarks>
    public List<string> Solve(VERTEXCOVER G){
        //{{a,b,c,d,e,f,g} : {(a,b) & (a,c) & (c,d) & (c,e) & (d,f) & (e,f) & (e,g)}}

        List<KeyValuePair<string, string>> edges = G.edges;
        List<KeyValuePair<string, string>> C = new List<KeyValuePair<string, string>>(); //This becomes our maximal matching
        Random rnd = new Random(); 
        while (edges.Count > 0)
        {
            int index = rnd.Next(edges.Count); //gets a random edge index
            KeyValuePair<string, string> edge = edges[index]; //gets a random edge
            KeyValuePair<string, string> fullEdge = new KeyValuePair<string, string>(edge.Key, edge.Value); //makes previous line more explicit
            C.Add(fullEdge); //Adds the random edge to C. 
            // string tempString = "";
            // foreach(KeyValuePair<string,string> tEdge in edges){
            //     tempString += tEdge.Key + " " + tEdge.Value + ",";
            // }
            // Console.WriteLine(tempString);
            foreach (KeyValuePair<string, string> e in new List<KeyValuePair<string, string>>(edges))
            { //For the random edge {u,v}, remove every edge in vertexcover that has a node u or v.
                
                if (e.Key.Equals(edge.Key))
                {
                    edges.Remove(e);
                }
                if (e.Key.Equals(edge.Value))
                {
                    edges.Remove(e);
                    
                }
                if (e.Value.Equals(edge.Key))
                {
                    edges.Remove(e);
                }
                if (e.Value.Equals(edge.Value))
                {
                    KeyValuePair<string, string> rmEdge = new KeyValuePair<string, string>(edge.Key, edge.Value);
                    edges.Remove(e);
                }
            }
        }

        List<string> leftoverNodes = new List<string>();
            foreach(KeyValuePair<string,string> cEdge in C){
            if(!leftoverNodes.Contains(cEdge.Key)){
                leftoverNodes.Add(cEdge.Key);
                }
                if(!leftoverNodes.Contains(cEdge.Value)){
                    leftoverNodes.Add(cEdge.Value);
                    
                }
            }

        return leftoverNodes; 

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
        VertexCoverGraph vGraph = new VertexCoverGraph(problemInstance, true);
        List<string> problemInstanceNodes = vGraph.nodesStringList;
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
