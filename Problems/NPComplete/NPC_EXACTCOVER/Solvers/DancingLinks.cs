using API.Interfaces;
using System.Diagnostics;

namespace API.Problems.NPComplete.NPC_EXACTCOVER.Solvers;
class DancingLinks : ISolver<EXACTCOVER> {

    // --- Fields ---
    public string solverName {get;} = "Dancing Links";
    public string solverDefinition {get;} = "";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Andrija Sevaljevic"};
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Exact search WITH pruning (Algorithm X via dancing links) -- distinct
    // from ExactCoverBruteForce, which does not prune.
    public SolverType solverType { get; } = SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Choosing the minimum-remaining-rows column each step (iterate's OrderBy) is a heuristic that cuts
    // branching in practice, but it does not change the worst-case bound: any subset of the s = |S| rows
    // could still be visited by some sequence of column choices, so the search tree is still bounded by
    // the standard O(2^s) exact-cover worst case. Each node's select/deselect walks, for every column a
    // row covers (<= x = |X|), every row covering that column (<= s), every column that row covers
    // (<= x), doing an O(s) List<int>.Remove -- O(s^2 * x^2) per node in the worst (densest) case.
    public string complexity { get; } = "O(2^s * s^2 * x^2), s = |S| (rows/subsets), x = |X| (columns/universe)";

    // --- Methods Including Constructors ---
    public DancingLinks() {
        
    }
       
    public string solve(EXACTCOVER exactCover) {

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
        iterate(Y,ref X,ref selectedSets, ref foundSolution);

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

    public string solutionToCertificate(Stack<int> selectedSets, EXACTCOVER exactCover) {
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
