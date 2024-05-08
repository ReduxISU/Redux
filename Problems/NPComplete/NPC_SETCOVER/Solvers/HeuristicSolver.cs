using API.Interfaces;
using System.Diagnostics;

namespace API.Problems.NPComplete.NPC_SETCOVER.Solvers;
class HeuristicSolver : ISolver
{

    // --- Fields ---
    private string _solverName = "Heuristic Solver";
    private string _solverDefinition = "";
    private string _source = "";
    private string[] _contributors = { "Andrija Sevaljevic" };


    // --- Properties ---
    public string solverName
    {
        get
        {
            return _solverName;
        }
    }
    public string solverDefinition
    {
        get
        {
            return _solverDefinition;
        }
    }
    public string source
    {
        get
        {
            return _source;
        }
    }
    public string[] contributors
    {
        get
        {
            return _contributors;
        }
    }
    // --- Methods Including Constructors ---
    public HeuristicSolver()
    {

    }

    public string solve(SETCOVER setCover)
    {

        Dictionary<string, List<string>> Y = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> X = new Dictionary<string, List<string>>();

        for (int i = 0; i < setCover.subsets.Count; i++)
        {
            Y.Add(i.ToString(), new List<string>());
        }

        for (int i = 0; i < setCover.universal.Count; i++)
        {
            X.Add(setCover.universal[i], new List<string>());
        }

        for (int i = 0; i < setCover.subsets.Count; i++)
        {
            foreach (var j in setCover.subsets[i])
            {
                X[j].Add(i.ToString());
                Y[i.ToString()].Add(j);
            }
        }

        Stack<string> selectedSets = new Stack<string>();
        bool foundSolution = false;
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        iterate(Y, ref X, ref selectedSets, ref foundSolution, setCover.K);
        stopwatch.Stop();

        Console.WriteLine(stopwatch.ElapsedMilliseconds);

        if (selectedSets.Any())
        {
            return solutionToCertificate(selectedSets, setCover);
        }

        return "{}";
    }

    private void iterate(Dictionary<string, List<string>> Y, ref Dictionary<string, List<string>> X, ref Stack<string> solution, ref bool foundSolution, int K)
    {
        if (solution.Count() > K) return;
        if (!X.Keys.Any()) foundSolution = true;
        if (foundSolution == true) return;
        else
        {
            string minimumColumn = X.OrderBy(kv => kv.Value.Count).First().Key;
            foreach (var row in X[minimumColumn].OrderByDescending(e => Y[e].Count))
            {
                solution.Push(row);
                Stack<List<string>> columns = select(Y, ref X, row);
                iterate(Y, ref X, ref solution, ref foundSolution, K);
                if (foundSolution == true) return;
                deselect(Y, ref X, row, ref columns);
                solution.Pop();
            }
        }
    }

    private Stack<List<string>> select(Dictionary<string, List<string>> Y, ref Dictionary<string, List<string>> X, string row)
    {
        Stack<List<string>> columns = new Stack<List<string>>();
        foreach (var j in Y[row])
        {
            if (X.Keys.Contains(j))
            {
                columns.Push(X[j]);
                X.Remove(j);
            }
            else
            {
                columns.Push(new List<string>());
            }
        }

        return columns;
    }

    private void deselect(Dictionary<string, List<string>> Y, ref Dictionary<string, List<string>> X, string row, ref Stack<List<string>> columns)
    {
        List<string> reversed = new List<string>(Y[row]);
        reversed.Reverse();
        foreach (var j in reversed)
        {
            if (columns.Peek().Any()) X.Add(j, columns.Pop());
        }
    }

    public string solutionToCertificate(Stack<string> selectedSets, SETCOVER setCover)
    {
        string solution = "{";
        foreach (var i in selectedSets)
        {
            solution += "{";
            foreach (var j in setCover.subsets[Int32.Parse(i)])
            {
                solution += j + ",";
            }
            solution = solution.TrimEnd(',') + "},";
        }
        return solution.TrimEnd(',') + "}";
    }
}
