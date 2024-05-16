using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_WEIGHTEDCUT.Verifiers;

class WeightedCutVerifier : IVerifier {

    // --- Fields ---
    private string _verifierName = "Weighted Cut Verifier";
    private string _verifierDefinition = "This is a verifier for the Weighted Cut problem";
    private string _source = "";
    private string[] _contributors = {"Andrija Sevaljevic"};


    private string _certificate =  "";

    // --- Properties ---
    public string verifierName {
        get {
            return _verifierName;
        }
    }
    public string verifierDefinition {
        get {
            return _verifierDefinition;
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

      public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public WeightedCutVerifier() {
        
    }
    private List<string> parseCertificate(string certificate){

        List<string> edgeList = certificate.Replace("{{", "").Replace("}}","").Replace(" ","").Split("},{").ToList();
        return edgeList;
    }
    public bool verify(WEIGHTEDCUT problem, string certificate){

        if(certificate == "{}") {
            return false;
        }
        
        List<string> edgeList = parseCertificate(certificate); 
        int counter = 0;
        foreach(var i in edgeList){
            List<string> currentEdge = i.Split(",").ToList();
            string source = currentEdge[0];
            string destination = currentEdge[1];
            int weight = Int32.Parse(currentEdge[2]);
            if ((problem.edges.Contains((source,destination,weight)) || problem.edges.Contains((destination,source,weight))) && !currentEdge[1].Equals(currentEdge[0])) { //Checks if edge exists, then adds to cut
                counter += weight;
            }
            
        }
        if (counter != problem.K) {
            return false;
        }
        return true;
    }
}