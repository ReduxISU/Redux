using API.Interfaces;
using API.Problems.NPComplete.NPC_GRAPHCOLORING;


namespace API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_GRAPHCOLORING;

class KarpReduction : IReduction<SAT3, GRAPHCOLORING>
{


    #region Fields
    private string _reductionName = "Karps's Graph Coloring Reduction";
    private string _reductionDefinition = "Karp's reduction converts each clause from a 3CNF into an OR gadgets to establish the truth assignments using labels.";
    private string _source = "http://cs.bme.hu/thalg/3sat-to-3col.pdf.";
    private string[] _contributors = { "Daniel Igbokwe"};
    private Dictionary<Object,Object> _gadgetMap = new Dictionary<Object,Object>();

    private SAT3 _reductionFrom;
    private GRAPHCOLORING _reductionTo;
    private string _complexity = "O(n^2)";

    #endregion

    #region Properties
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
    public string[] contributors{
        get{
            return _contributors;
        }
    }

    public string complexity
    {
        get
        {
            return _complexity;
        }

        set
        {
            _complexity = value;
        }
    }
    public Dictionary<Object,Object> gadgetMap {
        get{
            return _gadgetMap;
        }
        set{
            _gadgetMap = value;
        }
    }
    public SAT3 reductionFrom
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


    public GRAPHCOLORING reductionTo
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

    #endregion

    #region Constructors
    public KarpReduction(SAT3 from)
    {
        _reductionFrom = from;
        _reductionTo = reduce();

    }


    # endregion

    # region Methods


