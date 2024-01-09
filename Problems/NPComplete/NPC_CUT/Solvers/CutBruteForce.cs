using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_CUT.Solvers;
class CutBruteForce : ISolver {

    // --- Fields ---
    private string _solverName = "Cut Brute Force";
    private string _solverDefinition = "This is a brute force solver for the Cut problem";
    private string _source = "Andrija Sevaljevic";
    private string[] _contributers = {"Andrija Sevaljevic"};

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
    public string[] contributers{
        get{
            return _contributers;
        }
    }

public CutBruteForce() {
        
    }
    private long factorial(long x){
        long y = 1;
        for(long i=1; i<=x; i++){
            y *= i;
        }
        return y;
    }
    //Function below turns index list into certificate
    private string indexListToCertificate(List<int> indecies, List<string> nodes){
        string certificate = "";
        foreach(int i in indecies){
            certificate += nodes[i]+",";
        }
        certificate = certificate.TrimEnd(',');
        return "{" + certificate + "}"; 
    }

    private List<string> parseCertificate(string certificate){

        List<string> nodeList = GraphParser.parseNodeListWithStringFunctions(certificate);
        return nodeList;
    }

// Function below turns certificate into list of edges
    private string certificateToEdges(CUT cut, string certificate) {
        List<string> nodeList = parseCertificate(certificate);
        certificate = "{";
        foreach(var i in nodeList){
             foreach(var j in cut.nodes){
                if (!nodeList.Contains(j)) { 
                    KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(i,j);
                    KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(j,i);
                        if ((cut.edges.Contains(pairCheck1) || cut.edges.Contains(pairCheck2)) && !i.Equals(j)) { // checks if is being cut
                        certificate += "{" + i + "," + j +"},"; // adds edge
                    }
                }
            }
        }
        certificate = certificate.TrimEnd(',');
        certificate += "}";
        return certificate;

    }

    // helper function to go through possible combinations
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
    public string solve(CUT cut){
        if(cut.K > cut.edges.Count) { // impossible to do cut if not enough edges
            return "{}";
        }
        for(int i=0; i<cut.K; i++) {
        List<int> combination = new List<int>();
        for(int j=0; j<=i; j++){
            combination.Add(j);
        }
        long reps = factorial(cut.nodes.Count) / (factorial(i + 1) * factorial(cut.nodes.Count - i - 1));
        for(int k=0; k<reps; k++){
            string certificate = indexListToCertificate(combination, cut.nodes);
            certificate = certificateToEdges(cut, certificate); // last two could be one function, but it is okay
            if(cut.defaultVerifier.verify(cut, certificate)) {
                return certificate;
            }
            combination = nextComb(combination, cut.nodes.Count);

        }
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
   public Dictionary<KeyValuePair<string,string>, bool> getSolutionDict(string problemInstance, string solutionString){

        Dictionary<KeyValuePair<string,string>, bool> solutionDict = new Dictionary<KeyValuePair<string,string>, bool>();
        CutGraph cGraph = new CutGraph(problemInstance, true);
        List<KeyValuePair<string, string>> problemInstanceEdges = cGraph.edgesKVP;
        List<KeyValuePair<string, string>> solvedEdges = GraphParser.parseUndirectedEdgeListWithStringFunctions(solutionString);

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

   