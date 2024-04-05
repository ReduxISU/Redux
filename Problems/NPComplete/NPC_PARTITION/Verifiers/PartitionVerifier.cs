using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_PARTITION.Verifiers;

class PartitionVerifier : IVerifier {

    // --- Fields ---
    private string _verifierName = "Partition Verifier";
    private string _verifierDefinition = "This is a verifier for the Partition problem";
    private string _source = "";
    private string[] _contributors = { "Andrija Sevaljevic"};

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
    public PartitionVerifier() {
        
    }

    public bool verify(PARTITION partition, string certificate){
        
        certificate = certificate.Replace("{","").Replace("}","").Replace(" ","");

        string[] pairs = certificate.Split("),(");
        string firstPair = pairs[0];
        string secondPair = pairs[1];

        List<string> c = firstPair.Replace("(","").Replace(")","").Replace(" ","").Split(",").ToList();
        List<string> c2 = secondPair.Replace("(","").Replace(")","").Replace(" ","").Split(",").ToList();

        foreach(var a in partition.S) {
            if(partition.S.Count(n => n == a) != (c.Count(n => n == a) + c2.Count(n => n == a))) {
                return false;
            }
        }

        int sum = 0;
        int sum2 = 0;

        foreach(string a in c){
            if(partition.S.Contains(a)){    
                sum += int.Parse(a);
            } else {
                return false;
            }
        }
        foreach(string a in c2){
            if(partition.S.Contains(a)){    
                sum2 += int.Parse(a);
            } else {
                return false;
            }
        }
        
        if(sum == sum2 && (c.Count() + c2.Count()) == partition.S.Count()) {
            return true;
        }

        return false;
    }

    
}