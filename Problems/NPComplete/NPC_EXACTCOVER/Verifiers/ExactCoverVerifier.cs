using API.Interfaces;

namespace API.Problems.NPComplete.NPC_EXACTCOVER.Verifiers;

class ExactCoverVerifier : IVerifier<EXACTCOVER> {

    // --- Fields ---
    public string verifierName {get;} = "Exact Cover Verifier";
    public string verifierDefinition {get;} = "This is a verifier for Exact Cover";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Caleb Eardley"};

    private string _certificate = "";

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
    public bool verify(EXACTCOVER problem, string certificate){
        List<List<string>> parsedCertificate = parseCertificate(certificate);

        foreach(var subset_i in parsedCertificate){
            bool inS = false;
            foreach(var subset_j in problem.S){
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
        if(cover.OrderBy(e => e).SequenceEqual(problem.X.OrderBy(e => e))){
            
            return true;
        }

        return false;
    }
}