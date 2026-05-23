using API.Interfaces;
using API.Problems.NPComplete.NPC_GRAPHCOLORING;
using API.Problems.NPComplete.NPC_EXACTCOVER;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo.NPC_EXACTCOVER;

class KarpGraphColorToExactCover : IReduction<GRAPHCOLORING, EXACTCOVER>
{

    // --- Fields ---
    public string reductionName {get;} = "Exact Cover Reduction";
    public string reductionDefinition {get;} = "Karp's Reduction from Exact Cover to Subset Sum";
    public string source {get;} = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors {get;} = { "Andrija Sevaljevic" };

    private string _complexity = "";
    private Dictionary<Object, Object> _gadgetMap = new Dictionary<Object, Object>();

    private GRAPHCOLORING _reductionFrom;
    private EXACTCOVER _reductionTo;


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
    public GRAPHCOLORING reductionFrom
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
    public EXACTCOVER reductionTo
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
    public KarpGraphColorToExactCover(GRAPHCOLORING from)
    {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public KarpGraphColorToExactCover(string instance) : this(new GRAPHCOLORING(instance)) { }
    public KarpGraphColorToExactCover() : this(new GRAPHCOLORING()) { }
    public EXACTCOVER reduce()
    {
        GRAPHCOLORING GRAPHCOLORINGInstance = _reductionFrom;
        EXACTCOVER reducedExactCover = new EXACTCOVER();

        List<string> universalSet = new List<string>();
        List<List<string>> subsets = new List<List<string>>();
        List<string> currentSubset = new List<string>();

        foreach (var i in reductionFrom.nodes)
        { //adding nodes to universal
            universalSet.Add(i);
        }

        foreach (var i in reductionFrom.edges)
        {//adding edges to universal
            universalSet.Add(i.Key + '_' + i.Value);
        }

        foreach (var u in reductionFrom.nodes)
        {
            foreach (var e in reductionFrom.edges)
            {
                //adding edges to universal
                if (e.Key == u || e.Value == u)
                {
                    for (int j = 1; j <= reductionFrom.K; j++)
                    {
                        universalSet.Add(u + "_" + e.Key + '_' + e.Value + "_" + j.ToString());
                    }
                }

            }
        }

        foreach (var u in reductionFrom.nodes)
        {
            for (int j = 1; j <= reductionFrom.K; j++)
            {
                currentSubset.Add(u);
                foreach (var e in reductionFrom.edges)
                {

                    if (e.Key == u || e.Value == u)
                    {
                        currentSubset.Add(u + "_" + e.Key + '_' + e.Value + "_" + j.ToString());

                    }
                }
                subsets.Add(new List<string>(currentSubset));
                currentSubset.Clear();
            }


        }

        foreach (var e in reductionFrom.edges)
        {//adding edge, edge,color1, edge,color2
            for (int f1 = 1; f1 <= reductionFrom.K; f1++)
            {
                for (int f2 = 1; f2 <= reductionFrom.K; f2++)
                {
                    if (f1 != f2)
                    {
                        currentSubset.Add(e.Key + '_' + e.Value);
                        for(int i = 1; i <= reductionFrom.K; i++)
                            if(i != f1)
                                currentSubset.Add(e.Key + '_' + e.Key + '_' + e.Value + '_' + i.ToString());
                        for(int i = 1; i <= reductionFrom.K; i++)
                            if(i != f2)
                                currentSubset.Add(e.Value + '_' + e.Key + '_' + e.Value + '_' + i.ToString());
                        subsets.Add(new List<string>(currentSubset));
                        currentSubset.Clear();
                    }
                }
            }
        }


        string instance = "{{";
        for (int i = 0; i < subsets.Count; i++)
        {
            for (int j = 0; j < subsets[i].Count; j++)
            {
                instance += subsets[i][j] + ',';
            }
            instance = instance.TrimEnd(',') + "},{";
        }

        instance = instance.TrimEnd('{').TrimEnd(',') + " : {";
        foreach (var i in universalSet)
        {
            instance += i + ',';
        }
        instance = instance.TrimEnd(',') + "}}";

        reducedExactCover.S = subsets;
        reducedExactCover.X = universalSet;
        reducedExactCover.instance = instance;

        reductionTo = reducedExactCover;
        return reducedExactCover;
    }

    public string mapSolutions(string reductionFromSolution)
    {
        return "";
    }

}
// return an instance of what you are reducing to