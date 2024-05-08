using API.Interfaces;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT3;

namespace API.Problems.NPComplete.NPC_SAT.ReduceTo.NPC_SAT3;

class SATReduction : IReduction<SAT, SAT3>
{

    // --- Fields ---
    private string _reductionName = "Karp's SAT3 Reduction";
    private string _reductionDefinition = "Karp's Reduction from SAT to SAT3";
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string[] _contributors = { "Andrija Sevaljevic" };

    private string _complexity = "";
    private Dictionary<Object, Object> _gadgetMap = new Dictionary<Object, Object>();

    private SAT _reductionFrom;
    private SAT3 _reductionTo;


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
    public SAT reductionFrom
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
    public SAT3 reductionTo
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
    public SATReduction(SAT from)
    {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public SAT3 reduce()
    {
        SAT SATInstance = _reductionFrom;
        SAT3 reducedSAT3 = new SAT3();

        List<string> literals = SATInstance.getLiterals(SATInstance.instance);
        List<List<string>> clauses = SATInstance.getClauses(SATInstance.instance);

        string newInstance = "x1";

        int index = findSet(clauses);
        while (index != -1)
        {
            reduceSetSize(index, ref literals, ref newInstance, ref clauses);
            index = findSet(clauses);
        }

        reducedSAT3.clauses = clauses;
        reducedSAT3.literals = literals;

        string instance = "(";
        foreach (var i in clauses)
        {
            foreach (var j in i)
            {
                instance += " " + j + " |";
            }
            while(i.Count < 3) {
                instance += " " + i[0] + " |";
                i.Add(i[0]);
            }
            instance = instance.TrimEnd('|') + ") & (";
        }
        instance = instance.TrimEnd('(').TrimEnd().TrimEnd('&');

        reducedSAT3.instance = instance;

        reductionTo = reducedSAT3;
        return reducedSAT3;
    }

    public void reduceSetSize(int index, ref List<string> literals, ref string newInstance, ref List<List<string>> clauses)
    {

        while (literals.Contains(newInstance) || literals.Contains('!' + newInstance))
        {
            newInstance = "x" + (int.Parse(newInstance.Split('x')[1]) + 1).ToString();
        }
        literals.Add(newInstance);
        literals.Add('!' + newInstance);
        List<string> tempList = new()
        {
            clauses[index][0],
            clauses[index][1],
            newInstance
        };
        foreach (var j in tempList)
        {
            clauses[index].Remove(j);
        }
        clauses.Add(new List<string>(tempList));
        tempList.Clear();
        foreach (var j in clauses[index])
        {
            if (j[0] == '!') tempList.Add(j);
            else tempList.Add('!' + j);
            tempList.Add(newInstance);
            clauses.Add(new List<string>(tempList));
            tempList.Clear();
            
        }
        clauses[index].Add('!' + newInstance);

    }

    public int findSet(List<List<string>> clauses)
    {
        for (int i = 0; i < clauses.Count; i++)
        {
            if (clauses[i].Count > 3) return i;
        }
        return -1;
    }


    public string mapSolutions(SAT reductionFrom, SAT3 problemTo, string reductionFromSolution)
    {
        if (!reductionFrom.defaultVerifier.verify(reductionFrom, reductionFromSolution))
        {
            return "Solution is incorect";
        }

        return false.ToString();




    }

}
// return an instance of what you are reducing to