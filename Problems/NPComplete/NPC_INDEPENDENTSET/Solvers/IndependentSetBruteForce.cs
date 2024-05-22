using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_INDEPENDENTSET.Solvers;
class IndependentSetBruteForce : ISolver {

    // --- Fields ---
    private string _solverName = "Independent Set Brute Force";
    private string _solverDefinition = "This is a brute force solver for the NP-Complete Independent Set problem";
    private string _source = "";
    private string[] _contributors = {"Russell Phillips"};


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
    public IndependentSetBruteForce() {
        
    }
    private long factorial(long x){
        long y = 1;
        for(long i=1; i<=x; i++){
            y *= i;
        }
        return y;
    }
    private string indexListToCertificate(List<int> indecies, List<string> nodes){
        string certificate = "";
        foreach(int i in indecies){
            certificate += nodes[i]+",";
        }
        certificate = certificate.TrimEnd(',');
        return "{" + certificate + "}"; 
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
    public string solve(INDEPENDENTSET independentSet){
        List<int> combination = new List<int>();
        for(int i=0; i<independentSet.K; i++){
            combination.Add(i);
        }
        long reps = factorial(independentSet.nodes.Count) / (factorial(independentSet.K) * factorial(independentSet.nodes.Count - independentSet.K));
        for(int i=0; i<reps; i++){
            string certificate = indexListToCertificate(combination,independentSet.nodes);
            if(independentSet.defaultVerifier.verify(independentSet, certificate)){
                return certificate;
            }
            combination = nextComb(combination, independentSet.nodes.Count);

        }
        return "{}";
    }

    /// <summary>
    /// Given Independent Set instance in string format and solution string, outputs a solution dictionary with 
    /// true values mapped to nodes that are in the solution set else false. 
    /// </summary>
    /// <param name="problemInstance"></param>
    /// <param name="solutionString"></param>
    /// <returns></returns>
    public Dictionary<string,bool> getSolutionDict(string problemInstance, string solutionString){

        Dictionary<string, bool> solutionDict = new Dictionary<string, bool>();
        GraphParser gParser = new GraphParser();
        IndependentSetGraph cGraph = new IndependentSetGraph(problemInstance, true);
        List<string> problemInstanceNodes = cGraph.nodesStringList;
        List<string> solvedNodes = gParser.getNodesFromNodeListString(solutionString);
        
        // Remove solvedNodes from instanceNodes
        foreach(string node in solvedNodes){
            problemInstanceNodes.Remove(node);
            solutionDict.Add(node, true);
        }
        foreach(string node in problemInstanceNodes){
            solutionDict.Add(node, false);
        }
        return solutionDict;
    }
}
