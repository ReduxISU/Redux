using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Logic;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01.Solvers;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01.Verifiers;

namespace API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_INTPROGRAMMING01;

class KarpIntProgStandard : IReversibleReduction<SAT3, INTPROGRAMMING01, Assignment>, IRecordedReduction<SAT3, INTPROGRAMMING01> {

    // --- Fields ---
    public string reductionName { get; } = "Karp's 0-1 Integer Linear Programming Reduction";
    public string reductionDefinition { get; } = "Karp's reduction maps each clause of a SAT problem to a row in a 0-1 integer linear programming matrix.";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Caleb Eardley" };
    // reduce() builds a dense clauses x variables constraint matrix — every cell is
    // explicitly filled (0, 1, or -1), not just the 3 literals actually present per
    // clause, so output size is O(clauses * variables) even for sparse SAT3 input.
    public ReductionCost cost { get; } = ReductionCost.Quadratic;
    // Declared, not derived. Each clause is independently replaced by one inequality
    // row, built from a fixed coefficient rule (-1/1/0) with no coordination needed
    // across rows.
    public ReductionType reductionType { get; } = ReductionType.LocalReplacement;
    // Declared, not derived. Nested loop over clauses x variables to fill the dense
    // coefficient matrix.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Polynomial;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(clauses * variables), sizes from the SAT3 input";
    private Dictionary<Object, Object> _gadgetMap = new Dictionary<Object, Object>();

