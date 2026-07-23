using API.Interfaces;
using SPADE;

namespace API.Problems.NPComplete.NPC_CLIQUE.Verifiers;

class CliqueVerifier : IVerifier<CLIQUE> {

    public const string CertificateGrammar = "{K | K is set}";
    public const string CertificateExample = "{1,2,4}";

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
    public bool verify(CLIQUE problem, string certificate){

        if (string.IsNullOrWhiteSpace(certificate)) {
            throw new CertificateParseException(problem, certificate, "certificate is empty");
        }

        StringParser parser = new(CertificateGrammar);
        List<string> nodeList;
        try {
            parser.parse(certificate);
            // Materialize inside the try: SPADE may parse without error and only
            // fail when the result is enumerated or a missing key is accessed.
            nodeList = parser["K"].ToList().Select(node => node.ToString()).ToList();
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