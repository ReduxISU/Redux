using System.ComponentModel;
using System.Text.Json.Serialization;
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
    // reduce() transposes the input's own subset/element incidence structure (one
    // output entry per (item, subset) pair where the item IS a member) — that
    // incidence count is already what the HITTINGSET instance's own encoding lists,
    // so output size tracks input size rather than blowing up beyond it.
    public ReductionCost cost { get; } = ReductionCost.Linear;

    // Declared, not derived. Transposes the input's own subset/element incidence
    // structure into EXACTCOVER's shape -- the same combinatorial object re-expressed,
    // no new gadgetry built.
    public ReductionType reductionType { get; } = ReductionType.Restriction;
    // Declared, not derived. One pass over subsets to number them, then one pass over
    // universalSet x subSets to transpose the incidence relation.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Polynomial;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = null;


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