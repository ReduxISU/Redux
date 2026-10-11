using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using API.Interfaces.Tripartite;
using API.Problems.NPComplete.NPC_DM3;
using API.Problems.NPComplete.NPC_SAT;

namespace API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_DM3;

class GareyJohnson : IReversibleReduction<SAT3, DM3, Assignment>, IRecordedReduction<SAT3, DM3> {

    // --- Fields ---
    public string reductionName { get; } = "Garey-Johnson Reduction";
    public string reductionDefinition { get; } = "Garey and Johnson Reduction converts 3SAT to a set of elements, and constraints of a 3-dimensional matching problem. The varibles are represented by wheels of 2 constraints for each clause a variable is in. The clauses are each mapped to a group of contraints all sharing two elements, with the third attaching to a varible gadget. Garbage collection gadgets are than created as constrains that assure any unincluded elements outside of the clause gadget are included in a matching.";
    public string source { get; } = "Garey, M. R. and David S. Johnson. “Computers and Intractability: A Guide to the Theory of NP-Completeness.” (1978).";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Caleb Eardley" };
    // The garbage-collection gadget loop ("for i in 0..(literals-clauses): foreach j in
    // Z") unconditionally emits one M-triple per (i,j) pair — O(n^2) matching triples.
    public ReductionCost cost { get; } = ReductionCost.Quadratic;
    // Declared, not derived. Per its own reductionDefinition: variable "wheel" gadgets,
    // clause gadgets, and garbage-collection gadgets are all constructed to interact --
    // the canonical Garey & Johnson component-design reduction.
    public ReductionType reductionType { get; } = ReductionType.ComponentDesign;
    // Declared, not derived. The garbage-collection loop emits one M-triple per
    // (literal, Z-entry) pair.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Polynomial;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(n^2), n ~ |SAT3.literals|";
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
    public GareyJohnson(SAT3 from) : this(from, StepRecorder<BuildDelta>.Off) { }

    // The one place the target is built; rec is Off except when the construction steps are asked for.
    private GareyJohnson(SAT3 from, StepRecorder<BuildDelta> rec) {
        _reductionFrom = from;
        _reductionTo = Build(rec);
    }
    public GareyJohnson(string instance) : this(new SAT3(instance)) { }
    // Builds from the instance with rec recording the construction as it goes (IRecordedReduction).
    public GareyJohnson(string instance, StepRecorder<BuildDelta> rec) : this(new SAT3(instance), rec) { }
    public GareyJohnson() : this(new SAT3()) { }

