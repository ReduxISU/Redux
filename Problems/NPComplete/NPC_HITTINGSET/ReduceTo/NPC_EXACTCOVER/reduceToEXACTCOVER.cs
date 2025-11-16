using System.ComponentModel;
using API.Interfaces;
using API.Problems.NPComplete.NPC_EXACTCOVER;
using SPADE;
using Microsoft.Net.Http.Headers;

namespace API.Problems.NPComplete.NPC_HITTINGSET.ReduceTo.NPC_EXACTCOVER;

class reduceToEXACTCOVER : IReduction<HITTINGSET, EXACTCOVER>
{

    // --- Fields ---
    public string reductionName { get; } = "Hitting Set Reduction";
    public string reductionDefinition { get; } = "Karp's Reduction from Hitting Set to Exact Cover";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors { get; } = { "Russell Phillip" };

    private string _complexity = "";


    private HITTINGSET _reductionFrom;
    private EXACTCOVER _reductionTo;


    // --- Properties ---
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
    public reduceToEXACTCOVER(HITTINGSET from)
    {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public reduceToEXACTCOVER(string instance) : this(new HITTINGSET(instance)) { }
    public reduceToEXACTCOVER() : this(new HITTINGSET()) { }
    public EXACTCOVER reduce()
    {
        UtilCollection universal = new UtilCollection("{}");
        Dictionary<UtilCollection, int> setsToElement = new Dictionary<UtilCollection, int>();
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

        string input = "(" + universal.ToString() + "," + subsets.ToString() + ")";
        reductionTo = new EXACTCOVER(input);

        return reductionTo;
    }

    public string mapSolutions(string problemFromSolution)
    {
        return "";
    }
}