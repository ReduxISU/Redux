using API.Interfaces;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT3;
using API.Interfaces.JSON_Objects;

namespace API.Problems.NPComplete.NPC_SAT.ReduceTo.NPC_SAT3;

class KarpSATToSAT3 : IReduction<SAT, SAT3>
{

    // --- Fields ---
    public string reductionName { get; } = "Karp's SAT3 Reduction";
    public string reductionDefinition { get; } = "Karp's Reduction from SAT to SAT3";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };

    private string _complexity = "";
    public List<Gadget> gadgets { get; }
    private SAT _reductionFrom;
    private SAT3 _reductionTo;


    // --- Properties ---
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
    public KarpSATToSAT3(SAT from)
    {
        gadgets = new();
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public KarpSATToSAT3(string instance) : this(new SAT(instance)) { }
    public KarpSATToSAT3() : this(new SAT()) { }
    public SAT3 reduce()
    {
        SAT SATInstance = _reductionFrom;
        SAT3 reducedSAT3 = new SAT3();

        List<string> literals = SATInstance.getLiterals(SATInstance.instance);
        List<List<string>> clauses = SATInstance.getClauses(SATInstance.instance);

        for (int clauseIndex = 0; clauseIndex < clauses.Count; clauseIndex++)
        {
            string clauseId = clauseIndex.ToString();
            gadgets.Add(new Gadget(
                "ClauseHighlight",
                new List<string>() { clauseId },
                new List<string>() { clauseId }
            ));
        }

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
            while (i.Count < 3)
            {
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

    public void reduceSetSize(int index, ref List<string> literals, ref string newVar, ref List<List<string>> clauses)
    {

        while (literals.Contains(newVar) || literals.Contains("!" + newVar))
        {
            int n = int.Parse(newVar.Substring(1)) + 1;
            newVar = "x" + n;
        }

        literals.Add(newVar);
        literals.Add("!" + newVar);

        string l1 = clauses[index][0];
        string l2 = clauses[index][1];

        clauses.Add(new List<string> { l1, l2, newVar });

        gadgets.Add(new Gadget("ClauseHighlight", new List<string>() { index.ToString() }, new List<string>() { (clauses.Count - 1).ToString() }));

        clauses[index].RemoveAt(0);
        clauses[index].RemoveAt(0);

        clauses[index].Add("!" + newVar);
    }


    public int findSet(List<List<string>> clauses)
    {
        for (int i = 0; i < clauses.Count; i++)
        {
            if (clauses[i].Count > 3) return i;
        }
        return -1;
    }


    public string mapSolutions(string reductionFromSolution)
    {
        return "";
    }

}
// return an instance of what you are reducing to