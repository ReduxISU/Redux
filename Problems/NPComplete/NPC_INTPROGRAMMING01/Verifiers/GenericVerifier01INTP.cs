using API.Interfaces;

namespace API.Problems.NPComplete.NPC_INTPROGRAMMING01.Verifiers;

class GenericVerifier01INTP : IVerifier {

    // --- Fields ---
    private string _verifierName = "0-1 Integer Programming Verifier";
    private string _verifierDefinition = "This is a verifier for 0-1 Integer Programming";
    private string _source = " ";

    private string _certificate = "";
    private string[] _contributors = { "Author Unknown"};


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
    public GenericVerifier01INTP() {
        
    }
    public List<int> parseCertificate(string certificate){
        List<int> c = new List<int>();
        string[] stringVector = certificate.Replace("(","").Replace(")","").Split(" ");
        for(int i=0; i<stringVector.Length; i++){
            c.Add(int.Parse(stringVector[i]));
        }
        return c;
    }

    //Takes an instance of the 0-1 integer programming problem and a certificate, and verifies if that certificate is a solution
    //c should be in the form of a vector of 1's and 0's separated by spaces. such as "(1 0 1 1 0)"
    public Boolean verify(INTPROGRAMMING01 Problem, string c){
        List<int> certificate = parseCertificate(c);
        
        //checks that the certificate is the correct size
        if(certificate.Count != Problem.C[0].Count){return false;}

        //compute C*certificate, or Cx
        List<int> solution = new List<int>();
        foreach(var row in Problem.C){
            int value = 0;
            for(int i = 0; i< row.Count; i++){
                value += row[i]*certificate[i];
            }
            solution.Add(value);
        }

        //checks that C*solution <= d
        for(int i=0; i<Problem.d.Count; i++){
            if(!(solution[i] <= Problem.d[i])){return false;}
        }

        return true; 
    }
}