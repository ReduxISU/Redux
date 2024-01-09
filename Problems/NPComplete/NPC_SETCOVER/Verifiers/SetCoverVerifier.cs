using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_SETCOVER.Verifiers;

class SetCoverVerifier : IVerifier {

    // --- Fields ---
    private string _verifierName = "Set Cover Verifier";
    private string _verifierDefinition = "This is a verifier for Set Cover";
    private string _source = " ";
    private string[] _contributers = {"Andrija Sevaljevic"};


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
       public string[] contributers{
        get{
            return _contributers;
        }
    }

      public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public SetCoverVerifier() {
        
    }
    private List<string> parseCertificate(string certificate){

        List<string> elementList = certificate.Replace("},{",",").Replace("{","").Replace("}","").Replace(" ","").Split(",").ToList();
        return elementList;
        
    }


    public bool verify(SETCOVER problem, string certificate){

        List<string> elementList = parseCertificate(certificate);
        List<string> universalSet = new List<string>(problem.universal);

        foreach(var i in elementList){
            if (universalSet.Contains(i)) {
                universalSet.Remove(i);
            }
        }

        if(!universalSet.Any()) {
            return true;
        }

        return false;
    }
}