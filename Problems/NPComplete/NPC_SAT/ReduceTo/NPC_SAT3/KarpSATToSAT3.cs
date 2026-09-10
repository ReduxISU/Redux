using System.Text.Json.Serialization;
using API.Interfaces;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT3;
using API.Interfaces.JSON_Objects;

namespace API.Problems.NPComplete.NPC_SAT.ReduceTo.NPC_SAT3;

class KarpSATToSAT3 : IReduction<SAT, SAT3> {

    // --- Fields ---
    public string reductionName { get; } = "Karp's 3SAT Reduction";
    public string reductionDefinition { get; } = "Karp's Reduction from SAT to SAT3";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    // Each over-length clause split removes literals from the original clause and adds
    // one new 3-literal clause per split; total splits (and clauses produced) is O(total
    // literal count) across the whole formula — linear in input size.
    public ReductionCost cost { get; } = ReductionCost.Linear;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = null;
    public List<Gadget> gadgets { get; }
    private SAT _reductionFrom;
    private SAT3 _reductionTo;


    // --- Properties ---
    public SAT reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public SAT3 reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }



    // --- Methods Including Constructors ---
    public KarpSATToSAT3(SAT from) {
        gadgets = new();
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public KarpSATToSAT3(string instance) : this(new SAT(instance)) { }
    public KarpSATToSAT3() : this(new SAT()) { }
    public SAT3 reduce() {
        SAT SATInstance = _reductionFrom;
        SAT3 reducedSAT3 = new SAT3();

        List<string> literals = SATInstance.getLiterals(SATInstance.instance);
        List<List<string>> clauses = SATInstance.getClauses(SATInstance.instance);

        for (int clauseIndex = 0; clauseIndex < clauses.Count; clauseIndex++) {
            string clauseId = clauseIndex.ToString();
            gadgets.Add(new Gadget(
                "ClauseHighlight",
                new List<string>() { clauseId },
                new List<string>() { clauseId }
            ));

            for (int literalIndex = 0; literalIndex < clauses[clauseIndex].Count; literalIndex++) {
                string literalId = clauseId + "-" + literalIndex.ToString();
                gadgets.Add(new Gadget(
                    "ElementHighlight",
                    new List<string>() { literalId },
                    new List<string>() { literalId }
                ));
            }
        }


        string newInstance = "x1";

        int index = findSet(clauses);
        while (index != -1) {
            reduceSetSize(index, ref literals, ref newInstance, ref clauses);
            index = findSet(clauses);
        }

        reducedSAT3.clauses = clauses;
        reducedSAT3.literals = literals;

        string instance = "(";
        for (int c = 0; c < clauses.Count; c++) {
            var i = clauses[c];

            for (int k = 0; k < i.Count; k++) {
                instance += " " + i[k] + " |";
            }

            if (i.Count == 2) {
                instance += " " + i[0] + " |";
                i.Add(i[0]);
                gadgets.Add(new Gadget("ElementHighlight", new List<string>() { c + "-0" }, new List<string>() { c + "-2" }));
            }

            if (i.Count == 1) {
                instance += " " + i[0] + " |" + " " + i[0] + " |";
                i.Add(i[0]);
                i.Add(i[0]);
                gadgets.Add(new Gadget("ElementHighlight", new List<string>() { c + "-0" }, new List<string>() { c + "-2", c + "-1" }));
            }

            instance = instance.TrimEnd('|') + ") & (";
        }

        instance = instance.TrimEnd('(').TrimEnd().TrimEnd('&');

        reducedSAT3.instance = instance;

        reductionTo = reducedSAT3;
        return reducedSAT3;
    }

    public void reduceSetSize(int index, ref List<string> literals, ref string newVar, ref List<List<string>> clauses) {

        while (literals.Contains(newVar) || literals.Contains("!" + newVar)) {
            int n = int.Parse(newVar.Substring(1)) + 1;
            newVar = "x" + n;
        }

        literals.Add(newVar);
        literals.Add("!" + newVar);

        // Getting last two literals from the clause
        string l1 = clauses[index][clauses[index].Count - 2];
        string l2 = clauses[index][clauses[index].Count - 1];

        string index1 = index.ToString() + "-" + (clauses[index].Count - 2).ToString();
        string index2 = index.ToString() + "-" + (clauses[index].Count - 1).ToString();

        clauses.Add(new List<string> { l1, l2, newVar });

        // Adding clause highlighting gadget
        var existing = gadgets.FirstOrDefault(g =>
            g.color == "ClauseHighlight" &&
            g.reductionFromIds.FirstOrDefault() == index.ToString()
        );

        if (existing != null)
            existing.reductionToIds.Add((clauses.Count - 1).ToString());
        else
            gadgets.Add(new Gadget("ClauseHighlight", new List<string> { index.ToString() }, new List<string> { (clauses.Count - 1).ToString() }));

        // Adding element highlighting gadget
        var literal1 = gadgets.FirstOrDefault(g =>
            g.color == "ElementHighlight" &&
            g.reductionToIds.Contains(index1)
        );

        var literal2 = gadgets.FirstOrDefault(g =>
            g.color == "ElementHighlight" &&
            g.reductionToIds.Contains(index2)
        );

        if (literal1 != null) {
            literal1.reductionToIds.Remove(index1);
            literal1.reductionToIds.Add((clauses.Count - 1).ToString() + "-0");
        }
        if (literal2 != null) {
            literal2.reductionToIds.Remove(index2);
            literal2.reductionToIds.Add((clauses.Count - 1).ToString() + "-1");
        }

        // Removing last two literals from the original clause
        clauses[index].RemoveAt(clauses[index].Count - 1);
        clauses[index].RemoveAt(clauses[index].Count - 1);

        clauses[index].Add("!" + newVar);
    }


    public int findSet(List<List<string>> clauses) {
        for (int i = 0; i < clauses.Count; i++) {
            if (clauses[i].Count > 3) return i;
        }
        return -1;
    }


    public string mapSolutions(string reductionFromSolution) {
        return "";
    }

}
