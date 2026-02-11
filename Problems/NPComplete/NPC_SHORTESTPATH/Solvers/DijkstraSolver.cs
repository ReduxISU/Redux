using System;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.NPComplete.NPC_SHORTESTPATH;

namespace NPComplete.NPC_SHORTESTPATH.Solvers;

class DijkstraSolver : ISolver<SHORTESTPATH>
{
	// ----- Fields ----- //
	public string solverName { get; } = "Dijkstra's Algorithm";
	public string solverDefinition { get; } = "This solver implements Dijkstra's algorithm to find the shortest path from a source node to a target node in a positively weighted graph.";
	public string source { get; } = "";
	public string[] contributors { get; } = { "Rajit Nilkar" };
	public bool timerHasExpired { get; set; };


}
