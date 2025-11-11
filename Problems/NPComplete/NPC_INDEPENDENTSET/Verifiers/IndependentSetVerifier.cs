using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_INDEPENDENTSET.Verifiers;

class IndependentSetVerifier : IVerifier<INDEPENDENTSET> {

    // --- Fields ---
    public string verifierName {get;} = "Independent Verifier";
    public string verifierDefinition {get;} = "This is a verifier for Independent Set";
    public string source {get;} = "";
    public string[] contributors {get;} = {"Russell Phillips"};


    private string _certificate =  "";

      public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public IndependentSetVerifier() {
        
    }
    private List<string> parseCertificate(string certificate){

        List<string> nodeList = GraphParser.parseNodeListWithStringFunctions(certificate);
        return nodeList;
    }

    public bool verify(INDEPENDENTSET problem, string certificate){
        List<string> nodeList = parseCertificate(certificate);
        //Check k value
        if(nodeList.Count != problem.K){
            return false;
        }
        foreach(var i in nodeList){
            foreach(var j in nodeList){
                KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(i,j);
                KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(j,i);
                if((problem.edges.Contains(pairCheck1) || problem.edges.Contains(pairCheck2)) && !i.Equals(j)){
                    return false;
                }
            }
        }
        return true;
    }
}