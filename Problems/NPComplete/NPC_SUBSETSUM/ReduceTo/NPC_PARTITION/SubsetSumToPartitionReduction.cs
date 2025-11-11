using API.Interfaces;
using API.Problems.NPComplete.NPC_PARTITION;

namespace API.Problems.NPComplete.NPC_SUBSETSUM.ReduceTo.NPC_PARTITION;

class SubsetSumToPartitionReduction : IReduction<SUBSETSUM, PARTITION> {

    // --- Fields ---
    public string reductionName {get;} = "PARTITION Reduction";
    public string reductionDefinition {get;} = "Karp's Reduction from Subset Sum to Partition";
    public string source {get;} = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors {get;} = {"Andrija Sevaljevic"};
  
    private string _complexity ="";
    private Dictionary<Object,Object> _gadgetMap = new Dictionary<Object,Object>();

    private SUBSETSUM _reductionFrom;
    private PARTITION _reductionTo;


    // --- Properties ---
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
    public SubsetSumToPartitionReduction(SUBSETSUM from) {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public SubsetSumToPartitionReduction(string instance) : this(new SUBSETSUM(instance)) { }
    public SubsetSumToPartitionReduction() : this(new SUBSETSUM()) { }
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

    public string mapSolutions(string problemFromSolution)
    {
        return reductionTo.S[0];
    }
}