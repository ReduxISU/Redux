using API.Interfaces;
using API.Interfaces.Logic;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_GRAPHCOLORING;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers;

namespace API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_GRAPHCOLORING;

// Karp's 3SAT to Graph Coloring with K = 3: a palette triangle F, T, B; a node per literal, joined to B and to its negation,
// so it takes the true or the false color; and per clause a 6-node OR gadget wired to its literals, whose output is joined to
// F and B, so it can be colored only when one of the literals takes the true color. It records the construction steps (the
// palette, one per variable, one per clause, then the Done; see Build) and the backward map's steps (one per variable read;
// see MapBack).
class KarpReduceGRAPHCOLORING : IReversibleReduction<SAT3, GRAPHCOLORING, Assignment>, IRecordedReduction<SAT3, GRAPHCOLORING> {


    #region Fields
    public string reductionName { get; } = "Karp's Graph Coloring Reduction";
    public string reductionDefinition { get; } = "Karp's reduction converts each clause from a 3CNF into an OR gadgets to establish the truth assignments using labels.";
    public string source { get; } = "http://cs.bme.hu/thalg/3sat-to-3col.pdf.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Daniel Igbokwe" };
    // The literal-negation loop iterates O(v^2) times but each distinct literal has at
    // most one negation partner, so real edges added there are O(v); clause-gadget
    // edges are O(1) per clause. Total real output edges are O(v + c), despite the
    // O(v^2)-iteration scan — not the same claim as the pre-existing `complexity` field
    // below (unrelated, unverified runtime-complexity note).
    public ReductionCost cost { get; } = ReductionCost.Linear;
    // Declared, not derived. Palette nodes, per-variable true/false gadgets, and
    // per-clause 6-node OR-gadgets are all wired together against the shared palette --
    // a classic component-design 3SAT-to-coloring construction.
    public ReductionType reductionType { get; } = ReductionType.ComponentDesign;
    // Declared, not derived. Dominant term is the literal-negation loop over all
    // variable pairs.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Polynomial;
    private Dictionary<Object, Object> _gadgetMap = new Dictionary<Object, Object>();

    private SAT3 _reductionFrom;
    private GRAPHCOLORING _reductionTo;
    private string _complexity = "O(n^2)";

    #endregion

    #region Properties
    public string complexity {
        get {
            return _complexity;
        }

        set {
            _complexity = value;
        }
    }
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


    public GRAPHCOLORING reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }

    #endregion

    #region Constructors
    public KarpReduceGRAPHCOLORING(SAT3 from) : this(from, StepRecorder<BuildDelta>.Off) { }

    // The one place the target is built; rec is Off except when the construction steps are asked for.
    private KarpReduceGRAPHCOLORING(SAT3 from, StepRecorder<BuildDelta> rec) {
        _reductionFrom = from;
        _reductionTo = Build(rec);
    }
    public KarpReduceGRAPHCOLORING(string instance) : this(new SAT3(instance)) { }
    // Builds from the instance with rec recording the construction as it goes (IRecordedReduction).
    public KarpReduceGRAPHCOLORING(string instance, StepRecorder<BuildDelta> rec) : this(new SAT3(instance), rec) { }
    public KarpReduceGRAPHCOLORING() : this(new SAT3()) { }

    # endregion

    # region Methods

    /// <summary>
    /// The gadgets: the palette triangle F, T, B; one element per variable (its literal nodes, built from the variable
    /// and every occurrence of it); one OR gadget per clause (its six nodes C{i}N0..N5); and the bound, K = 3.
    /// </summary>
    public List<Gadget> gadgets { get; } = new();

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new KarpReduceGRAPHCOLORING(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    public GRAPHCOLORING reduce() => Build(StepRecorder<BuildDelta>.Off);

    // The nodes one gadget brings and the edges it adds, in the order reduce() adds them.
    private sealed class Piece {
        public readonly List<string> Nodes = new();
        public readonly List<BuildEdge> Edges = new();
    }

    // How the construction steps show a gadget (the palette, a variable or a clause): what it is made from in 3SAT, the nodes
    // and edges it adds to the graph, and the gadget. The state is (SourceFocus, Piece, Gadget): the ids of what it is made
    // from (none for the palette), its nodes and edges, and its gadget's index.
    private static RunSteps<(string[] SourceFocus, Piece Piece, int Gadget), BuildDelta> NewGadgetSteps(StepRecorder<BuildDelta> rec) => new(rec,
        // What the Reduction View shows: what the gadget is made from, the nodes and edges added, and the gadget.
        partial: s => new BuildDelta { SourceFocus = s.SourceFocus, Nodes = s.Piece.Nodes.ToArray(), Edges = BuildEdge.Sorted(s.Piece.Edges), Gadgets = [s.Gadget] },
        focus: s => s.Piece.Nodes.ToArray());

    //The below code is reducing the SAT3 instance to a GRAPHCOLORING instance.
    // Builds the graph, recording the construction steps as it goes (see IRecordedReduction). Its steps:
    //   - an Accept for the palette: F, T and B in a triangle;
    //   - an Accept per variable: its literal nodes, joined to B and to each other;
    //   - an Accept per clause: its OR gadget and the edges that wire it to its literals and the palette;
    //   - the Done: K = 3.
    // A construction has no verdicts, so Accept means "these pieces go into the target". The edges are added in the order the
    // instance has always listed them, and each one is filed under the gadget that adds it, so what is recorded is what is
    // built; each gadget's step is recorded where the gadget is made, once the graph is done.
    // Gadgets: the palette, an element per variable (its literal nodes), an OR gadget per clause (its six nodes), then K.
    private GRAPHCOLORING Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();

        // color palette
        // 0 : False, 1 : True,  2 : Base

        string[] palette = { "F", "T", "B" };

        SAT3 SAT3Instance = _reductionFrom;
        GRAPHCOLORING reducedGRAPHCOLORING = new GRAPHCOLORING();
        Dictionary<string, string> coloring = new Dictionary<string, string>();

        var palettePiece = new Piece();
        palettePiece.Nodes.AddRange(palette);
        var variableNames = CnfFormula.VariablesOf(SAT3Instance.clauses);
        var byVariable = variableNames.ToDictionary(v => v, _ => new Piece(), StringComparer.Ordinal);
        var byClause = SAT3Instance.clauses.Select(_ => new Piece()).ToList();
        // Adds the edge as reduce() always has, and files it under the gadget that adds it when it is new.
        void Link(string x, string y, List<KeyValuePair<string, string>> all, List<string> instanceEdges, Piece piece) {
            int before = all.Count;
            addEdge(x, y, all, instanceEdges);
            if (all.Count > before) piece.Edges.Add(new BuildEdge(x, y));
        }


        // ------- Add nodes -------

        List<string> nodes = new List<string>(palette);

        for (int i = 0; i < palette.Length; i++) {
            coloring.Add(palette[i], i.ToString());
        }

        List<string> variables = SAT3Instance.literals.Distinct().ToList();

        for (int i = 0; i < variables.Count; i++) {
            nodes.Add(variables[i]);
            coloring.Add(variables[i], "-1");
            byVariable[CnfFormula.VariableOf(variables[i])].Nodes.Add(variables[i]);
        }

        // Create clause nodes
        List<List<string>> clauses = new List<List<string>>();
        for (int i = 0; i < SAT3Instance.clauses.Count; i++) {

            List<string> tempClause = new List<string>();

            for (int j = 0; j < 6; j++) {
                tempClause.Add("C" + i + "N" + j);
                coloring.Add("C" + i + "N" + j, "-1");
            }

            // Add clause-nodes to list of clauses
            clauses.Add(tempClause);
            byClause[i].Nodes.AddRange(tempClause);

            // Add clause to node list
            nodes.AddRange(tempClause);
        }

        //Set GRAPHCOLORING nodes
        reducedGRAPHCOLORING.nodes = nodes;


        // -------------  Add edges -----------------------
        List<KeyValuePair<string, string>> edges = new List<KeyValuePair<string, string>>();

        // holds edges for parsing problem instance
        List<string> instanceEdges = new List<string>();


        // Connect palette edges
        for (int i = 0; i < palette.Length; i++) {
            for (int j = 0; j < palette.Length; j++) {
                if (i != j) {
                    Link(palette[i], palette[j], edges, instanceEdges, palettePiece);
                }
            }
        }


        // Connect variable edges to palette color blue
        // Can only be colored True or false can't be base
        for (int i = 0; i < variables.Count; i++) {
            Link(variables[i], palette[2], edges, instanceEdges, byVariable[CnfFormula.VariableOf(variables[i])]);

        }

        // Connect literal to literal negation
        // x1 and !x1 can't have the same color
        for (int i = 0; i < variables.Count; i++) {
            for (int j = 0; j < variables.Count; j++) {
                if (variables[i].Replace("!", "") == variables[j].Replace("!", "") && variables[i] != variables[j]) {
                    Link(variables[i], variables[j], edges, instanceEdges, byVariable[CnfFormula.VariableOf(variables[i])]);
                }
            }
        }

        // Create clause gadget
        // Each clause contains 6 nodes
        for (int i = 0; i < clauses.Count; i++) {
            // Connect   (a V b )
            for (int j = 0; j < 3; j++) {
                for (int k = 0; k < 3; k++) {
                    if (k != j) { Link(clauses[i][j], clauses[i][k], edges, instanceEdges, byClause[i]); }
                }

            }

            // Connect  ((a V b) V c )
            for (int j = 3; j < clauses[i].Count; j++) {
                for (int k = 3; k < clauses[i].Count; k++) {
                    if (k != j) { Link(clauses[i][j], clauses[i][k], edges, instanceEdges, byClause[i]); }
                }
            }


            // Join ((a V b) V c )
            for (int j = 2; j < 4; j++) {

                for (int k = 2; k < 4; k++) {

                    if (k != j) { Link(clauses[i][j], clauses[i][k], edges, instanceEdges, byClause[i]); }
                }
            }

        }


        // Combine clause, variable and palette

        for (int i = 0; i < clauses.Count; i++) {


            // Connect variables to clause gadgets
            List<string> clauseLiterals = PaddedClause(SAT3Instance.clauses[i]);
            Link(clauseLiterals[0], clauses[i][0], edges, instanceEdges, byClause[i]);
            Link(clauseLiterals[1], clauses[i][1], edges, instanceEdges, byClause[i]);
            Link(clauseLiterals[2], clauses[i][4], edges, instanceEdges, byClause[i]);



            Link(clauses[i][0], clauseLiterals[0], edges, instanceEdges, byClause[i]);
            Link(clauses[i][1], clauseLiterals[1], edges, instanceEdges, byClause[i]);
            Link(clauses[i][4], clauseLiterals[2], edges, instanceEdges, byClause[i]);


            // Connect palette base node to (a V b)
            Link(clauses[i][2], palette[2], edges, instanceEdges, byClause[i]);
            Link(palette[2], clauses[i][2], edges, instanceEdges, byClause[i]);

            // Connect palette nodes to clauses ((a V b) V c )

            // palette : False
            Link(clauses[i][5], palette[0], edges, instanceEdges, byClause[i]);
            Link(palette[0], clauses[i][5], edges, instanceEdges, byClause[i]);


            //  palette : Base
            Link(clauses[i][5], palette[2], edges, instanceEdges, byClause[i]);
            Link(palette[2], clauses[i][5], edges, instanceEdges, byClause[i]);

        }

        // The list of KVP(edges) need to have {a,b} && {b, a} for the GC problem
        // because we wont know if b is connected to a in the solver && verifier with out that in the list
        // we might need to make an adjustment to how undirected graphs are parsed out.
        // if an instance has {a,b} then the KVP list should have {a,b} && {b, a}




        // Set GRAPHCOLORING edges
        reducedGRAPHCOLORING.edges = edges;

        //Set NodeColoring
        reducedGRAPHCOLORING.nodeColoring = coloring;


        //The number of colors that satisfy the problem
        reducedGRAPHCOLORING.K = 3; //ALEX NOTE: This is a hardcoded magic number. Beware.
        reducedGRAPHCOLORING.parseProblem();

        // ------- Gadgets, and the steps that build them (one step per gadget) -------
        var sourceClauses = SAT3Instance.clauses;
        var gadgetSteps = NewGadgetSteps(rec);
        gadgets.Add(new Gadget(GadgetKind.Palette, new List<string>(), new List<string>(palette),
            note: "F, T and B form a triangle, so they take three different colors: false, true and base."));
        gadgetSteps.Accept(([], palettePiece, 0),
            $"Palette: nodes F, T and B, joined in a triangle. In any 3-coloring they take three different colors; call them false, true and base.");

        // One element per variable, and one step.
        foreach (string variable in variableNames) {
            Piece piece = byVariable[variable];
            int index = gadgets.Count;
            string variableId = GroupedGraphFrames.VariableId(variable);
            gadgets.Add(new Gadget(GadgetKind.Element,
                new List<string> { variableId }.Concat(GroupedGraphFrames.OccurrenceIds(sourceClauses, variable)).ToList(), new List<string>(piece.Nodes)));
            if (piece.Nodes.Count == 2)
                gadgetSteps.Accept(([variableId], piece, index),
                    $"Variable {variable}: nodes {piece.Nodes[0]} and {piece.Nodes[1]}, each joined to B and to each other. B takes the base color, so one of them takes the true color and the other the false color.");
            else
                gadgetSteps.Accept(([variableId], piece, index),
                    $"Variable {variable} only appears as {piece.Nodes[0]}: one node {piece.Nodes[0]}, joined to B, so it takes the true color or the false color.");
        }

        // One OR gadget per clause, and one step.
        for (int i = 0; i < clauses.Count; i++) {
            int index = gadgets.Count;
            Piece piece = byClause[i];
            string[] sourceIds = GroupedGraphFrames.ClauseIds(i, sourceClauses[i].Count);
            gadgets.Add(new Gadget(GadgetKind.OrGadget, new List<string>(sourceIds), new List<string>(piece.Nodes)));
            gadgetSteps.Accept((sourceIds, piece, index),
                $"Clause C{i + 1} {GroupedGraphFrames.ClauseText(sourceClauses[i])} becomes a 6-node OR gadget, {piece.Nodes[0]} to {piece.Nodes[5]}, wired to {GraphGroupFrames.And(PaddedClause(sourceClauses[i]).Distinct().ToList())}. Its output {piece.Nodes[5]} is joined to F and B, so it must take the true color, and that only works if one of those literals takes it too.");
        }

        int bound = gadgets.Count;
        gadgets.Add(new Gadget(GadgetKind.Bound, new List<string>(), new List<string>(), note: "K = 3, always."));
        // The closing step.
        gadgetSteps.Done(new BuildDelta { Bound = 3, Gadgets = [bound] }, true,
            $"K = 3 colors: the graph can be colored with 3 colors exactly when the formula can be satisfied.");
        // Redux_GUI reads the legacy ids: the formula's pieces in its own words ("0", "0-1"), the target's as they are.
        foreach (Gadget g in gadgets) g.reductionFromIds = GroupedGraphFrames.LegacyIds(g.sourceIds, sourceClauses);

        return reducedGRAPHCOLORING;
    }


    // 3SAT allows 1-3 literals per clause, but the OR gadget takes exactly 3 inputs. Pad a short
    // clause by repeating its first literal, the same convention KarpSATToSAT3 uses: (a) becomes
    // (a | a | a) and (a | b) becomes (a | b | a). Repeating a literal doesn't change what the
    // clause evaluates to, so the gadget stays correct.
    private static List<string> PaddedClause(List<string> clause) {
        List<string> padded = new List<string>(clause);
        while (padded.Count < 3) {
            padded.Add(clause[0]);
        }
        return padded;
    }

    // This method is adding the edges to the list of edges.
    public void addEdge(string x, string y, List<KeyValuePair<string, string>> edges, List<string> instanceEdges) {

        foreach (var elem in edges) {
            if (elem.Key.Equals(y) && elem.Value.Equals(x)) {
                // Console.WriteLine("This is this the key: "+ y + " This is the val: "+x + "\n");
                return;
            }
        }




        KeyValuePair<string, string> fullEdge = new KeyValuePair<string, string>(x, y);
        KeyValuePair<string, string> reverseEdge = new KeyValuePair<string, string>(y, x);


        //  Console.WriteLine("This is allowed edge the key: "+ x + " This is allowed the val: "+y+ "\n");
        edges.Add(fullEdge);
        edges.Add(reverseEdge);


    }

    // The Reduction View runs the backtracking solver on the target by default: the default formula's graph has 27 nodes and K = 3, past what the brute force (Graph Coloring's default solver) finishes within the view's
    // 10 s limit, and the backtracking solves it in milliseconds. /solve and the target's own default solver are unchanged.
    public ISolver? ReductionViewSolver() => new GraphColoringBacktracking();

    public string mapSolutions(string problemFromSolution) {
        //Check if the colution is correct
        if (!reductionFrom.defaultVerifier.verify(reductionFrom, problemFromSolution)) {
            throw new ReductionInputException(this, problemFromSolution, reductionFrom.certificateFormat,
                "the assignment does not satisfy the 3SAT formula, so there is no coloring to map it to");
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

        //Map solution: assign every node a color (0=False,1=True,2=Base), then group
        //the nodes by color into GraphColoringVerifier's expected "{{n1,n2},{n3},...}"
        //format (nodes sharing a color grouped into their own "{...}", split on "},{").
        // reduce() only creates a graph node for each DISTINCT LITERAL TOKEN that
        // actually occurs (SAT3Instance.literals.Distinct()) -- e.g. if "!x2" never
        // appears in the instance, there is no "!x2" node. Coloring both polarities of
        // every variable (rather than just the tokens that actually occur) would emit
        // colors for nodes that don't exist, which the verifier rejects.
        Dictionary<string, int> nodeColor = new Dictionary<string, int>();
        nodeColor["F"] = 0;
        nodeColor["T"] = 1;
        nodeColor["B"] = 2;
        foreach (string literal in reductionFrom.literals.Distinct()) {
            nodeColor[literal] = solutionList.Contains(literal) ? 1 : 0;
        }
        for (int i = 0; i < reductionFrom.clauses.Count; i++) {
            string l0, l1, l2;
            List<string> clauseLiterals = PaddedClause(reductionFrom.clauses[i]);
            l0 = clauseLiterals[0];
            l1 = clauseLiterals[1];
            l2 = clauseLiterals[2];
            int N0, N1, N2, N3, N4, N5;

            // reduce() wires this 6-node gadget as two triangles (N0-N1-N2 and N3-N4-N5)
            // joined by an N2-N3 edge, with N0--l0, N1--l1, N4--l2 (literal edges) and
            // N2--B, N5--F, N5--B (palette edges). Since N5 is adjacent to both F(0) and
            // B(2), N5 is forced to T(1) regardless of the assignment. Whichever of
            // l0/l1 is the satisfying literal drives N0/N1/N2; N3/N4 then just need to
            // land on the two colors {0,2} with N4 != color(l2).
            if (solutionList.Contains(l0)) {
                N0 = 0;
                N1 = 2;
                N2 = 1;
            } else if (solutionList.Contains(l1)) {
                N0 = 2;
                N1 = 0;
                N2 = 1;
            } else {
                // l2 must be the satisfying literal.
                N0 = 1;
                N1 = 2;
                N2 = 0;
            }
            N4 = nodeColor[l2] == 0 ? 2 : 0;
            N3 = 2 - N4;
            N5 = 1;

            nodeColor[string.Format("C{0}N0", i)] = N0;
            nodeColor[string.Format("C{0}N1", i)] = N1;
            nodeColor[string.Format("C{0}N2", i)] = N2;
            nodeColor[string.Format("C{0}N3", i)] = N3;
            nodeColor[string.Format("C{0}N4", i)] = N4;
            nodeColor[string.Format("C{0}N5", i)] = N5;
        }

        int numColors = nodeColor.Values.Max() + 1;
        List<string> colorGroups = new List<string>();
        for (int c = 0; c < numColors; c++) {
            List<string> group = nodeColor.Where(kvp => kvp.Value == c).Select(kvp => kvp.Key).ToList();
            colorGroups.Add("{" + string.Join(",", group) + "}");
        }
        return "{" + string.Join(",", colorGroups) + "}";
    }

    public Assignment EmptyAnswer() => new Assignment([]);

    // "{}" as B's answer is not a coloring of this graph (it has nodes), so it keeps the default: no answer. A's answer is an
    // assignment, never "{}".

    // How the map-back steps show a variable read: the assignment so far, and the id the step is about (the literal node read).
    private static RunSteps<string, Assignment> NewMapSteps(StepRecorder<Assignment> rec, Dictionary<string, bool> values) => new(rec,
        // What every picture shows: the assignment so far, and the piece the step is about.
        partial: _ => GroupedGraphFrames.ToAssignment(values),
        focus: node => [node]);

    // Backward map: a coloring of the graph to an assignment. The palette's T takes the true color, so a variable is True when
    // its plain literal node shares T's color, and False when it shares F's. A variable that only appears negated is read from
    // that node the other way round. Variables are read in the order the formula first mentions them. 3SAT's verifier decides
    // whether the assignment satisfies the formula.
    // Its steps: an Accept per variable whose literal node has T's or F's color (the variable takes the value it says), and a
    // Reject per variable whose literal node has no color, or neither T's nor F's (B's, or a fourth color): it says nothing,
    // and the variable is set to False. The caller adds the Done.
    public (string Answer, Assignment Partial) MapBack(string problemToSolution, StepRecorder<Assignment> rec) {
        Groups coloring = ReadColoring(problemToSolution);
        int? trueColor = coloring.Of.TryGetValue("T", out int t) ? t : null;
        int? falseColor = coloring.Of.TryGetValue("F", out int f) ? f : null;

        var values = new Dictionary<string, bool>();
        var steps = NewMapSteps(rec, values);
        var order = CnfFormula.VariablesOf(reductionFrom.clauses);
        var literalNodes = new HashSet<string>(reductionTo.nodes, StringComparer.Ordinal);
        foreach (string variable in order) {
            bool positive = literalNodes.Contains(variable);
            string node = positive ? variable : "!" + variable;
            int? color = coloring.Of.TryGetValue(node, out int c) ? c : null;
            string? says = color == null ? null : color == trueColor ? "T's" : color == falseColor ? "F's" : null;
            if (color == null) {
                values[variable] = false;
                steps.Reject(node, $"{node} has no color, so it says nothing about {variable}: {variable} = False.");
                continue;
            }
            if (says == null) {
                values[variable] = false;
                steps.Reject(node, $"{node} has neither T's nor F's color, so it says nothing about {variable}: {variable} = False.");
                continue;
            }
            bool nodeTrue = says == "T's";
            values[variable] = positive ? nodeTrue : !nodeTrue;
            steps.Accept(node, $"{node} has {says} color, so {node} is {(nodeTrue ? "true" : "false")}: {variable} = {Assignment.Word(values[variable])}.");
        }
        return (SAT.Certificate(order, values), GroupedGraphFrames.ToAssignment(values));
    }

    // A coloring as Graph Coloring writes it, {{F,x1},{T,!x1},{B}}: the n-th class is color n. Every node it names must be a
    // node of the graph; malformed text throws FormatException, which the endpoint reports as a 400.
    private Groups ReadColoring(string certificate) {
        string text = certificate.Trim();
        if (text.Length < 2 || text[0] != '{' || text[^1] != '}')
            throw new FormatException($"'{certificate}' is not a coloring in braces like {{{{F,x1}},{{T,!x1}},{{B}}}}");
        Groups coloring = GraphGroupFrames.ParseClasses(text);
        var known = new HashSet<string>(reductionTo.nodes, StringComparer.Ordinal);
        foreach (string node in coloring.Of.Keys)
            if (!known.Contains(node))
                throw new FormatException($"'{node}' is not a node of the graph");
        return coloring;
    }
}

#endregion
