using API.Interfaces;
using API.Problems.NPComplete.NPC_DM3;
using API.Problems.NPComplete.NPC_SAT;

namespace API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_DM3;

class GareyJohnson : IReduction<SAT3, DM3> {

    // --- Fields ---
    public string reductionName { get; } = " Garey & Johnson Reduction";
    public string reductionDefinition { get; } = "Garey and Johnson Reduction converts 3SAT to a set of elements, and constraints of a 3-dimensional matching problem. The varibles are represented by wheels of 2 constraints for each clause a variable is in. The clauses are each mapped to a group of contraints all sharing two elements, with the third attaching to a varible gadget. Garbage collection gadgets are than created as constrains that assure any unincluded elements outside of the clause gadget are included in a matching.";
    public string source { get; } = "Garey, M. R. and David S. Johnson. “Computers and Intractability: A Guide to the Theory of NP-Completeness.” (1978).";
    public string[] contributors { get; } = { "Caleb Eardley" };
    // The garbage-collection gadget loop ("for i in 0..(literals-clauses): foreach j in
    // Z") unconditionally emits one M-triple per (i,j) pair — O(n^2) matching triples.
    public ReductionCost cost { get; } = ReductionCost.Quadratic;
    private Dictionary<Object, Object> _gadgetMap = new Dictionary<Object, Object>();

    private SAT3 _reductionFrom;
    private DM3 _reductionTo;


