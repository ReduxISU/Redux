using System;
using System.Text.Json;
using System.Collections;


class ProblemNode{

    #region Fields
    private string _reduceTo = "";
    private string _methodName = "";
    #endregion


    public ProblemNode(string reduceTo, string methodName){
        this._reduceTo = reduceTo.ToLower();
        this._methodName = methodName;
    }

    public string reduceToName {
        get {
            return _reduceTo;
        }
    }

    public string methodName {
        get {
            return _methodName;
        }
    }


}




class ProblemGraph {

   // private Dictionary<string,  List<ProblemNode>> _graph = new Dictionary<string,  List<ProblemNode>>();

    private Dictionary<string, Dictionary<string, List<string> >> _graph = 
    new Dictionary<string, Dictionary<string, List<string>>>();

    public Dictionary<string, Dictionary<string, List<string> >> graph{
        get{
            return _graph;
        }
    }


    public ProblemGraph(){
        string? [] problems = parseNpProblems();
    
        if(problems != null){

            foreach(string? problem in problems ){
                // Console.WriteLine("ProblemNode:  "+problem);
                string[] splitStr = problem.Split('_');
                this.graph.Add(splitStr[1].ToLower(), parseReducesTo(problem));
            }
        }

        foreach(KeyValuePair<string,  Dictionary<string, List<string>>> entry in this.graph){
            //Console.WriteLine("Problem name:  "+entry.Key + "\n");
            foreach(KeyValuePair<string, List<string>> elem in entry.Value){
                   foreach(string method in elem.Value){
        //    Console.WriteLine("Problem name:  "+ entry.Key +" || Reduce to: "+ elem.Key+" || Reduce method: "+ method+ "\n \n");
                      

                  }

            
            }
        }
       }  


    public Dictionary<string, List<string>> getConnectedNodes(string problemName){
        Dictionary<string, List<string>> edges = new Dictionary<string, List<string>>();
        Stack<string> stack = new Stack<string>();
        HashSet<string> visited = new HashSet<string>();
        stack.Push(problemName.ToLower());
        while(stack.Count > 0) {
            string currentNode = stack.Pop();

            Dictionary<string, List<string>> nodes = this.graph[currentNode];

            // Console.WriteLine(currentNode);
            // var options = new JsonSerializerOptions { WriteIndented = true };
            // Console.WriteLine(JsonSerializer.Serialize(nodes,options));
            // Console.WriteLine("-------------------------------");
               // add node to visited
            if(!visited.Contains(currentNode)){ 
                visited.Add(currentNode);
            }
            foreach(KeyValuePair<string, List<string>> elem in nodes){
                // for(int i=0; i<elem.Value.Count; i++){
                //     Console.WriteLine("     "+elem.Value[i]);
                // }
                if(edges.ContainsKey(elem.Key)){
                    foreach(string method in elem.Value){
                        if(!currentNode.Equals(problemName)){
                            edges[elem.Key].Add("*"+method); 

                        }else{
                            edges[elem.Key].Add(method);
                        }
                    }
                } else {
                    if(currentNode.Equals(problemName.ToLower())){
                        edges.Add(elem.Key, elem.Value);
                              
                    }else{
                        List<string> temp = new List<string>();
                        foreach(string method in elem.Value){
                            temp.Add("*"+method);
                        }
                          edges.Add(elem.Key, temp);
                    }   
                }
                 if(!visited.Contains(elem.Key)){
                    stack.Push(elem.Key);
                }
            }
        }
        //  Console.WriteLine(problemName+ "Reductions \n");
        // foreach(KeyValuePair<string, List<string>> elem in edges){
        //     foreach(string method in elem.Value){
        //         Console.WriteLine("Problem name:  "+problemName +" || Reduce to: "+ elem.Key+" || Reduce method: "+ method+ "\n");       
        // }}
        return edges;
    }

    public List<string> getConnectedProblems(string problemName){
        List<string> connectedNodes = new List<string>();
        Stack<string> stack = new Stack<string>();
        HashSet<string> visited = new HashSet<string>{};
        stack.Push(problemName);
        while(stack.Count > 0) {
            string currentNode = stack.Pop();

            Dictionary<string, List<string>> adjacentNodes = this.graph[currentNode];

            // Console.WriteLine(currentNode);
            // var options = new JsonSerializerOptions { WriteIndented = true };
            // Console.WriteLine(JsonSerializer.Serialize(nodes,options));
            // Console.WriteLine("-------------------------------");
               // add node to visited
            if(!visited.Contains(currentNode)){ 
                visited.Add(currentNode);
            }
            foreach(KeyValuePair<string, List<string>> node in adjacentNodes){
                if(!connectedNodes.Contains(node.Key) && node.Key != problemName){
                    // connectedNodes.Add("*"+node.Key); //transitive
                    connectedNodes.Add(node.Key);
                }

                if(!visited.Contains(node.Key)){
                    stack.Push(node.Key);
                }
            }
        }
        for(int i=0; i<connectedNodes.Count; i++){
            connectedNodes[i] = connectedNodes[i].ToUpper();
        }
        return connectedNodes;
    }

