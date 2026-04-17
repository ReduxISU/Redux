using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.NPComplete.NPC_NFA;
using Xunit;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace API.Problems.NPComplete.NPC_NFA.Solvers;

public class NFASolver : ISolver<NFA>
{
    public string solverName { get; } = "NFA Solver";
    public string solverDefinition { get; } = "This solver enumerates all accepting runs of a nondeterministic finite automaton (returns all successful state sequences).";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    public bool timerHasExpired { get; set; }

    public NFASolver() { }

    public string solve(NFA problem)
    {
        // Normalize empty-input representation "ε"
        string rawInput = problem.inputString ?? "";
        string input = rawInput == "ε" ? "" : rawInput;

        // Validate characters
        foreach (char c in input)
        {
            if (!problem.alphabet.Contains(c))
                return $"No Solution: Input contains character '{c}' not in NFA alphabet";
        }

        var edges = problem.edges; // List<NFAEdge>
        var acceptPaths = new List<List<string>>();

        // DFS exploring nondeterministic runs; visitedPerPath prevents infinite loops for epsilon cycles
        void DFS(string state, int pos, List<string> path, HashSet<(string, int)> visitedPerPath)
        {
            // If consumed all input and in accept state, record a copy of the path
            if (pos >= input.Length && problem.acceptStates.Contains(state))
            {
                acceptPaths.Add(new List<string>(path));
                // Do not return: still allow further epsilon transitions that may produce other accept runs
            }

            // Explore epsilon transitions (do not advance position)
            foreach (var e in edges.Where(x => x.From == state && x.Symbol == 'ε'))
            {
                var key = (e.To, pos);
                if (visitedPerPath.Contains(key)) continue;
                visitedPerPath.Add(key);
                path.Add(e.To);
                DFS(e.To, pos, path, visitedPerPath);
                path.RemoveAt(path.Count - 1);
                visitedPerPath.Remove(key);
            }

            // Explore regular symbol transitions (advance position)
            if (pos < input.Length)
            {
                char need = input[pos];
                foreach (var e in edges.Where(x => x.From == state && x.Symbol == need))
                {
                    var key = (e.To, pos + 1);
                    if (visitedPerPath.Contains(key)) continue;
                    visitedPerPath.Add(key);
                    path.Add(e.To);
                    DFS(e.To, pos + 1, path, visitedPerPath);
                    path.RemoveAt(path.Count - 1);
                    visitedPerPath.Remove(key);
                }
            }
        }

        // Seed DFS with start state
        var startPath = new List<string> { problem.startState };
        var startVisited = new HashSet<(string, int)> { (problem.startState, 0) };
        DFS(problem.startState, 0, startPath, startVisited);

        // Build output
        if (acceptPaths.Count == 0)
        {
            return "No Solution Exists: No run accepts the input";
        }

        var sb = new StringBuilder();
        foreach (var p in acceptPaths)
        {
            sb.AppendLine("The sequence of states to accept is: " + string.Join(", ", p));
        }

        return sb.ToString().TrimEnd();
    }
}