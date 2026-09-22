using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Solvers;

class TwoApproximationMinimumVertexCover : ISolver<MINIMUMVERTEXCOVER> {

    // --- Fields ---
    public string solverName { get; } = "Minimum Vertex Cover Approximation";
    public string solverDefinition { get; } = "This approximation solver is a naive solver for Minimum Vertex Cover that does not have a clear origination, although there have been many improvements upon it"
    + " published. It repeatedly picks an arbitrary remaining edge, adds both endpoints to the cover, and"
    + " removes all edges incident to either endpoint, until no edges remain. It returns a cover of size at"
    + " most 2n when the optimal solution is n.";
    public string source { get; } = "Cormen, Thomas H.; Leiserson, Charles E.; Rivest, Ronald L.; Stein, Clifford (2001) [1990]. 'Section 35.1: The vertex-cover problem'. Introduction to Algorithms (2nd ed.). MIT Press and McGraw-Hill. pp. 1024–1027. ISBN 0-262-03293-7.";
    public string sourceLink { get; } =
        "https://www.cs.mcgill.ca/~akroit/math/compsci/Cormen%20Introduction%20to%20Algorithms.pdf";
    public string[] contributors { get; } = { "Janita Aamir", "Alex Diviney" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Approximation;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    public string complexity { get; } = "O(E), E = |edges|";

    // --- Methods Including Constructors ---
    public TwoApproximationMinimumVertexCover() {

    }

    public string solve(MINIMUMVERTEXCOVER G) {
        //{{a,b,c,d,e,f,g} : {(a,b) & (a,c) & (c,d) & (c,e) & (d,f) & (e,f) & (e,g)}}

        List<KeyValuePair<string, string>> edges = G.edges;
        List<KeyValuePair<string, string>> C = new List<KeyValuePair<string, string>>(); //This becomes our maximal matching
        Random rnd = new Random();
        while (edges.Count > 0) {
            int index = rnd.Next(edges.Count); //gets a random edge index
            KeyValuePair<string, string> edge = edges[index]; //gets a random edge
            KeyValuePair<string, string> fullEdge = new KeyValuePair<string, string>(edge.Key, edge.Value); //makes previous line more explicit
            C.Add(fullEdge); //Adds the random edge to C. 
                             // string tempString = "";
                             // foreach(KeyValuePair<string,string> tEdge in edges){
                             //     tempString += tEdge.Key + " " + tEdge.Value + ",";
                             // }
                             // Console.WriteLine(tempString);
            foreach (KeyValuePair<string, string> e in new List<KeyValuePair<string, string>>(edges)) { //For the random edge {u,v}, remove every edge in vertexcover that has a node u or v.

                if (e.Key.Equals(edge.Key)) {
                    edges.Remove(e);
                }
                if (e.Key.Equals(edge.Value)) {
                    edges.Remove(e);

                }
                if (e.Value.Equals(edge.Key)) {
                    edges.Remove(e);
                }
                if (e.Value.Equals(edge.Value)) {
                    KeyValuePair<string, string> rmEdge = new KeyValuePair<string, string>(edge.Key, edge.Value);
                    edges.Remove(e);
                }
            }
        }

        List<string> leftoverNodes = new List<string>();
        foreach (KeyValuePair<string, string> cEdge in C) {
            if (!leftoverNodes.Contains(cEdge.Key)) {
                leftoverNodes.Add(cEdge.Key);
            }
            if (!leftoverNodes.Contains(cEdge.Value)) {
                leftoverNodes.Add(cEdge.Value);

            }
        }

        return "{" + string.Join(",", leftoverNodes) + "}";;

    }

}