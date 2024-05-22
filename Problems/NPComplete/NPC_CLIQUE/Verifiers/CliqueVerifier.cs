using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_CLIQUE.Verifiers;

class CliqueVerifier : IVerifier {

    // --- Fields ---
    private string _verifierName = "Clique Verifier";
    private string _verifierDefinition = "This is a verifier for Clique";
    private string _source = " ";
    private string[] _contributors = {"Caleb Eardley", "Kaden Marchetti"};


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
    public CliqueVerifier() {
        
    }
    private List<string> parseCertificate(string certificate){

        List<string> nodeList = GraphParser.parseNodeListWithStringFunctions(certificate);
        return nodeList;
    }
    public bool verify(CLIQUE problem, string certificate){
        
        List<string> nodeList = parseCertificate(certificate);
        //Check k value
        if(nodeList.Count != problem.K){
            return false;
        }
        foreach(var i in nodeList){
            foreach(var j in nodeList){
                KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(i,j);
                KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(j,i);
                if(!(problem.edges.Contains(pairCheck1) || problem.edges.Contains(pairCheck2) || i.Equals(j))){
                    return false;
                }
            }
        }
        return true;
    }
}