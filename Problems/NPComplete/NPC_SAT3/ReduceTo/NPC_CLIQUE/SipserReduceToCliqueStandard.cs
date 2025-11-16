using API.Interfaces;
using API.Problems.NPComplete.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_CLIQUE.Inherited;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using SPADE;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_CLIQUE;

class SipserReduceToCliqueStandard : IReduction<SAT3, CLIQUE>
{

    // --- Fields ---
    public string reductionName {get;} = "Sipser's Clique Reduction";
    public string reductionDefinition {get;} = "Sipsers reduction converts clauses from 3SAT into clusters of nodes in a graph for which CLIQUES exist";
    public string source {get;} = "Sipser, Michael. Introduction to the Theory of Computation.ACM Sigact News 27.1 (1996): 27-29.";
    public string[] contributors {get;} = { "Kaden Marchetti", "Alex Diviney", "Caleb Eardley", "Russell Phillips"};
    private Dictionary<Object,Object> _gadgetMap = new Dictionary<Object,Object>();
    private SAT3 _reductionFrom;
    private CLIQUE _reductionTo;


    // --- Properties ---
    public List<Gadget> gadgets { get; set; }
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
    public CLIQUE reductionTo
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
    public SipserReduceToCliqueStandard(SAT3 from)
    {
        gadgets = new();
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public SipserReduceToCliqueStandard(string instance) : this(new SAT3(instance)) { }
    public SipserReduceToCliqueStandard() : this(new SAT3()) { }

    private bool fromSameClause(UtilCollection node1, UtilCollection node2)
    {
        List<string> node1List = node1.ToString().Split("_").ToList();
        List<string> node2List = node2.ToString().Split("_").ToList();
        return node1List[node1List.Count - 1] == node2List[node2List.Count - 1]; // node names may contain underscores, but clause number will always be the very last
    }
    private string removeClauseNumber(string s)
    {

        int underscore = s.LastIndexOf('_');
        if (underscore == -1)
            return s;

        return s.Substring(0, underscore);
    }
    private bool isSameLiteral(UtilCollection node1, UtilCollection node2)
    {
        string coreA = removeClauseNumber(node1.ToString());
        if (coreA.StartsWith("!"))
            coreA = coreA.Substring(1);
        string coreB = removeClauseNumber(node2.ToString());
        if (coreB.StartsWith("!"))
            coreB = coreB.Substring(1);
        return coreA == coreB;
    }

    private bool isInverse(UtilCollection node1, UtilCollection node2)
    {
        if (!isSameLiteral(node1, node2)) return false;
        return node1.ToString()[0] == '!' ^ node2.ToString()[0] == '!';
    }

    public CLIQUE reduce()
    {
        UtilCollection nodes = new("{}");
        UtilCollection edges = new("{}");
        for (int i = 0; i < reductionFrom.clauses.Count; i++)
        {
            List<string> nodesInClause = new();
            for (int j = 0; j < reductionFrom.clauses[i].Count; j++)
            {
                string literal = reductionFrom.clauses[i][j];
                string nodeName = literal + "_" + i;
                nodes.Add(new UtilCollection(nodeName));

                gadgets.Add(new Gadget("ElementHighlight", new List<string>() {i + "-" + j }, new List<string> { nodeName }));
                nodesInClause.Add(nodeName);
            }
            gadgets.Add(new Gadget("ClauseHighlight", new List<string>() {i.ToString() }, nodesInClause));
        }

        foreach (UtilCollection node1 in nodes)
            foreach (UtilCollection node2 in nodes)
            {
                if (node1.Equals(node2)) continue;
                if (fromSameClause(node1, node2)) continue; //no edges between nodes of the same clause
                if (isInverse(node1, node2)) continue; //no edges between literals that are always opposite of eachother

                UtilCollection edge = new("{}");
                edge.Add(node1);
                edge.Add(node2);
                edges.Add(edge);
            }
        reductionTo = new CLIQUE($"(({nodes},{edges}),{reductionFrom.clauses.Count})");
        
        return reductionTo;

    }
    public SipserClique reduce2()
    {
        SAT3 SAT3Instance = _reductionFrom;

        _gadgetMap = new Dictionary<object, object>();

        //number literals of sat before reduction
        List<List<String>> newClauses = new List<List<string>>();
        foreach(var clause in SAT3Instance.clauses){
            List<String> temp = new List<String>();
            foreach(var element in clause){
                temp.Add(element);
            }
            newClauses.Add(temp);
        }
        for (int i = 0; i < SAT3Instance.clauses.Count; i++){
            for (int j = 0; j < SAT3Instance.clauses[i].Count; j++){
                int count = 0;
                for( int k = 0; k<i; k++){
                    foreach(var element in SAT3Instance.clauses[k]){
                        if(element == SAT3Instance.clauses[i][j]){
                            count ++;
                        }
                    }
                }
                for( int k = 0; k<j; k++){
                    if(SAT3Instance.clauses[i][j] == SAT3Instance.clauses[i][k]){
                        count ++;
                    }
                }
                if(count >0){
                    newClauses[i][j] = SAT3Instance.clauses[i][j] + "_" +count;
                }
                else{
                    newClauses[i][j] = SAT3Instance.clauses[i][j];
                }
            }
        }
        SipserClique reducedCLIQUE = new SipserClique();
        // SAT3 literals become nodes.
        reducedCLIQUE.nodes = SAT3Instance.literals;

      
       

        List<KeyValuePair<string, string>> edges = new List<KeyValuePair<string, string>>();
        List<string> usedNames = new List<string>(); // Used to track what names have been used for nodes

        // define what makes the edges. Not in same cluster & not inverse

        // I is the cluster
        for (int i = 0; i < newClauses.Count; i++)
        {
            reducedCLIQUE.numberOfClusters = newClauses.Count;
            for (int j = 0; j < newClauses[i].Count; j++)
            {
                string nodeFrom = newClauses[i][j];
                // nodeFrom = duplicateName(nodeFrom, usedNames, 1, nodeFrom);

                SipserNode newNode = new SipserNode(nodeFrom, i.ToString());
                reducedCLIQUE.clusterNodes.Add(newNode);
                // usedNames.Add(nodeFrom);
                //Four loops? Sounds efficent
                for (int a = 0; a < newClauses.Count; a++)
                {

                    for (int b = 0; b < newClauses[a].Count; b++)
                    {
                        
                        string nodeTo = newClauses[a][b];
                        bool inverse = false;
                        bool samecluser = false;

                        // Check if nodes are inverse of one another

                        if (removeIndex(nodeFrom) != removeIndex(nodeTo) && removeIndex(nodeFrom.Replace("!", "")) == removeIndex(nodeTo.Replace("!", "")))
                        {
                            inverse = true;
                        }
                        // Check if nodes belong to same cluster
                        if (i == a)
                        {
                            samecluser = true;
                        }

                        KeyValuePair<string, string> fullEdge = new KeyValuePair<string, string>(nodeFrom, nodeTo);

                        if (!inverse && !samecluser && nodeFrom != nodeTo)
                        {
                            if(i == 0 && a ==1 && j == 0 && b == 1){
                                foreach(var name in usedNames){
                                }
                            }
                            edges.Add(fullEdge);
                        }
                    }
                }
            }
        }
        reducedCLIQUE.edges = edges;
        reducedCLIQUE.K = SAT3Instance.clauses.Count;

        // --- Generate G string for new CLIQUE ---
        string nodesString = "";
        string literalName = String.Empty;
        List<string> usedNamesLiterals = new List<string>();
        foreach (string literal in SAT3Instance.literals)
        {
            literalName = duplicateName(literal, usedNamesLiterals, 1, literal);
            nodesString += literalName + ",";
            usedNamesLiterals.Add(literalName);
        }
        nodesString = nodesString.TrimEnd(',');

        string edgesString = "";
        foreach (KeyValuePair<string, string> edge in edges)
        {
            edgesString += "{" + edge.Key + "," + edge.Value + "}" + ",";
        }
        edgesString = edgesString.Trim(' ').TrimEnd(',');

        int kint = SAT3Instance.clauses.Count;
        // "{{1,2,3,4} : {(4,1) & (1,2) & (4,3) & (3,2) & (2,4)} : 1}";
        string G = "(({" + nodesString + "},{" + edgesString + "})," + kint.ToString() + ")";

        // Assign and return
        //Console.WriteLine(G);
        var options = new JsonSerializerOptions { WriteIndented = false };
        //Update gadget mapping to set literals as keys and nodes as values.


        List<string> satGadgetList = new List<string>();
        List<string> cliqueGadgetList = new List<string>();
        int id = 0;
        foreach(string l in SAT3Instance.literals ){
            id++;
            SAT3Gadget sGadget = new SAT3Gadget("SipserReduceToCliqueStandard",l,id);
            // string[] sGadget = new string[] {l};
            string serializedGadget = JsonSerializer.Serialize(sGadget, options);
            satGadgetList.Add(serializedGadget);
        }
        id = 0;
        foreach(string l in usedNamesLiterals ){
            id++;
            CLIQUEGadget cGadget = new CLIQUEGadget("SipserReduceToCliqueStandard",l,id);
            // string[] cGadget = new string[] { l };
            string serializedGadget = JsonSerializer.Serialize(cGadget, options);
            cliqueGadgetList.Add(serializedGadget);
        }
    
        for (int i = 0; i < satGadgetList.Count;i++){
            _gadgetMap.Add(satGadgetList[i], cliqueGadgetList[i]);
        }

        CLIQUE clique = new CLIQUE(G);
        reducedCLIQUE.graph = clique.graph; 
        reducedCLIQUE.instance = G;
        reductionTo = reducedCLIQUE;
        return reducedCLIQUE;
    }

    private string duplicateName(string name, List<string> usedNames, int version, string originalName)
    {
        if (usedNames.Contains(name))
        {
            // usedNames.Add(name);
            string newName = originalName + '_' + version;
            version = version + 1;
            return duplicateName(newName, usedNames, version, originalName);
        }

        return name;
    }

    /// <summary>
    ///  Given a solution string and a reduced to problem instance, map the solution to the problem. 
    /// </summary>
    /// <param name="sipserInput"></param>
    /// <param name="solutionDict"></param>
    /// <returns> A Sipser Clique with a cluster nodes attribute (list of SipserNodes) that has a solution state mapped to each node.</returns>
    public SipserClique solutionMappedToClusterNodes(SipserClique sipserInput, List<string> solution)
    {

        foreach (var s in sipserInput.clusterNodes){
            if(solution.Contains(s.name)){
                s.solutionState = true.ToString();
            }
        }

        return sipserInput;

    }

    /// <summary>
    ///  This maps a name prefix, ie. x1, to the possible clusters that it could appear in, ie. [x1_1, x1_2] and returns that list
    /// </summary>
    /// <param name="primaryName"></param>
    /// <param name="amountOfClusters"></param>
    /// <returns> A list of possible names</returns>
    private List<string> getclusterNodeSearchList(string primaryName, int amountOfClusters)
    {
        List<string> searchList = new List<string>();
        searchList.Add(primaryName);
        for (int i = 1; i < amountOfClusters; i++)
        {
            searchList.Add(primaryName + "_" + i);
            //Console.WriteLine(primaryName + "_" + i);
        }
        return searchList;

    }
    private string removeIndex(string node){
        if(node.Contains("_")){
            return node.Split("_")[0];
        }
        return node;
    }

    private bool alreadyContainsNodeFromClause(string node, List<string> potentialNodes)
    {
        foreach (string selNode in potentialNodes)
        {
            List<string> selNodeSplit = selNode.Split("_").ToList();
            List<string> nodeSplit = node.Split("_").ToList();
            if (selNodeSplit[selNodeSplit.Count - 1] == nodeSplit[nodeSplit.Count - 1])
            {
                return true;
            }
        }
        return false;
    }
    public string mapSolutions(string solution)
    {
        List<string> items = solution.TrimStart('(').TrimEnd(')').Split(",").ToList();
        HashSet<string> trueLiterals = new();
        foreach (string item in items)
        {
            List<string> split = item.Split(":").ToList();
            if (split[1] == "True")
                trueLiterals.Add(split[0]);
            else
                trueLiterals.Add("!" + split[0]);
        }

        List<string> potentialNodes = new();
        foreach (string node in reductionTo.nodes)
        {
            if (trueLiterals.Contains(removeClauseNumber(node)))
            {
                if (alreadyContainsNodeFromClause(node, potentialNodes))
                    continue;
                potentialNodes.Add(node);
            }
        }

    string mappedSol = "{";
        foreach (string node in potentialNodes)
            mappedSol += node + ",";
        return mappedSol.TrimEnd(',') + "}";
    }

    public string reverseMapSolutions(SAT3 problemFrom, SipserClique problemTo, string problemToSolution)
    {
        if (!problemTo.defaultVerifier.verify(problemTo, problemToSolution))
        {
            return "Clique Solution is incorect";
        }

        //Parse problemFromSolution into a list of nodes
        List<string> solutionList = GraphParser.parseNodeListWithStringFunctions(problemToSolution);


        //Reverse Mapping
        List<string> reverseMappedSolutionList = new List<string>();
        foreach (string node in solutionList)
        {
            string temp = node;
            if (temp.Contains("_")) { temp = temp.Substring(0, temp.IndexOf("_")); }
            if (temp.Contains("!"))
            {
                temp = temp.Replace("!", "") + ":False";
            }
            else
            {
                temp = temp + ":True";
            }
            if (!reverseMappedSolutionList.Contains(temp)) { reverseMappedSolutionList.Add(temp); }

        }

        string problemFromSolution = "";
        foreach (string literal in reverseMappedSolutionList)
        {
            problemFromSolution += literal + ',';
        }
        return '(' + problemFromSolution.TrimEnd(',') + ')';
    }

}