    public List<List<string>> getReductionPath(string startProblem, string endProblem){
        if(endProblem.Contains("*")){
            endProblem = endProblem.Replace("*","");
        }
        List<List<string>> path = new List<List<string>>();
        Dictionary<string,string> links = new Dictionary<string,string>();
        Queue<string> Q = new Queue<string>();
        Q.Enqueue(startProblem);
        while(Q.Count != 0){
            string u = Q.Dequeue();
            Dictionary<string, List<string>> adjacentNodes = this.graph[u];
            foreach(KeyValuePair<string, List<string>> node in adjacentNodes){
                if(!links.ContainsKey(node.Key) && node.Key != startProblem && startProblem != node.Key){
                    links.Add(node.Key,u);
                    Q.Enqueue(node.Key);
                }
                if(node.Key == endProblem) {Q.Clear();}
            }
        }
        string currentProblem = endProblem;
        while(links.ContainsKey(currentProblem)){
            List<string> templist = this.graph[links[currentProblem]][currentProblem];
            for(int i=0; i<templist.Count; i++){
                templist[i] = templist[i].Replace(".cs","");
            }
            path.Add(templist);
            currentProblem = links[currentProblem];
        }
        path.Reverse();
        // var options = new JsonSerializerOptions { WriteIndented = true };
        // Console.WriteLine(JsonSerializer.Serialize(path,options));
        // Console.WriteLine("-------------------------------");
        return path;
    }

    


    // public List<KeyValuePair<string, string>> getConnectedNodes(string problemName){
    //     List<KeyValuePair<string, string>> edges = new List<KeyValuePair<string, string>>();

    //     Stack<string> stack = new Stack<string>();
    //     HashSet<string> visited = new HashSet<string>();
    //     stack.Push(problemName);

    //    while(stack.Count > 0){
    //      string currentNode =  stack.Pop();
    //      List<ProblemNode> nodes =  this.graph[currentNode.ToLower()];


    //     // add node to visited
    //     if(!visited.Contains(currentNode)){ 
    //         visited.Add(currentNode);

    //     }
       

    //     foreach(ProblemNode nodeTo in nodes){

    //         // Transverse problem 
    //         if(!currentNode.Equals(problemName)){
    //             edges.Add(new KeyValuePair<string,string>(nodeTo.reduceToName, "*"+nodeTo.methodName));

    //         }else{
    //             edges.Add(new KeyValuePair<string,string>(nodeTo.reduceToName, nodeTo.methodName));
    //         }

    //         // 
    //         if(!visited.Contains(nodeTo.reduceToName)){
    //             stack.Push(nodeTo.reduceToName);
    //         }
    //     }

    //    }
    //    foreach(KeyValuePair<string, string> entry in edges){
    //    Console.WriteLine("Problem: :  "+problemName+" || ReduceTo name:  "+entry.Key + " || Reduce Method: "+ entry.Value + "\n");
    //    }

    //    return edges;
    // }


    public string? [] parseNpProblems(){
        string projectSourcePath = ProjectSourcePath.Value;
        return Directory.GetDirectories(projectSourcePath+ @"Problems/NPComplete")
                            .Select(Path.GetFileName)
                            .ToArray();

    }


    public Dictionary<string, List<string>> parseReducesTo(string chosenProblem){
        string projectSourcePath = ProjectSourcePath.Value;
        string problemTypeDirectory = "";
        string problemType = chosenProblem.Split('_')[0];
        List<ProblemNode> problemsReducedTo = new List<ProblemNode>();
        Dictionary<string, List<string>> dict = new Dictionary<string, List<string>>();
        string?[] subdirs =  {};
        ArrayList subdirsNoPrefix = new ArrayList();
 
        if (problemType == "NPC") {
            problemTypeDirectory = "NPComplete";
        }

        try{

                 subdirs = Directory.GetDirectories(projectSourcePath+ @"Problems/" + problemTypeDirectory + "/" + chosenProblem + "/ReduceTo")
                            .Select(Path.GetFileName)
                            .ToArray();

        

            foreach(string problemDirName in subdirs) {

                string[] splitStr = problemDirName.Split('_');
                string newName = splitStr[1];
                subdirsNoPrefix.Add(newName);
           }
           

        }  catch (System.IO.DirectoryNotFoundException dirNotFoundException){
           // Console.WriteLine( " directory not found, exception was thrown in Nav_Reductions.cs");
            // Console.WriteLine(dirNotFoundException.StackTrace);

        } finally{

        }

        for(int i = 0; i < subdirs.Length; i++){
            string problemName = subdirsNoPrefix[i].ToString();

                string?[] subfiles = Directory.GetFiles(projectSourcePath+ @"Problems/" + problemTypeDirectory + "/" + chosenProblem + "/ReduceTo/" + subdirs[i])
                            .Select(Path.GetFileName)
                            .ToArray();

                // string reductionMethods = "";
                // foreach(string? method in subfiles){
                //     reductionMethods += method+", "; 
                // }
                dict.Add(problemName.ToLower(), subfiles.ToList());

                // reductionMethods = reductionMethods.TrimEnd(',').Trim();
                // problemsReducedTo.Add(new ProblemNode(subdirsNoPrefix[i].ToString(), reductionMethods));

        }
           

        return dict;

}
}





