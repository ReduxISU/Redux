using API.Interfaces;
using API.Problems.NPComplete.NPC_WEIGHTEDCUT;

namespace API.Problems.NPComplete.NPC_PARTITION.ReduceTo.NPC_WEIGHTEDCUT;

class WEIGHTEDCUTReduction : IReduction<PARTITION, WEIGHTEDCUT>
{

    // --- Fields ---
    private string _reductionName = "WEIGHTEDCUT Reduction";
    private string _reductionDefinition = "Karp's Reduction from Graph Coloring to Clique Cover";
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string[] _contributers = { "Andrija Sevaljevic" };

    private string _complexity = "";
    private Dictionary<Object, Object> _gadgetMap = new Dictionary<Object, Object>();

    private PARTITION _reductionFrom;
    private WEIGHTEDCUT _reductionTo;


    // --- Properties ---
    public string reductionName
    {
        get
        {
            return _reductionName;
        }
    }
    public string reductionDefinition
    {
        get
        {
            return _reductionDefinition;
        }
    }
    public string source
    {
        get
        {
            return _source;
        }
    }
    public string[] contributers
    {
        get
        {
            return _contributers;
        }
    }
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
    public WEIGHTEDCUT reduce()
    {
        PARTITION PARTITIONInstance = _reductionFrom;
        WEIGHTEDCUT reducedWEIGHTEDCUT = new WEIGHTEDCUT();

        List<string> nodes = new List<string>();
        List<(string source, string destination, int weight)> edges = new List<(string source, string destination, int weight)>();

        string instance = "(({";
        for(int i = 1; i <= reductionFrom.S.Count; i++)
        {
            instance += i.ToString() + ',';
            nodes.Add(i.ToString());
        }

        instance = instance.TrimEnd(',') + "},{{";
        for(int i = 0; i < reductionFrom.S.Count; i++)
        {
            for(int j = i + 1; j < reductionFrom.S.Count; j++)
            {
               string edge = (i + 1) + "," + (j + 1) + "," +
               (Int32.Parse(reductionFrom.S[i]) * Int32.Parse(reductionFrom.S[j])).ToString() + "},{";
               instance += edge;
               string source = (1 + i).ToString();
               string destination = (1 + j).ToString();
               int weight = Int32.Parse(reductionFrom.S[i]) * Int32.Parse(reductionFrom.S[j]);
               edges.Add((source,destination,weight));
            }
        }

        int sum = 0;
        for(int i = 0; i < reductionFrom.S.Count; i++) {
            sum += Int32.Parse(reductionFrom.S[i]);
        }
        sum = (sum * sum) / 4;

        instance = instance.TrimEnd('{').TrimEnd(',') +"})," + sum.ToString() + ')';

        reducedWEIGHTEDCUT.K = sum;
        reducedWEIGHTEDCUT.nodes = nodes;
        reducedWEIGHTEDCUT.edges = edges;
        reducedWEIGHTEDCUT.instance = instance;

        reductionTo = reducedWEIGHTEDCUT;
        return reducedWEIGHTEDCUT;
    }

    public string mapSolutions(PARTITION reductionFrom, WEIGHTEDCUT problemTo, string reductionFromSolution)
    {
        if (!reductionFrom.defaultVerifier.verify(reductionFrom, reductionFromSolution))
        {
            return "Solution is incorect";
        }

        return false.ToString();




    }
}
// return an instance of what you are reducing to