    //The below code is reducing the SAT3 instance to a GRAPHCOLORING instance.
    public GRAPHCOLORING reduce()
    {

        // color palette 
        // 0 : False, 1 : True,  2 : Base

        string[] palette = { "F", "T", "B" };

        SAT3 SAT3Instance = _reductionFrom;
        GRAPHCOLORING reducedGRAPHCOLORING = new GRAPHCOLORING();
        Dictionary<string, string> coloring = new Dictionary<string, string>();


        // ------- Add nodes -------

        List<string> nodes = new List<string>(palette);

        for (int i = 0; i < palette.Length; i++)
        {
            coloring.Add(palette[i], i.ToString());
        }

        List<string> variables = SAT3Instance.literals.Distinct().ToList();

        for (int i = 0; i < variables.Count; i++)
        {
            nodes.Add(variables[i]);
            coloring.Add(variables[i], "-1");
        }

        // Create clause nodes 
        List<List<string>> clauses = new List<List<string>>();
        for (int i = 0; i < SAT3Instance.clauses.Count; i++)
        {

            List<string> tempClause = new List<string>();

            for (int j = 0; j < 6; j++)
            {
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
        for (int i = 0; i < palette.Length; i++)
        {
            for (int j = 0; j < palette.Length; j++)
            {
                if (i != j)
                {
                    addEdge(palette[i], palette[j], edges, instanceEdges);
                }
            }
        }


        // Connect variable edges to palette color blue 
        // Can only be colored True or false can't be base 
        for (int i = 0; i < variables.Count; i++)
        {
            addEdge(variables[i], palette[2], edges, instanceEdges);

        }

        // Connect literal to literal negation
        // x1 and !x1 can't have the same color
        for (int i = 0; i < variables.Count; i++)
        {
            for (int j = 0; j < variables.Count; j++)
            {
                if (variables[i].Replace("!", "") == variables[j].Replace("!", "") && variables[i] != variables[j])
                {
                    addEdge(variables[i], variables[j], edges, instanceEdges);
                }
            }
        }

        // Create clause gadget
        // Each clause contains 6 nodes 
        for (int i = 0; i < clauses.Count; i++)
        {
            // Connect   (a V b ) 
            for (int j = 0; j < 3; j++)
            {
                for (int k = 0; k < 3; k++)
                {
                    if (k != j) { addEdge(clauses[i][j], clauses[i][k], edges, instanceEdges); }
                }

            }

            // Connect  ((a V b) V c )
            for (int j = 3; j < clauses[i].Count; j++)
            {
                for (int k = 3; k < clauses[i].Count; k++)
                {
                    if (k != j) { addEdge(clauses[i][j], clauses[i][k], edges, instanceEdges); }
                }
            }


            // Join ((a V b) V c )
            for (int j = 2; j < 4; j++)
            {

                for (int k = 2; k < 4; k++)
                {

                    if (k != j) { addEdge(clauses[i][j], clauses[i][k], edges, instanceEdges); }
                }
            }

        }


        // Combine clause, variable and palette  

        for (int i = 0; i < clauses.Count; i++)
        {


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
    public void addEdge(string x, string y, List<KeyValuePair<string, string>> edges, List<string> instanceEdges)
    {

        foreach(var elem in edges){
            if(elem.Key.Equals(y) && elem.Value.Equals(x)){
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

    public string mapSolutions(SAT3 problemFrom, GRAPHCOLORING problemTo, string problemFromSolution){
        //Check if the colution is correct
        if(!problemFrom.defaultVerifier.verify(problemFrom,problemFromSolution)){
            return "Solution is inccorect";
        }

        //Parse problemFromSolution into a list of nodes
        List<string> solutionList = problemFromSolution.Replace(" ","").Replace("(","").Replace(")","").Split(",").ToList();
        for(int i=0; i<solutionList.Count; i++){
            string[] tempSplit = solutionList[i].Split(":");
            if(tempSplit[1] == "False"){
                solutionList[i] = "!"+tempSplit[0];
            }
            else if(tempSplit[1] == "True"){
                solutionList[i] = tempSplit[0];
            }
            else{solutionList[i] = "";}
            
        }
        solutionList.RemoveAll(x => string.IsNullOrEmpty(x));

        //Map solution
        List<string> mappedSolutionList = new List<string>();
        List<string> variables = new List<string>();
        foreach(string literal in problemFrom.literals){
            if(!variables.Contains(literal.Replace("!",""))){
                variables.Add(literal.Replace("!",""));
            }
        }
        mappedSolutionList.Add("F:0");
        mappedSolutionList.Add("T:1");
        mappedSolutionList.Add("B:2");
        foreach(string variable in variables){
            if(solutionList.Contains(variable)){
                mappedSolutionList.Add(string.Format("{0}:1",variable));
                mappedSolutionList.Add(string.Format("!{0}:0",variable));
            }
            else{
                mappedSolutionList.Add(string.Format("{0}:0",variable));
                mappedSolutionList.Add(string.Format("!{0}:1",variable));
            }
        }
        for(int i=0; i<problemFrom.clauses.Count; i++){
            string l0,l1,l2;
            l0 = problemFrom.clauses[i][0];
            l1 = problemFrom.clauses[i][1];
            l2 = problemFrom.clauses[i][2];
            int N0, N1, N2, N3, N4, N5;

            if(solutionList.Contains(l0) || solutionList.Contains(l1)){
                if(solutionList.Contains(l0)){
                    N0 = 0;
                    N1 = 2;
                }
                else{
                    N0 = 2;
                    N1 = 0;
                }
                N2 = 1;
                N3 = 0;
                N4 = 2;
            }else{
                
                N2 = 0;
            }
            N0=1;
            N1=1;
            N2=1;
            N3=1;
            N4=1;
            N5=1;

            mappedSolutionList.Add(string.Format("C{0}N0:{1}",i,N0));
            mappedSolutionList.Add(string.Format("C{0}N1:{1}",i,N1));
            mappedSolutionList.Add(string.Format("C{0}N2:{1}",i,N2));
            mappedSolutionList.Add(string.Format("C{0}N3:{1}",i,N3));
            mappedSolutionList.Add(string.Format("C{0}N4:{1}",i,N4));
            mappedSolutionList.Add(string.Format("C{0}N5:{1}",i,N5));
        }


        string problemToSolution = "";
        foreach(string node in mappedSolutionList){
            problemToSolution += node + ',';
        }
        return "{(" + problemToSolution.TrimEnd(',') + "):3}";
    }
}

    #endregion




