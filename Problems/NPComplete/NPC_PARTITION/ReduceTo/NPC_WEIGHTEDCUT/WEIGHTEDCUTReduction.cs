using API.Interfaces;
using API.Problems.NPComplete.NPC_WEIGHTEDCUT;

namespace API.Problems.NPComplete.NPC_PARTITION.ReduceTo.NPC_WEIGHTEDCUT;

class WEIGHTEDCUTReduction : IReduction<PARTITION, WEIGHTEDCUT>
{

    // --- Fields ---
    public string reductionName { get; } = "WEIGHTEDCUT Reduction";
    public string reductionDefinition { get; } = "Karp's Reduction from Graph Coloring to Clique Cover";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };

    private string _complexity = "";
    private Dictionary<Object, Object> _gadgetMap = new Dictionary<Object, Object>();

    private PARTITION _reductionFrom;
    private WEIGHTEDCUT _reductionTo;


    // --- Properties ---
    public Dictionary<Object, Object> gadgetMap
    {
        get
        {
            return _gadgetMap;
        }
        set
        {
            _gadgetMap = value;
        }
    }
    public PARTITION reductionFrom
    {
        get
        {
            return _reductionFrom;
        }
        set
        {
            _reductionFrom = value;
        }
    }
    public WEIGHTEDCUT reductionTo
    {
        get
        {
            return _reductionTo;
        }
        set
        {
            _reductionTo = value;
        }
    }



    // --- Methods Including Constructors ---
    public WEIGHTEDCUTReduction(PARTITION from)
    {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public WEIGHTEDCUTReduction(string instance) : this(new PARTITION(instance)) { }
    public WEIGHTEDCUTReduction() : this(new PARTITION()) { }
    public WEIGHTEDCUT reduce()
    {
        PARTITION PARTITIONInstance = _reductionFrom;
        WEIGHTEDCUT reducedWEIGHTEDCUT = new WEIGHTEDCUT();

        List<string> nodes = new List<string>();
        List<(string source, string destination, int weight)> edges = new List<(string source, string destination, int weight)>();

        string instance = "(({";
        for (int i = 1; i <= reductionFrom.S.Count; i++)
        {
            instance += i + ",";
            nodes.Add(i.ToString());
        }

        instance = instance.TrimEnd(',') + "}, {(";
        for (int i = 0; i < reductionFrom.S.Count; i++)
        {
            for (int j = i + 1; j < reductionFrom.S.Count; j++)
            {
                int weight = int.Parse(reductionFrom.S[i]) * int.Parse(reductionFrom.S[j]);
                instance += $"{{{i + 1},{j + 1}}},{weight}),(";
                edges.Add(((i + 1).ToString(), (j + 1).ToString(), weight));
            }
        }

        // remove trailing ",(" safely
        if (instance.EndsWith(",("))
            instance = instance[..^2];

        int sum = reductionFrom.S.Sum(int.Parse);
        sum = (sum * sum) / 4;

        instance += $"}}),{sum})";

        reducedWEIGHTEDCUT.K = sum;
        reducedWEIGHTEDCUT.nodes = nodes;
        reducedWEIGHTEDCUT.edges = edges;
        reducedWEIGHTEDCUT.instance = instance;

        reductionTo = reducedWEIGHTEDCUT;
        return reducedWEIGHTEDCUT;
    }

    public string mapSolutions(string reductionFromSolution)
    {
        return "";
    }
}
// return an instance of what you are reducing to