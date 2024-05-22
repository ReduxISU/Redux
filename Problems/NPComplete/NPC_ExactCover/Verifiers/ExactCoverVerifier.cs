using API.Interfaces;

namespace API.Problems.NPComplete.NPC_ExactCover.Verifiers;

class ExactCoverVerifier : IVerifier {

    // --- Fields ---
    private string _verifierName = "Exact Cover Verifier";
    private string _verifierDefinition = "This is a verifier for Exact Cover";
    private string _source = " ";
    private string[] _contributors = { "Caleb Eardley"};

    private string _certificate = "";

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
    public ExactCoverVerifier() {
        
    }
    private List<List<string>> parseCertificate(string certificate){
        List<List<string>> parsedCertificate = new List<List<string>>();
        List<string> SubsetStringList = certificate.Replace(" ","").Split(":")[0].Split("},{").ToList();
        foreach(string stringSet in SubsetStringList){
            List<string> subset = stringSet.Replace("{","").Replace("}","").Split(",").ToList();
            parsedCertificate.Add(subset);
        }

        return parsedCertificate;
    }
    //Example certificate "{{1,2,3},{2,3,4},{1,2}}
    public bool verify(ExactCover exactCover, string certificate){
        List<List<string>> parsedCertificate = parseCertificate(certificate);

        foreach(var subset_i in parsedCertificate){
            bool inS = false;
            foreach(var subset_j in exactCover.S){
                if(subset_i.OrderBy(e => e).SequenceEqual(subset_j.OrderBy(e => e))){
                    inS = true;
                }
            }
            if(!inS) return false;
        }

        //Check that it is an exact cover
        List<string> cover = new List<string>();
        foreach(var subset in parsedCertificate){
            foreach(string element in subset){
                cover.Add(element);
            }
        }
        if(cover.OrderBy(e => e).SequenceEqual(exactCover.X.OrderBy(e => e))){
            
            return true;
        }

        return false;
    }
}