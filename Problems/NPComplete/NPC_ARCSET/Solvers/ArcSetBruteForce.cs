using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_ARCSET.Solvers;
class ArcSetBruteForce : ISolver {

    // --- Fields ---
    private string _solverName = "Arc Set Brute Force Solver";
    private string _solverDefinition = @" This Solver is a brute force solver, which checks all combinations of k edges until a solution is found or its determined there is no solution";
    private string _source = "wikipedia: https://en.wikipedia.org/wiki/Feedback_arc_set";

    private string[] _contributors = { "Alex Diviney","Caleb Eardley"};


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
    public ArcSetBruteForce() {

    }
    private long factorial(long x){
        long y = 1;
        for(long i=1; i<=x; i++){
            y *= i;
        }
        return y;
    }
    private string indexListToCertificate(List<int> indecies, List<Edge> edges){
        string certificate = "";
        foreach(int i in indecies){
            certificate += ","+edges[i].directedString();
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

    /**
    * Returns the set of edges that if removed from arcset would turn it acyclic
    */

    public string solve(ARCSET arc){
        ArcsetGraph graph = arc.directedGraph;
        List<int> combination = new List<int>();
        for(int i=0; i<graph.K; i++){
            combination.Add(i);
        }
        long reps = factorial(graph.getEdgeList.Count) / (factorial(graph.K) * factorial(graph.getEdgeList.Count - graph.K));
        for(int i=0; i<reps; i++){
            string certificate = indexListToCertificate(combination,graph.getEdgeList);
            bool verified = arc.defaultVerifier.verify(arc, certificate);
            if(verified){
                return certificate;
            }
            if(i != reps-1)combination = nextComb(combination, graph.getEdgeList.Count);
        }
        return "{}";
    }

    public Dictionary<KeyValuePair<string,string>, bool> getSolutionDict(string problemInstance, string solutionString){
        Dictionary<KeyValuePair<string,string>, bool> solutionDict = new Dictionary<KeyValuePair<string,string>, bool>();
        ArcsetGraph aGraph = new ArcsetGraph(problemInstance, true);
        List<KeyValuePair<string, string>> problemInstanceEdges = aGraph.edgesKVP;
        List<KeyValuePair<string, string>> solvedEdges = GraphParser.parseDirectedEdgeListWithStringFunctions(solutionString);

        foreach(var edge in solvedEdges){
            problemInstanceEdges.Remove(edge);
            solutionDict.Add(edge, true);
        }
        foreach(var edge in problemInstanceEdges){
            solutionDict.Add(edge, false);
        }
        return solutionDict;
    }

}