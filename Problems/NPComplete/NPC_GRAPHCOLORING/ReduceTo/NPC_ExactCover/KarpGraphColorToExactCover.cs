using API.Interfaces;
using API.Problems.NPComplete.NPC_GRAPHCOLORING;
using API.Problems.NPComplete.NPC_ExactCover;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo.NPC_ExactCover;

class GraphColorToExactCoverReduction : IReduction<GRAPHCOLORING, ExactCover>
{

    // --- Fields ---
    private string _reductionName = "Exact Cover Reduction";
    private string _reductionDefinition = "Karp's Reduction from Exact Cover to Subset Sum";
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string[] _contributors = { "Andrija Sevaljevic" };

    private string _complexity = "";
    private Dictionary<Object, Object> _gadgetMap = new Dictionary<Object, Object>();

    private GRAPHCOLORING _reductionFrom;
    private ExactCover _reductionTo;


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
    public string[] contributors
    {
        get
        {
            return _contributors;
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
    public ExactCover reductionTo
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
    public GraphColorToExactCoverReduction(GRAPHCOLORING from)
    {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public ExactCover reduce()
    {
        GRAPHCOLORING GRAPHCOLORINGInstance = _reductionFrom;
        ExactCover reducedExactCover = new ExactCover();

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

    public string mapSolutions(GRAPHCOLORING reductionFrom, ExactCover problemTo, string reductionFromSolution)
    {
        if (!reductionFrom.defaultVerifier.verify(reductionFrom, reductionFromSolution))
        {
            return "Solution is incorect";
        }

        return false.ToString();




    }

}
// return an instance of what you are reducing to