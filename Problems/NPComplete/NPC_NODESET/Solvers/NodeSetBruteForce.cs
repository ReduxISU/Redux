using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_NODESET.Solvers;
class NodeSetBruteForce : ISolver {

    // --- Fields ---
    private string _solverName = "Node Set Brute Force Solver";
    private string _solverDefinition = "This is a brute force solver for the Node Set problem";
    private string _source = "";
    private string[] _contributors = {"Andrija Sevaljevic"};

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

public NodeSetBruteForce() {
        
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
    public string solve(NODESET nodeSet){
        for(int i=0; i< nodeSet.K; i++) {
        List<int> combination = new List<int>();
        for(int j=0; j<=i; j++){
            combination.Add(j);
        }
        long reps = factorial(nodeSet.nodes.Count) / (factorial(i + 1) * factorial(nodeSet.nodes.Count - i - 1));
        for(int k=0; k<reps; k++){
            string certificate = indexListToCertificate(combination, nodeSet.nodes);
            if(nodeSet.defaultVerifier.verify(nodeSet, certificate)) {
                return certificate;
            }
            combination = nextComb(combination, nodeSet.nodes.Count);

        }
        }
        return "{}";
    }
   
}

   