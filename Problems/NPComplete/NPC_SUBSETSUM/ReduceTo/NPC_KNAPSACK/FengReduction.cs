using API.Interfaces;
using API.Problems.NPComplete.NPC_KNAPSACK;
using SPADE;

namespace API.Problems.NPComplete.NPC_SUBSETSUM.ReduceTo.NPC_KNAPSACK;

class FengReduction : IReduction<SUBSETSUM, KNAPSACK> {

    // --- Fields ---
    public string reductionName {get;} = "Feng's Knapsack Reduction";
    public string reductionDefinition {get;} = "Fengs reduction converts positive integers in SUBSETSUM to items in KNAPSACK";
    public string source {get;} = "Feng, Thomas";
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Steiner_tree_problem";
    public string[] contributors {get;} = {"Garret Stouffer, Daniel Igbokwe"};
  
    private string _complexity ="O(n)";
    private Dictionary<Object,Object> _gadgetMap = new Dictionary<Object,Object>();

    private SUBSETSUM _reductionFrom;
    private KNAPSACK _reductionTo;


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
    public KNAPSACK reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }
    public string complexity {
        get {
            return _complexity;
        }
    }


    // --- Methods Including Constructors ---
    public FengReduction(SUBSETSUM from) {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public FengReduction(string instance) : this(new SUBSETSUM(instance)) { }
    public FengReduction() : this(new SUBSETSUM()) { }
    public KNAPSACK reduce() {
        SUBSETSUM SUBSETSUMInstance = _reductionFrom;
        KNAPSACK reducedKNAPSACK = new KNAPSACK
        {
            //We reduce by setting T from SUBSETSUM equal to both the minimum value and maxmimum weight constraints. 
            W = SUBSETSUMInstance.T,
            V = SUBSETSUMInstance.T 
        };

        // We reduce the set of integers to a set of items by having each integer n equal (n,n) as an item. 
        List<string> integers = SUBSETSUMInstance.S;
        UtilCollection items = new UtilCollection("{}");
        for(int i=0; i < SUBSETSUMInstance.S.Count; i++) {
            UtilCollection item = new UtilCollection($"({integers[i]},{integers[i]})");
            items.Add(item);
            _gadgetMap[integers[i]] = integers[i];
        }

        reducedKNAPSACK.items = items;

        reducedKNAPSACK.instance = $"({items},{reducedKNAPSACK.W},{reducedKNAPSACK.V})";

        reductionTo = reducedKNAPSACK;
        return reducedKNAPSACK;
    }

    public string mapSolutions(string problemFromSolution)
    {
        return "";
    }
}
// return an instance of what you are reducing to