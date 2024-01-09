using API.Interfaces;
using API.Problems.NPComplete.NPC_PARTITION;

namespace API.Problems.NPComplete.NPC_SUBSETSUM.ReduceTo.NPC_PARTITION;

class PartitionReduction : IReduction<SUBSETSUM, PARTITION> {

    // --- Fields ---
    private string _reductionName = "PARTITION Reduction";
    private string _reductionDefinition = "Karp's Reduction from Subset Sum to Partition";
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string[] _contributers = {"Andrija Sevaljevic"};
  
    private string _complexity ="";
    private Dictionary<Object,Object> _gadgetMap = new Dictionary<Object,Object>();

    private SUBSETSUM _reductionFrom;
    private PARTITION _reductionTo;


    // --- Properties ---
    public string reductionName {
        get {
            return _reductionName;
        }
    }
    public string reductionDefinition {
        get {
            return _reductionDefinition;
        }
    }
    public string source {
        get {
            return _source;
        }
    }
     public string[] contributers{
        get
        {
            return _contributers;
        }
    }
    public Dictionary<Object,Object> gadgetMap {
        get{
            return _gadgetMap;
        }
        set{
            _gadgetMap = value;
        }
    }
    public SUBSETSUM reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public PARTITION reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }
    


    // --- Methods Including Constructors ---
    public PartitionReduction(SUBSETSUM from) {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public PARTITION reduce() {
        SUBSETSUM SUBSETSUMInstance = _reductionFrom;
        PARTITION reducedPARTITION = new PARTITION();

        int sum = 0;
        string instance = "{";
        List<string> partitionNumbers = new List<string>();

        foreach (var i in SUBSETSUMInstance.S) {
            partitionNumbers.Add(i);
            instance += i + ",";
            sum += int.Parse(i);
        }

        partitionNumbers.Add((SUBSETSUMInstance.T + 1).ToString());
        instance += (SUBSETSUMInstance.T + 1).ToString() + ",";
        partitionNumbers.Add((sum - SUBSETSUMInstance.T + 1).ToString());
        instance += (sum - SUBSETSUMInstance.T + 1).ToString() + "}";

        reducedPARTITION.S = partitionNumbers;
        reducedPARTITION.instance = instance;

        reductionTo = reducedPARTITION;
        return reducedPARTITION;
    }

    public string mapSolutions(SUBSETSUM problemFrom,PARTITION problemTo, string problemFromSolution){
        if(!problemFrom.defaultVerifier.verify(problemFrom,problemFromSolution)){
            return "Subset Sum Solution is incorect";
        }

        return problemTo.S[0];
       



    }
}
// return an instance of what you are reducing to