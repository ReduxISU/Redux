using System.ComponentModel;
using API.Interfaces;
using API.Problems.NPComplete.NPC_CLIQUECOVER;
using API.Problems.NPComplete.NPC_ExactCover;
using API.Tools.UtilCollection;
using Microsoft.Net.Http.Headers;

namespace API.Problems.NPComplete.NPC_HITTINGSET.ReduceTo.NPC_EXACTCOVER;

class ExactCoverReduction : IReduction<HITTINGSET, ExactCover>
{

    // --- Fields ---
    private string _reductionName = "Hitting set Reduction";
    private string _reductionDefinition = "Karp's Reduction from Hitting Set to Exact Cover";
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string[] _contributors = { "Russell Phillip" };

    private string _complexity = "";

    private Dictionary<Object, Object> _gadgetMap = new Dictionary<Object, Object>();

    private HITTINGSET _reductionFrom;
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
    public HITTINGSET reductionFrom
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
    public ExactCoverReduction(HITTINGSET from)
    {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public ExactCover reduce()
    {
        UtilCollection universal = new UtilCollection("{}");
        Dictionary<UtilCollection,int> setsToElement = new Dictionary<UtilCollection,int>();
        int elementNum = 1;
        foreach (UtilCollection set in _reductionFrom.subSets)
        {
            setsToElement.Add(set, elementNum);
            universal.Add(new UtilCollection(elementNum.ToString()));
            elementNum++;
        }

        UtilCollection subsets = new UtilCollection("{}");
        foreach (UtilCollection item in _reductionFrom.universalSet)
        {
            UtilCollection newSubset = new UtilCollection("{}");
            foreach (UtilCollection set in _reductionFrom.subSets)
            {
                if (set.Contains(item))
                {
                    newSubset.Add(new UtilCollection(setsToElement.GetValueOrDefault(set).ToString()));
                }
            }
            subsets.Add(newSubset);
        }

        ExactCover reductionTo = new ExactCover($"{{{subsets} : {universal}}}");
        return reductionTo;
    }

    public string mapSolutions(HITTINGSET problemFrom, ExactCover problemTo, string problemFromSolution)
    {
        if (!problemFrom.defaultVerifier.verify(problemFrom, problemFromSolution))
        {
            return "Solution is incorect";
        }

        return false.ToString();




    }
}