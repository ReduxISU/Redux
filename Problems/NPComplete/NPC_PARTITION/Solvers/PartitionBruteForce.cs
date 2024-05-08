using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_PARTITION.Solvers;
class PartitionBruteForce : ISolver {

    // --- Fields ---
    private string _solverName = "Partition Brute Force Solver";
    private string _solverDefinition = "This is a brute force solver for the Partition problem";
    private string _source = "";
    private string[] _contributors = {"Andrija Sevaljevic"};


    // --- Properties ---
    public string solverName {
        get {
            return _solverName;
        }
    }
    public string solverDefinition {
        get {
            return _solverDefinition;
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
    // --- Methods Including Constructors ---
    public PartitionBruteForce() {
        
    }
    private string BinaryToCertificate(List<int> binary, List<string> S ){
        string certificate = "(";
        for(int i = 0; i< binary.Count; i++){
            if(binary[i] == 1){
                certificate += S[i]+",";
            }
        }
        certificate = certificate.TrimEnd(',');
        certificate += "),(";
        for(int i = 0; i< binary.Count; i++){
            if(binary[i] == 0){
                certificate += S[i]+",";
            }
        }
        return "{" + certificate.TrimEnd(',') + ")}";

    }
    private void nextBinary(List<int> binary){
        for(int i = 0; i< binary.Count; i++){
            if(binary[i] == 0){
                binary[i] += 1;
                return;
            }
            else if(binary[i] == 1){
                binary[i] = 0;
            }
        }
    }
       
    public string solve(PARTITION partition){
        List<int> binary = new List<int>(){1};
        int counter = 0;
        for(int i = 0; i < partition.S.Count-1; i++){
            binary.Add(0);
        }
        string certificate = BinaryToCertificate(binary, partition.S);
        while(counter <= Math.Pow(2,partition.S.Count)){
            nextBinary(binary);
            certificate = BinaryToCertificate(binary, partition.S);
            if(partition.defaultVerifier.verify(partition, certificate)){
                return certificate;
            }
            counter++;
        }
        return "{}";
    }
}