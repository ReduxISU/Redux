using API.Interfaces;
using System.Diagnostics;

namespace API.Problems.NPComplete.NPC_ExactCover.Solvers;
class DancingLinks : ISolver<ExactCover>
{

    // --- Fields ---
    public string solverName { get; } = "Dancing Links";
    public string solverDefinition { get; } = "";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };

    // --- Methods Including Constructors ---
    public DancingLinks()
    {

    }

    public string solve(ExactCover exactCover)
    {

        Dictionary<int, List<int>> Y = new Dictionary<int, List<int>>();
        Dictionary<int, List<int>> X = new Dictionary<int, List<int>>();
        Dictionary<string, int> names = new Dictionary<string, int>();

        for (int i = 0; i < exactCover.S.Count; i++)
        {
            Y.Add(i, new List<int>());
        }

        for (int i = 0; i < exactCover.X.Count; i++)
        {
            names.Add(exactCover.X[i], i);
            X.Add(i, new List<int>());
        }

        for (int i = 0; i < exactCover.S.Count; i++)
        {
            foreach (var j in exactCover.S[i])
            {
                X[names[j]].Add(i);
                Y[i].Add(names[j]);
            }
        }

        Stack<int> selectedSets = new Stack<int>();
        bool foundSolution = false;

        iterate(Y, X, ref selectedSets, ref foundSolution);

        if (selectedSets.Any())
        {
            return solutionToCertificate(selectedSets, exactCover);
        }

        return "{}";
    }

    private void iterate(Dictionary<int, List<int>> Y, Dictionary<int, List<int>> X, ref Stack<int> solution, ref bool foundSolution)
    {
        // If we already have a solution, return early
        if (foundSolution)
            return;

        // If X is empty, solution found
        if (!X.Any())
        {
            foundSolution = true;
            return;
        }

        // Create a safe copy of the minimum column selection
        var minColEntry = X.OrderBy(kv => kv.Value.Count).First();
        int minimumColumn = minColEntry.Key;
        var rowsToProcess = minColEntry.Value.ToList(); // Create a copy to iterate over

        foreach (var row in rowsToProcess)
        {
            solution.Push(row);
            Stack<List<int>> columns = select(Y, X, row);
            iterate(Y, X, ref solution, ref foundSolution);

            if (foundSolution)
                return;

            deselect(Y, X, row, ref columns);
            solution.Pop();
        }
    }

    private Stack<List<int>> select(Dictionary<int, List<int>> Y, Dictionary<int, List<int>> X, int row)
    {
        Stack<List<int>> columns = new Stack<List<int>>();
        var yRowCopy = Y[row].ToList(); // Create a copy to iterate over

        foreach (var j in yRowCopy)
        {
            var xjCopy = X[j].ToList(); // Create a copy to iterate over
            foreach (var i in xjCopy)
            {
                var yiCopy = Y[i].ToList(); // Create a copy to iterate over
                foreach (var k in yiCopy)
                {
                    if (k != j)
                        X[k].Remove(i); // Safe because we're not iterating X[k]
                }
            }
            columns.Push(new List<int>(X[j])); // Push a copy
            X.Remove(j); // Now safe because we're not iterating X directly
        }
        return columns;
    }

    private void deselect(Dictionary<int, List<int>> Y, Dictionary<int, List<int>> X, int row, ref Stack<List<int>> columns)
    {
        List<int> reversed = new List<int>(Y[row]);
        reversed.Reverse();

        foreach (var j in reversed)
        {
            if (!X.ContainsKey(j))
                X.Add(j, new List<int>());

            X[j] = columns.Pop();
            var xjCopy = X[j].ToList(); // Create a copy to iterate over

            foreach (var i in xjCopy)
            {
                var yiCopy = Y[i].ToList(); // Create a copy to iterate over
                foreach (var k in yiCopy)
                {
                    if (k != j)
                    {
                        if (!X.ContainsKey(k))
                            X.Add(k, new List<int>());
                        X[k].Add(i); // Safe because we're not iterating X[k]
                    }
                }
            }
        }
    }

    public string solutionToCertificate(Stack<int> selectedSets, ExactCover exactCover)
    {
        string solution = "{";
        foreach (var i in selectedSets)
        {
            solution += "{";
            foreach (var j in exactCover.S[i])
            {
                solution += j + ",";
            }
            solution = solution.TrimEnd(',') + "},";
        }
        return solution.TrimEnd(',') + "}";
    }
}
