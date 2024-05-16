using API.Interfaces;
using System.Diagnostics;

namespace API.Problems.NPComplete.NPC_ExactCover.Solvers;
class DancingLinks : ISolver {

    // --- Fields ---
    private string _solverName = "Dancing Links";
    private string _solverDefinition = "";
    private string _source = "";
    private string[] _contributors = { "Andrija Sevaljevic"};


    // --- Properties ---
    public string solverName {
        get {
            return _solverName;
        }
    }
    public string solverDefinition {
        get {
            return _solverDefinition;
        }
    }
    public string source {
        get {
            return _source;
        }
    }
    public string[] contributors{
        get{
            return _contributors;
        }
    }
    // --- Methods Including Constructors ---
    public DancingLinks() {
        
    }
       
    public string solve(ExactCover exactCover) {

        Dictionary<int,List<int>> Y = new Dictionary<int, List<int>>();
        Dictionary<int,List<int>> X = new Dictionary<int, List<int>>();
        Dictionary<string,int> names = new Dictionary<string,int>();

        for(int i = 0; i < exactCover.S.Count; i++) {
            Y.Add(i, new List<int>());
        }

        for(int i = 0; i < exactCover.X.Count; i++) {
            names.Add(exactCover.X[i],i);
            X.Add(i, new List<int>());
        }

        for(int i = 0; i < exactCover.S.Count; i++) {
            foreach(var j in exactCover.S[i]) {
                X[names[j]].Add(i);
                Y[i].Add(names[j]);
            }
        }

        Stack<int> selectedSets = new Stack<int>();
        bool foundSolution = false;
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        iterate(Y,ref X,ref selectedSets, ref foundSolution);

        stopwatch.Stop();

        Console.WriteLine(stopwatch.ElapsedMilliseconds);
        Console.WriteLine(exactCover.S.Count());

        if(selectedSets.Any()) {
            return solutionToCertificate(selectedSets, exactCover);
        }

        return "{}";
    }

    private void iterate(Dictionary<int,List<int>> Y, ref Dictionary<int,List<int>> X, ref Stack<int> solution, ref bool foundSolution) {
        if(!X.Keys.Any()) foundSolution = true;
        if(foundSolution == true) return;
        else {
            int minimumColumn = X.OrderBy(kv => kv.Value.Count).First().Key;
            foreach(var row in X[minimumColumn]) {
                solution.Push(row);
                Stack<List<int>> columns = select(Y, ref X, row);
                iterate(Y,ref X,ref solution, ref foundSolution);
                if(foundSolution == true) return;
                deselect(Y, ref X, row, ref columns);
                solution.Pop();
            }
        }
    }

    private Stack<List<int>> select(Dictionary<int,List<int>> Y, ref Dictionary<int,List<int>> X, int row) {
        Stack<List<int>> columns = new Stack<List<int>>();
        foreach(var j in Y[row]) {
            foreach(var i in X[j])
                foreach(var k in Y[i]) 
                    if(k != j)
                        X[k].Remove(i);
            columns.Push(X[j]);
            X.Remove(j);
        }
        return columns;
    }

    private void deselect(Dictionary<int,List<int>> Y, ref Dictionary<int,List<int>> X, int row, ref Stack<List<int>> columns) {
        List<int> reversed = new List<int>(Y[row]);
        reversed.Reverse();
        foreach(var j in reversed) {
            if(!X.Keys.Contains(j)) X.Add(j, new List<int>());
            X[j] = columns.Pop();
            foreach(var i in X[j])
                foreach(var k in Y[i]) 
                    if(k != j) {
                        if(!X.Keys.Contains(k)) X.Add(k, new List<int>());
                        X[k].Add(i);
                    }
        }
    }

    public string solutionToCertificate(Stack<int> selectedSets, ExactCover exactCover) {
        string solution = "{";
        foreach(var i in selectedSets) {
            solution += "{";
            foreach(var j in exactCover.S[i]) {
                solution += j + ",";
            }
            solution = solution.TrimEnd(',') + "},";
        }
        return solution.TrimEnd(',') + "}";
    }
}
