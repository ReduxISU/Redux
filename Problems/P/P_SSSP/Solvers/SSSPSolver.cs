using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using SPADE;
using System;

namespace API.Problems.P.P_SSSP.Solvers;

class SSSPSolver : ISolver<SSSP>
{
    public string solverName { get; } = "Dijkstra's Algorithm";
    public string solverDefinition { get; } = "";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Rajit Nilkar" };
    public bool timerHasExpired { get; set; }
    PriorityQueue<string, int>? pq;

    public string solve(SSSP problem)
    {
        return ""; // To Do Dijkstra's algorithm implementation for SSSP problem
    }
}