    /// <summary>
    /// The gadgets (#693), in the Tripartite Matching picture's ids (the i-th element of X, Y and Z is x{i}, y{i}, z{i}; the
    /// i-th triple of M is T{i}): one wheel per variable (a group: its elements and triples, built from the variable and every
    /// occurrence of it), one OR gadget per clause (its x and y elements and one triple per literal), one element per literal
    /// occurrence (the z element it takes and its triple), and the garbage collection (a palette: the pairs that soak up the
    /// z elements no clause takes).
    /// </summary>
    public List<Gadget> gadgets { get; } = new();

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new GareyJohnson(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    /***************************************************
     * reduce() called after GareyAndJohnsonReduction reduction, and returns a THREE_DM object, that
     * is a reduction from the SAT3 object passed into GareyAndJohnsonReduction.
     */

    public DM3 reduce() => Build(StepRecorder<BuildDelta>.Off);

    // Construction steps (see IRecordedReduction): one per variable (its wheel), one per clause (its OR gadget), one for the
    // garbage collection, then Done. Each records the positions reduce() gives its pieces as it adds them, so what is recorded
    // is what is built.
    private DM3 Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
        SAT3 SAT3Instance = _reductionFrom;
        DM3 reduced3DM = new DM3();

        List<string> X = new List<string>();
        List<string> Y = new List<string>();
        List<string> Z = new List<string>();
        List<List<string>> M = new List<List<string>>();
        string instance = "";

        // The picture's ids of the pieces added since the last mark, in the order they are added: elements, then triples.
        int xMark = 0, yMark = 0, zMark = 0, mMark = 0;
        (List<string> Elements, List<string> Triples) Added() {
            var elements = Enumerable.Range(xMark, X.Count - xMark).Select(TripartiteFrames.XId)
                .Concat(Enumerable.Range(yMark, Y.Count - yMark).Select(TripartiteFrames.YId))
                .Concat(Enumerable.Range(zMark, Z.Count - zMark).Select(TripartiteFrames.ZId)).ToList();
            var triples = Enumerable.Range(mMark, M.Count - mMark).Select(TripartiteFrames.TripleId).ToList();
            (xMark, yMark, zMark, mMark) = (X.Count, Y.Count, Z.Count, M.Count);
            return (elements, triples);
        }

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
                // The next spoke's y (Garey and Johnson's a[j+1]): a shift of 1 makes the wheel one cycle, so every spoke
                // takes the same value. A shift of 2 split a wheel of an even size into two halves that could disagree (#819).
                M.Add(new List<string> { X[X.Count - 1], "y_" + literal + "_" + ((i + 1) % count).ToString(), Z[Z.Count - 1] });
            }
            var (elements, triples) = Added();
            string variableId = GroupedGraphFrames.VariableId(literal);
            int index = gadgets.Count;
            gadgets.Add(new Gadget(GadgetKind.Group, new List<string> { variableId }.Concat(CnfPieces.OccurrenceIds(SAT3Instance.clauses, literal)).ToList(),
                elements.Concat(triples).ToList()));
            if (rec.Enabled) {
                string variable = literal;
                int spokes = count;
                rec.Accept(
                    () => new BuildDelta { SourceFocus = [variableId], Nodes = elements.Concat(triples).ToArray(), Gadgets = [index] },
                    () => $"Variable {variable} occurs {BuildCaptions.Count(spokes, "time")}, so it becomes a wheel of {BuildCaptions.Count(spokes, "spoke")}: "
                        + $"x_{variable}_i and y_{variable}_i, the markers z_{variable}_i and z_!{variable}_i, and {BuildCaptions.Count(2 * spokes, "triple")}. "
                        + $"Covering the x's and y's takes either every z_{variable} marker or every z_!{variable} marker, so the wheel picks one value for {variable}: "
                        + $"it leaves the z_{variable} markers free for clauses with {variable} when {variable} is True, the z_!{variable} markers when it is False.",
                    elements.Concat(triples).ToArray());
            }
        }
        // clause gadget
        List<string> unusedLiterals = new List<string>(Z);
        for (int i = 0; i < SAT3Instance.clauses.Count; i++) {
            var takes = new List<(int Position, string Z, int Triple)>();
            for (int j = 0; j < SAT3Instance.clauses[i].Count; j++) {
                string literal = SAT3Instance.clauses[i][j];
                string? found = unusedLiterals.Find(x => IsElementOf(x, literal));
                if (found is null) {
                    continue;
                }
                M.Add(new List<string> { "x_clause_" + i.ToString(), "y_clause" + i.ToString(), found });
                unusedLiterals.Remove(found);
                takes.Add((j, found, M.Count - 1));
            }
            X.Add("x_clause_" + i.ToString());
            Y.Add("y_clause" + i.ToString());

            var (elements, triples) = Added();
            int clause = i, firstGadget = gadgets.Count;
            List<string> literals = SAT3Instance.clauses[clause];
            string[] sourceIds = CnfPieces.ClauseIds(clause, literals.Count);
            // The clause's own elements (x_clause, y_clause) come first, then its triples.
            gadgets.Add(new Gadget(GadgetKind.OrGadget, new List<string>(sourceIds), elements.Concat(triples).ToList()));
            foreach (var take in takes)
                gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { GroupedGraphFrames.LiteralId(clause, take.Position) },
                    new List<string> { TripartiteFrames.ZId(Z.IndexOf(take.Z)), TripartiteFrames.TripleId(take.Triple) }));
            if (rec.Enabled) {
                int[] gadgetIdx = Enumerable.Range(firstGadget, gadgets.Count - firstGadget).ToArray();
                var markers = takes.Select(t => t.Z).ToList();
                rec.Accept(
                    () => new BuildDelta { SourceFocus = sourceIds, Nodes = elements.Concat(triples).ToArray(), Gadgets = gadgetIdx },
                    () => $"Clause C{clause + 1} {CnfPieces.ClauseText(literals)} becomes x_clause_{clause} and y_clause{clause} with one triple per literal, "
                        + $"through {TripartiteFrames.AndList(markers)}. Covering x_clause_{clause} takes one of those markers, and a marker is only free when its literal is true.",
                    elements.Concat(triples).ToArray());
            }
        }
        // gaebage gadget
        for (int i = 0; i < SAT3Instance.literals.Count() - SAT3Instance.clauses.Count(); i++) {
            foreach (var j in Z) {
                M.Add(new List<string> { "x_garb_" + i.ToString(), "y_garb_" + i.ToString(), j });
            }
            X.Add("x_garb_" + i.ToString());
            Y.Add("y_garb_" + i.ToString());
        }
        int pairs = SAT3Instance.literals.Count() - SAT3Instance.clauses.Count();
        if (pairs > 0) {
            var (elements, triples) = Added();
            int index = gadgets.Count;
            gadgets.Add(new Gadget(GadgetKind.Palette, new List<string>(), elements.Concat(triples).ToList(),
                note: "Garbage collection: one x, y pair for each occurrence beyond the number of clauses, each with a triple to every z, to cover the markers no clause takes."));
            if (rec.Enabled) {
                int occurrences = SAT3Instance.literals.Count, zs = Z.Count;
                rec.Accept(
                    () => new BuildDelta { Nodes = elements.Concat(triples).ToArray(), Gadgets = [index] },
                    () => $"Garbage collection: {BuildCaptions.Count(pairs, "pair")} x_garb_i and y_garb_i, each with a triple to every one of the {zs} z elements. "
                        + $"The wheels take half of the z's and the clauses one each, so {occurrences} − {SAT3Instance.clauses.Count} = {pairs} are left for these pairs to soak up.",
                    elements.Concat(triples).ToArray());
            }
        }

        // A DM3 instance starts with its three sets, {X}{Y}{Z}, and then lists M's triples (#807).
        instance = "{" + string.Join(",", X) + "}{" + string.Join(",", Y) + "}{" + string.Join(",", Z) + "}";
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

        rec.Done(new BuildDelta(), true,
            $"{BuildCaptions.Count(X.Count, "element")} in each of X, Y and Z and {BuildCaptions.Count(M.Count, "triple")}: "
            + "some triples cover every element exactly once exactly when the formula can be satisfied.");

        // Redux_GUI reads the legacy ids: the formula's pieces in its own words ("0", "0-1"), the target's as they are.
        foreach (Gadget g in gadgets) g.reductionFromIds = CnfPieces.LegacyIds(g.sourceIds, SAT3Instance.clauses);
        return reduced3DM;
    }

    /// <summary>
    /// Whether <paramref name="z"/> is one of <paramref name="literal"/>'s own Z elements, <c>z_&lt;literal&gt;_&lt;k&gt;</c>. A
    /// substring test also matched every variable whose name starts with the literal (x1 took z_x10_0, a took z_a_1_0, #820).
    /// </summary>
    private static bool IsElementOf(string z, string literal) {
        string prefix = "z_" + literal + "_";
        return z.StartsWith(prefix, StringComparison.Ordinal) && z.Length > prefix.Length && z[prefix.Length..].All(char.IsAsciiDigit);
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
                    string yName = "y_" + variable + "_" + ((i + 1) % count);
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
                string? found = availableZ.Find(z => IsElementOf(z, literal));
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

    public Assignment EmptyAnswer() => new Assignment([]);

    // "{}" as B's answer is no matching (X is never empty here), so it keeps the default: no answer. A's answer is an
    // assignment, never "{}".

    /// <summary>
    /// Backward map (#693): a perfect matching to an assignment. A wheel takes every z_v marker or every z_!v marker (#819),
    /// so each variable is read off its first spoke, x_v_0: the triple that covers it is (x_v_0, y_v_0, z_v_0), which takes the
    /// z_v markers and leaves the z_!v ones for the clauses (v = False), or (x_v_0, y_v_1, z_!v_0), which leaves the z_v
    /// markers (v = True). Variables are read in the order the formula first mentions them. A spoke no triple of the answer
    /// covers says nothing: the variable is set to False and the step is a Reject; the Done step then reports what 3SAT's
    /// verifier says.
    /// </summary>
    public (string Answer, Assignment Partial) MapBack(string problemToSolution, StepRecorder<Assignment> rec) {
        List<int> chosen = ReadMatching(problemToSolution);
        var values = new Dictionary<string, bool>();
        var order = CnfPieces.Variables(reductionFrom.clauses);
        foreach (string variable in order) {
            string spoke = "x_" + variable + "_0";
            int t = chosen.FirstOrDefault(k => reductionTo.M[k][0] == spoke, -1);
            if (t < 0) {
                values[variable] = false;
                rec.Reject(() => CnfPieces.Snapshot(values),
                    () => $"No triple of the matching covers {spoke}, so {variable}'s wheel says nothing: {variable} = False.",
                    TripartiteFrames.XId(reductionTo.X.IndexOf(spoke)));
                continue;
            }
            var triple = reductionTo.M[t];
            bool value = triple[2] == "z_!" + variable + "_0";
            values[variable] = value;
            rec.Accept(() => CnfPieces.Snapshot(values),
                () => $"{TripartiteFrames.TripleId(t)} ({string.Join(", ", triple)}) is in the matching: {variable}'s wheel takes its z_{(value ? "!" : "")}{variable} markers "
                    + $"and leaves the z_{(value ? "" : "!")}{variable} ones for the clauses, so {variable} = {CnfPieces.Word(value)}.",
                TripartiteFrames.TripleId(t));
        }
        return (CnfPieces.Certificate(order, values), CnfPieces.Snapshot(values));
    }

    // A matching as 3-Dimensional Matching writes it, {{x,y,z},{x,y,z}} (or {x,y,z}{x,y,z}): each listed triple is the first
    // triple of M with those three names not taken yet. Malformed text, or a triple that is not in M, throws FormatException,
    // which the endpoint reports as a 400.
    private List<int> ReadMatching(string certificate) {
        string text = certificate.Trim();
        if (text.Length < 2 || text[0] != '{' || text[^1] != '}')
            throw new FormatException($"'{certificate}' is not a set of triples like {{{{x,y,z}},{{x,y,z}}}}");
        var listed = System.Text.RegularExpressions.Regex.Matches(text, @"\{([^{}]*)\}").Select(m => m.Groups[1].Value).ToList();
        var taken = new bool[reductionTo.M.Count];
        var chosen = new List<int>();
        foreach (string triple in listed) {
            string[] names = triple.Split(',').Select(n => n.Trim()).ToArray();
            if (names.Length != 3)
                throw new FormatException($"'{{{triple}}}' is not a triple of three names");
            int k = Enumerable.Range(0, reductionTo.M.Count).FirstOrDefault(j => !taken[j] && reductionTo.M[j].SequenceEqual(names), -1);
            if (k < 0)
                throw new FormatException($"'{{{triple}}}' is not a triple of M");
            taken[k] = true;
            chosen.Add(k);
        }
        return chosen;
    }
}
