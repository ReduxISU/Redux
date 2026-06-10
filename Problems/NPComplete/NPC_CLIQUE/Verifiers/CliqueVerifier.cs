using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_CLIQUE.Verifiers;

class CliqueVerifier : IVerifier<CLIQUE> {

    // --- Fields ---
    public string verifierName {get;} = "Clique Verifier";
    public string verifierDefinition {get;} = "This is a verifier for Clique";
    public string source {get;} = "";
    public string[] contributors {get;} = {"Caleb Eardley", "Kaden Marchetti"};


    private string _certificate =  "";

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

        if (string.IsNullOrWhiteSpace(certificate)) {
            throw new CertificateParseException(problem, certificate, "certificate is empty");
        }

        List<string> nodeList;
        try {
            nodeList = parseCertificate(certificate);
        } catch (Exception ex) {
            throw new CertificateParseException(problem, certificate, ex.Message);
        }
        if (nodeList.Count == 0 || nodeList.Any(string.IsNullOrWhiteSpace)) {
            throw new CertificateParseException(problem, certificate,
                "certificate did not parse to a non-empty list of node names");
        }
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