    // --- Properties ---
    public Dictionary<Object, Object> gadgetMap {
        get {
            return _gadgetMap;
        }
        set {
            _gadgetMap = value;
        }
    }
    public SAT3 reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public DM3 reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }

    // --- Methods Including Constructors ---
    public GareyJohnson(SAT3 from) {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public GareyJohnson(string instance) : this(new SAT3(instance)) { }
    public GareyJohnson() : this(new SAT3()) { }
    /***************************************************
     * reduce() called after GareyAndJohnsonReduction reduction, and returns a THREE_DM object, that
     * is a reduction from the SAT3 object passed into GareyAndJohnsonReduction.
     */

    public DM3 reduce() {
        SAT3 SAT3Instance = _reductionFrom;
        DM3 reduced3DM = new DM3();

        List<string> X = new List<string>();
        List<string> Y = new List<string>();
        List<string> Z = new List<string>();
        List<List<string>> M = new List<List<string>>();
        string instance = "";

        List<string> variables = new List<string>();

        //Creates a list of variable from the list of literals- may need updated if SAT3 is changed to
        //include a variable list.
        foreach (var l in SAT3Instance.literals) {
            if (!variables.Contains(l.Replace("!", string.Empty))) {
                variables.Add(l.Replace("!", string.Empty));
            }
        }
        // variable gadget
        foreach (var literal in variables) {
            int count = SAT3Instance.literals.Count(x => x.Replace("!", string.Empty) == literal);
            for (int i = 0; i < count; i++) {
                X.Add("x_" + literal + "_" + i.ToString());
                Y.Add("y_" + literal + "_" + i.ToString());
                Z.Add("z_" + literal + "_" + i.ToString());
                M.Add(new List<string> { X[X.Count - 1], Y[Y.Count - 1], Z[Z.Count - 1] });
                Z.Add("z_" + "!" + literal + "_" + i.ToString());
                M.Add(new List<string> { X[X.Count - 1], "y_" + literal + "_" + ((i + 2) % count).ToString(), Z[Z.Count - 1] });
            }
        }
        // clause gadget
        List<string> unusedLiterals = new List<string>(Z);
        for (int i = 0; i < SAT3Instance.clauses.Count; i++) {
            foreach (var literal in SAT3Instance.clauses[i]) {
                string? found = unusedLiterals.Find(x => x.Contains("z_" + literal));
                if (found is null) {
                    continue;
                }
                M.Add(new List<string> { "x_clause_" + i.ToString(), "y_clause" + i.ToString(), found });
                unusedLiterals.Remove(found);
            }
            X.Add("x_clause_" + i.ToString());
            Y.Add("y_clause" + i.ToString());
        }
        // gaebage gadget
        for (int i = 0; i < SAT3Instance.literals.Count() - SAT3Instance.clauses.Count(); i++) {
            foreach (var j in Z) {
                M.Add(new List<string> { "x_garb_" + i.ToString(), "y_garb_" + i.ToString(), j });
            }
            X.Add("x_garb_" + i.ToString());
            Y.Add("y_garb_" + i.ToString());
        }

        foreach (var i in M) {
            instance += "{";
            foreach (var j in i) {
                instance += j + ",";
            }
            instance = instance.TrimEnd(',') + "}";
        }

        reduced3DM.X = X;
        reduced3DM.Y = Y;
        reduced3DM.Z = Z;
        reduced3DM.M = M;
        reduced3DM.instance = instance;

        //return new THREE_DM();
        return reduced3DM;
    }

    public string mapSolutions(string problemFromSolution) {
        //Parse out given solution
        //Parse problemFromSolution into a list of nodes
        List<string> solutionList = problemFromSolution.Replace(" ", "").Replace("(", "").Replace(")", "").Split(",").ToList();
        for (int i = 0; i < solutionList.Count; i++) {
            string[] tempSplit = solutionList[i].Split(":");
            if (tempSplit[1] == "False") {
                solutionList[i] = "!" + tempSplit[0];
            } else if (tempSplit[1] == "True") {
                solutionList[i] = tempSplit[0];
            } else { solutionList[i] = ""; }

        }
        solutionList.RemoveAll(x => string.IsNullOrEmpty(x));

        //Map solution
        // Node names below mirror reduce()'s own naming scheme exactly: x_<literal>_<i> /
        // y_<literal>_<i> / z_<literal>_<i> (variable gadget), x_clause_<i> / y_clause<i>
        // (clause gadget), and x_garb_<i> / y_garb_<i> (garbage-collection gadget) -- see
        // reduce() above. mappedSolutionList's triples must reference only names that
        // actually exist in reductionTo.X/Y/Z.
        List<string> mappedSolutionList = new List<string>();
        List<string> variables = new List<string>();
        foreach (string literal in reductionFrom.literals) {
            if (!variables.Contains(literal.Replace("!", ""))) {
                variables.Add(literal.Replace("!", ""));
            }
        }

        // availableZ tracks the Z-elements the variable gadget leaves unmatched for each
        // occurrence -- those are the elements the clause/garbage gadgets may attach to
        // (mirrors reduce()'s own "unusedLiterals" bookkeeping).
        List<string> availableZ = new List<string>();

        // mapping of solution to variable gadgets. reduce() emits two candidate triples
        // per occurrence i: (x_i, y_i, z_i) and (x_i, y_{(i+2)%count}, z_!_i). Picking
        // the same candidate for every occurrence of a variable is what keeps the y's
        // from colliding (identity vs. the (i+2)%count permutation, each a bijection
        // over 0..count-1). We pick the z_!_i candidate when the variable is True --
        // that leaves every z_<variable>_<i> (the "true" markers) available below for
        // clause gadgets whose satisfying literal is the positive one; and we pick the
        // z_i candidate when False, leaving the z_!<variable>_<i> markers available for
        // clauses satisfied by the negative literal.
        foreach (string variable in variables) {
            int count = reductionFrom.literals.Count(x => x.Replace("!", "") == variable);
            bool isTrue = solutionList.Contains(variable);
            for (int i = 0; i < count; i++) {
                string xName = "x_" + variable + "_" + i;
                if (isTrue) {
                    string yName = "y_" + variable + "_" + ((i + 2) % count);
                    string zName = "z_!" + variable + "_" + i;
                    mappedSolutionList.Add(string.Format("{{{0},{1},{2}}}", xName, yName, zName));
                    availableZ.Add("z_" + variable + "_" + i);
                } else {
                    string yName = "y_" + variable + "_" + i;
                    string zName = "z_" + variable + "_" + i;
                    mappedSolutionList.Add(string.Format("{{{0},{1},{2}}}", xName, yName, zName));
                    availableZ.Add("z_!" + variable + "_" + i);
                }
            }
        }

        // mapping solution to clause gadgets
        for (int i = 0; i < reductionFrom.clauses.Count; i++) {
            foreach (string literal in reductionFrom.clauses[i]) {
                if (!solutionList.Contains(literal)) {
                    continue;
                }
                string? found = availableZ.Find(z => z.Contains("z_" + literal + "_"));
                if (found is null) {
                    continue;
                }
                mappedSolutionList.Add(string.Format("{{x_clause_{0},y_clause{0},{1}}}", i, found));
                availableZ.Remove(found);
                break;
            }
        }

        // mapping solution to garbage collection gadgets
        int garbageCount = reductionFrom.literals.Count - reductionFrom.clauses.Count;
        for (int i = 0; i < garbageCount && i < availableZ.Count; i++) {
            mappedSolutionList.Add(string.Format("{{x_garb_{0},y_garb_{0},{1}}}", i, availableZ[i]));
        }

        //convert mappedSolutionList to one string
        string problemToSolution = "";
        foreach (string hyperEdge in mappedSolutionList) {
            problemToSolution += hyperEdge + ',';
        }
        return '{' + problemToSolution.TrimEnd(',') + '}';
    }
}