    private SAT3 _reductionFrom;
    private INTPROGRAMMING01 _reductionTo;


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
    public INTPROGRAMMING01 reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }

    // --- Methods Including Constructors ---
    public KarpIntProgStandard(SAT3 from) : this(from, StepRecorder<BuildDelta>.Off) { }

    // The one place the target is built; rec is Off except when the construction steps are asked for.
    private KarpIntProgStandard(SAT3 from, StepRecorder<BuildDelta> rec) {
        _reductionFrom = from;
        _reductionTo = Build(rec);
    }
    public KarpIntProgStandard(string instance) : this(new SAT3(instance)) { }
    // Builds from the instance with rec recording the construction as it goes (IRecordedReduction).
    public KarpIntProgStandard(string instance, StepRecorder<BuildDelta> rec) : this(new SAT3(instance), rec) { }
    public KarpIntProgStandard() : this(new SAT3()) { }

    /// <summary>
    /// The gadgets (#693), in the 0-1 program's Table picture: each variable becomes a 0-1 variable (its column and its cell in
    /// the row <c>x =</c>); each clause becomes a constraint row (an OR gadget: the row holds exactly when one of its literals
    /// is true), each literal its coefficient cell, and the clause's bound its bound cell.
    /// </summary>
    public List<Gadget> gadgets { get; } = new();

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new KarpIntProgStandard(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    public INTPROGRAMMING01 reduce() => Build(StepRecorder<BuildDelta>.Off);

    // The Table picture's ids (IntProgrammingVisualization): row 0 is the assignment "x =", row r + 1 is constraint r; columns
    // 0..n-1 are the variables, then Total, Bound and Holds.
    private static string Row(int row) => API.Interfaces.Table.TableFrames.RowId(row);
    private static string Column(int column) => API.Interfaces.Table.TableFrames.ColumnId(column);
    private static string Cell(int row, int column) => API.Interfaces.Table.TableFrames.CellId(row, column);

    // Construction steps (see IRecordedReduction): the table's frame, one step per variable (its column), one per clause (its
    // row), then Done. They are recorded from the decisions below, so what is recorded is what is built.
    private INTPROGRAMMING01 Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
        SAT3 SAT3Instance = _reductionFrom;
        INTPROGRAMMING01 reduced01INT = new INTPROGRAMMING01();

        List<int> dVector = new List<int>();
        List<List<int>> Cmatrix = new List<List<int>>();

        //Creates a list of variable from the list of literals- may need updated if SAT3 is changed to
        //include a variable list.
        List<string> variables = new List<string>();
        foreach (var l in SAT3Instance.literals) {
            if (!variables.Contains(l.Replace("!", string.Empty))) {
                variables.Add(l.Replace("!", string.Empty));
            }
        }
        int n = variables.Count;
        string[] totals = [Column(n), Column(n + 1), Column(n + 2)];
        rec.Accept(
            () => new BuildDelta { Nodes = new[] { Row(0) }.Concat(totals).ToArray() },
            () => $"The 0-1 program is a table: the row x = holds one 0-1 value per variable, and each constraint row gets its total, its bound and whether it holds.",
            Row(0));

        for (int j = 0; j < n; j++) {
            int column = j, index = gadgets.Count;
            string variableId = GroupedGraphFrames.VariableId(variables[j]);
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { variableId }.Concat(GroupedGraphFrames.OccurrenceIds(SAT3Instance.clauses, variables[j])).ToList(),
                new List<string> { Column(column), Cell(0, column) }));
            rec.Accept(
                () => new BuildDelta { SourceFocus = [variableId], Nodes = [Column(column)], Gadgets = [index] },
                () => {
                    string x = IntegerProgrammingBruteForce.Variable(column);
                    return x == variables[column]
                        ? $"Variable {x} becomes the 0-1 variable {x}: 1 means {x} is True, 0 means False."
                        : $"Variable {variables[column]} becomes the 0-1 variable {x}: {x} = 1 means {variables[column]} is True, 0 means False.";
                },
                Cell(0, column));
        }

        //Creates the rows or ineqlalities of the matrix C, for each clause in SAT3
        for (int i = 0; i < SAT3Instance.clauses.Count; i++) {
            List<int> row = new List<int>();
            dVector.Add(-1);
            for (int j = 0; j < variables.Count; j++) {
                if (SAT3Instance.clauses[i].Contains(variables[j]) && !SAT3Instance.clauses[i].Contains("!" + variables[j])) {
                    row.Add(-1);
                } else if (!SAT3Instance.clauses[i].Contains(variables[j]) && SAT3Instance.clauses[i].Contains("!" + variables[j])) {
                    row.Add(1);
                } else { row.Add(0); }
                //constructs dVector
                if (SAT3Instance.clauses[i].Contains("!" + variables[j])) {
                    dVector[i] += 1;
                }
            }
            Cmatrix.Add(row);

            int clause = i, tableRow = i + 1, firstGadget = gadgets.Count;
            List<string> literals = SAT3Instance.clauses[clause];
            gadgets.Add(new Gadget(GadgetKind.OrGadget, new List<string>(GroupedGraphFrames.ClauseIds(clause, literals.Count)), new List<string> { Row(tableRow) }));
            for (int j = 0; j < literals.Count; j++)
                gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { GroupedGraphFrames.LiteralId(clause, j) },
                    new List<string> { Cell(tableRow, variables.IndexOf(CnfFormula.VariableOf(literals[j]))) }));
            gadgets.Add(new Gadget(GadgetKind.Bound, new List<string> { GroupedGraphFrames.ClauseId(clause) }, new List<string> { Cell(tableRow, n + 1) },
                note: $"Bound {dVector[clause]}: the number of negated literals minus 1."));
            if (rec.Enabled) {
                int[] gadgetIdx = Enumerable.Range(firstGadget, gadgets.Count - firstGadget).ToArray();
                List<int> coefficients = row;
                int bound = dVector[clause];
                bool cancels = literals.Any(l => !l.StartsWith('!') && literals.Contains("!" + l));
                rec.Accept(
                    () => new BuildDelta { SourceFocus = GroupedGraphFrames.ClauseIds(clause, literals.Count), Nodes = [Row(tableRow)], Gadgets = gadgetIdx },
                    () => $"Clause C{clause + 1} {GroupedGraphFrames.ClauseText(literals)} becomes row {tableRow}: {Inequality(coefficients, bound)}. "
                        + "Each plain literal counts −1 and each negated one +1, and the bound is the number of negated literals minus 1, so the row holds exactly when one of its literals is true."
                        + (cancels ? " A variable written both ways cancels out, so this row always holds." : ""),
                    Row(tableRow));
            }
        }
        // Single-space-separated within each row/vector, matching INTPROGRAMMING01's own
        // parser (getMatrixC/getVectorD both do a naive Split(" ") -- double spaces here
        // produce empty tokens there, which fail int.Parse).
        string Cstring = string.Join(",", Cmatrix.Select(row => "(" + string.Join(" ", row) + ")"));
        string dstring = "(" + string.Join(" ", dVector) + ")";

        string instance = Cstring + "<=" + dstring;

        reduced01INT.C = Cmatrix;
        reduced01INT.d = dVector;
        reduced01INT.instance = instance;

        //K is the number of clauses

        reductionTo = reduced01INT;
        rec.Done(new BuildDelta(), true,
            $"The 0-1 program has {Words.Count(Cmatrix.Count, "constraint")} and {Words.Count(n, "variable")}: some 0-1 assignment meets them all exactly when the formula can be satisfied.");
        // Redux_GUI reads the legacy ids: the formula's pieces in its own words ("0", "0-1"), the target's as they are.
        foreach (Gadget g in gadgets) g.reductionFromIds = GroupedGraphFrames.LegacyIds(g.sourceIds, SAT3Instance.clauses);
        return reduced01INT;
    }

    // "−x1 + x2 − x3 ≤ 0", in the 0-1 program's own variable names.
    private static string Inequality(IReadOnlyList<int> coefficients, int bound) {
        var terms = new List<string>();
        for (int j = 0; j < coefficients.Count; j++) {
            int c = coefficients[j];
            if (c == 0) continue;
            string x = IntegerProgrammingBruteForce.Variable(j);
            string magnitude = Math.Abs(c) == 1 ? x : $"{Math.Abs(c)}{x}";
            terms.Add(terms.Count == 0 ? (c < 0 ? "−" : "") + magnitude : (c < 0 ? "− " : "+ ") + magnitude);
        }
        return $"{(terms.Count == 0 ? "0" : string.Join(" ", terms))} ≤ {(bound < 0 ? "−" + -bound : bound.ToString())}";
    }

    public string mapSolutions(string problemFromSolution) {
        // Check if the colution is correct
        if (!reductionFrom.defaultVerifier.verify(reductionFrom, problemFromSolution)) {
            throw new ReductionInputException(this, problemFromSolution, reductionFrom.certificateFormat,
                "the assignment does not satisfy the 3SAT formula, so there is no 0-1 assignment to map it to");
        }

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
        List<string> mappedSolutionList = new List<string>();
        List<string> variables = new List<string>();
        foreach (string literal in reductionFrom.literals) {
            if (!variables.Contains(literal.Replace("!", ""))) {
                variables.Add(literal.Replace("!", ""));
            }
        }
        foreach (string variable in variables) {
            if (solutionList.Contains(variable)) {
                mappedSolutionList.Add("1");
            } else {
                mappedSolutionList.Add("0");
            }
        }
        string problemToSolution = "";
        foreach (string num in mappedSolutionList) {
            problemToSolution += num + ' ';
        }
        return '(' + problemToSolution.TrimEnd(' ') + ')';
    }

    public Assignment EmptyAnswer() => new Assignment([]);

    // "{}" is how a 0-1 program says it has no answer (#797): never an assignment, so it keeps the default. A's answer is an
    // assignment, never "{}".

    /// <summary>
    /// Backward map (#693): a 0-1 assignment to a truth assignment. The j-th 0-1 variable is the j-th variable of the formula
    /// (in the order reduce() numbered them), and 1 means True. A value other than 0 or 1 says nothing: the variable is set to
    /// False and the step is a Reject; the Done step then reports what 3SAT's verifier says.
    /// </summary>
    public (string Answer, Assignment Partial) MapBack(string problemToSolution, StepRecorder<Assignment> rec) {
        var variables = new List<string>();
        foreach (var l in reductionFrom.literals)
            if (!variables.Contains(l.Replace("!", string.Empty))) variables.Add(l.Replace("!", string.Empty));

        List<int> x;
        try {
            x = new GenericVerifier01INTP().parseCertificate(problemToSolution.Trim());
        } catch (FormatException) {
            throw new FormatException($"'{problemToSolution}' is not a 0-1 assignment like (1 0 1)");
        }
        if (x.Count != variables.Count)
            throw new FormatException($"'{problemToSolution}' gives {x.Count} values, but the 0-1 program has {variables.Count} variables");

        var values = new Dictionary<string, bool>();
        for (int j = 0; j < variables.Count; j++) {
            string variable = variables[j], name = IntegerProgrammingBruteForce.Variable(j);
            int value = x[j];
            values[variable] = value == 1;
            string of = name == variable ? "" : $" (the 0-1 variable for {variable})";
            if (value is 0 or 1)
                rec.Accept(() => GroupedGraphFrames.ToAssignment(values), () => $"{name} = {value}{of}, so {variable} = {Assignment.Word(value == 1)}.", Cell(0, j));
            else
                rec.Reject(() => GroupedGraphFrames.ToAssignment(values), () => $"{name} = {value}{of} is not 0 or 1, so it says nothing about {variable}: {variable} = False.", Cell(0, j));
        }
        return (SAT.Certificate(variables, values), GroupedGraphFrames.ToAssignment(values));
    }
}
