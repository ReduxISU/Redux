using System;
using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.P.P_EDITDISTANCE;
using SPADE;


namespace API.Problems.P.P_EDITDISTANCE.Solvers;

class EditDistanceDPSolver : ISolver<P_EDITDISTANCE>
{
    public string solverName { get; } = "Dynamic Programming Edit Distance Solver";
    public string solverDefinition { get; } = "Finds the edit distance between two strings using dynamic programming.";
    public string source { get; } = "https://en.wikipedia.org/wiki/Edit_distance";
    public string[] contributors { get; } = { "Kaosi Ibeabuchi", "Diya Pandey", "Srijan Pant" };

}