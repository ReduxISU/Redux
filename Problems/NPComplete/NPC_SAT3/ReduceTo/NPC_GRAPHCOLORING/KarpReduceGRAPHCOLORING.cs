using API.Interfaces;
using API.Problems.NPComplete.NPC_GRAPHCOLORING;
using API.Problems.NPComplete.NPC_SAT;

namespace API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_GRAPHCOLORING;

class KarpReduceGRAPHCOLORING : IReduction<SAT3, GRAPHCOLORING> {


    #region Fields
    public string reductionName { get; } = "Karps's Graph Coloring Reduction";
    public string reductionDefinition { get; } = "Karp's reduction converts each clause from a 3CNF into an OR gadgets to establish the truth assignments using labels.";
    public string source { get; } = "http://cs.bme.hu/thalg/3sat-to-3col.pdf.";
    public string[] contributors { get; } = { "Daniel Igbokwe" };
    // The literal-negation loop iterates O(v^2) times but each distinct literal has at
    // most one negation partner, so real edges added there are O(v); clause-gadget
    // edges are O(1) per clause. Total real output edges are O(v + c), despite the
    // O(v^2)-iteration scan — not the same claim as the pre-existing `complexity` field
    // below (unrelated, unverified runtime-complexity note).
    public ReductionCost cost { get; } = ReductionCost.Linear;
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
    public KarpReduceGRAPHCOLORING(SAT3 from) {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public KarpReduceGRAPHCOLORING(string instance) : this(new SAT3(instance)) { }
    public KarpReduceGRAPHCOLORING() : this(new SAT3()) { }

    #endregion

    #region Methods


    //The below code is reducing the SAT3 instance to a GRAPHCOLORING instance.
    public GRAPHCOLORING reduce() {

        // color palette 
        // 0 : False, 1 : True,  2 : Base

        string[] palette = { "F", "T", "B" };

        SAT3 SAT3Instance = _reductionFrom;
        GRAPHCOLORING reducedGRAPHCOLORING = new GRAPHCOLORING();
        Dictionary<string, string> coloring = new Dictionary<string, string>();


        // ------- Add nodes -------

        List<string> nodes = new List<string>(palette);

        for (int i = 0; i < palette.Length; i++) {
            coloring.Add(palette[i], i.ToString());
        }

        List<string> variables = SAT3Instance.literals.Distinct().ToList();

        for (int i = 0; i < variables.Count; i++) {
            nodes.Add(variables[i]);
            coloring.Add(variables[i], "-1");
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
                    addEdge(palette[i], palette[j], edges, instanceEdges);
                }
            }
        }


        // Connect variable edges to palette color blue 
        // Can only be colored True or false can't be base 
        for (int i = 0; i < variables.Count; i++) {
            addEdge(variables[i], palette[2], edges, instanceEdges);

        }

        // Connect literal to literal negation
        // x1 and !x1 can't have the same color
        for (int i = 0; i < variables.Count; i++) {
            for (int j = 0; j < variables.Count; j++) {
                if (variables[i].Replace("!", "") == variables[j].Replace("!", "") && variables[i] != variables[j]) {
                    addEdge(variables[i], variables[j], edges, instanceEdges);
                }
            }
        }

        // Create clause gadget
        // Each clause contains 6 nodes 
        for (int i = 0; i < clauses.Count; i++) {
            // Connect   (a V b ) 
            for (int j = 0; j < 3; j++) {
                for (int k = 0; k < 3; k++) {
                    if (k != j) { addEdge(clauses[i][j], clauses[i][k], edges, instanceEdges); }
                }

            }

            // Connect  ((a V b) V c )
            for (int j = 3; j < clauses[i].Count; j++) {
                for (int k = 3; k < clauses[i].Count; k++) {
                    if (k != j) { addEdge(clauses[i][j], clauses[i][k], edges, instanceEdges); }
                }
            }


            // Join ((a V b) V c )
            for (int j = 2; j < 4; j++) {

                for (int k = 2; k < 4; k++) {

                    if (k != j) { addEdge(clauses[i][j], clauses[i][k], edges, instanceEdges); }
                }
            }

        }


        // Combine clause, variable and palette  

        for (int i = 0; i < clauses.Count; i++) {


            // Connect variables to clause gadgets 
            addEdge(SAT3Instance.clauses[i][0], clauses[i][0], edges, instanceEdges);
            addEdge(SAT3Instance.clauses[i][1], clauses[i][1], edges, instanceEdges);
            addEdge(SAT3Instance.clauses[i][2], clauses[i][4], edges, instanceEdges);



            addEdge(clauses[i][0], SAT3Instance.clauses[i][0], edges, instanceEdges);
            addEdge(clauses[i][1], SAT3Instance.clauses[i][1], edges, instanceEdges);
            addEdge(clauses[i][4], SAT3Instance.clauses[i][2], edges, instanceEdges);


            // Connect palette base node to (a V b)
            addEdge(clauses[i][2], palette[2], edges, instanceEdges);
            addEdge(palette[2], clauses[i][2], edges, instanceEdges);

            // Connect palette nodes to clauses ((a V b) V c )

            // palette : False  
            addEdge(clauses[i][5], palette[0], edges, instanceEdges);
            addEdge(palette[0], clauses[i][5], edges, instanceEdges);


            //  palette : Base  
            addEdge(clauses[i][5], palette[2], edges, instanceEdges);
            addEdge(palette[2], clauses[i][5], edges, instanceEdges);

        }



        // for (int i = 0; i < edges.Count; i++){
        //     for (int j = 0; j < edges.Count; j++){
        //         if (edges[i].Key == edges[j].Value && edges[i].Value == edges[j].Key){
        //             edges.Remove(new KeyValuePair<string,string>(edges[j].Key, edges[j].Value));
        //         }
        //     }
        // }

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

        return reducedGRAPHCOLORING;
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

    public string mapSolutions(string problemFromSolution) {
        //Check if the colution is correct
        if (!reductionFrom.defaultVerifier.verify(reductionFrom, problemFromSolution)) {
            return "Solution is inccorect";
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
            l0 = reductionFrom.clauses[i][0];
            l1 = reductionFrom.clauses[i][1];
            l2 = reductionFrom.clauses[i][2];
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
}

#endregion


