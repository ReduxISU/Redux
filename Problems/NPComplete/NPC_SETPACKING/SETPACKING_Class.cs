using API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using API.Interfaces.Graphs;
using SPADE;
using API.Problems.NPComplete.NPC_SETPACKING.Solvers;
using API.Problems.NPComplete.NPC_SETPACKING.Verifiers;
using API.Problems.NPComplete.NPC_SETPACKING.Visualizations;

namespace API.Problems.NPComplete.NPC_SETPACKING;

public class SETPACKING : IProblem<
    SetPackingBruteForce,
    SetPackingVerifier,
    SetPackingDefaultVisualization>
{
    public string problemName { get; } = "Set Packing";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Set_packing";
    public string formalDefinition { get; } = "Choose K sets with no overlap";
    public string problemDefinition { get; } = "No shared elements";
    public string source { get; } = "Wikipedia";
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Set_packing";
    public string wikiName { get; } = "";
    public string[] contributors { get; } = { "Sansar Kharal" };

    private static readonly string _defaultInstance =
    "({{S1:{a,b,c},S2:{b,d},S3:{c,e},S4:{d,e,f},S5:{f,g},S6:{a,g},S7:{h,i},S8:{i,j}}},3)";

    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;

    public Dictionary<string, List<string>> sets { get; set; }
    public List<string> setNames { get; set; }
    public int K { get; set; }

    internal UtilCollectionGraph graph { get; set; }

    public SetPackingBruteForce defaultSolver { get; } = new();
    public SetPackingVerifier defaultVerifier { get; } = new();
    public SetPackingDefaultVisualization defaultVisualization { get; } = new();

    public SETPACKING() : this(_defaultInstance) { }

    public SETPACKING(string input)
    {
        instance = input;
        sets = new();
        setNames = new();
        parseInstance(input);
    }

    private void parseInstance(string input)
    {
        input = input.Trim('(', ')');

        int lastComma = input.LastIndexOf(',');
        string setsPart = input.Substring(0, lastComma);
        string kPart = input.Substring(lastComma + 1);

        K = int.Parse(kPart);
        setsPart = setsPart.Trim('{', '}');

        var rawSets = setsPart.Split("},", StringSplitOptions.RemoveEmptyEntries);

        foreach (var raw in rawSets)
        {
            var parts = raw.Trim(' ', '{', '}').Split(':');
            string setName = parts[0].Trim();

            var elements = parts[1].Trim('{', '}')
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .ToList();

            sets[setName] = elements;
            setNames.Add(setName);
        }

        UtilCollection nodes = new(
            setNames.Select(n => new UtilCollection(n)).ToList()
        );

        List<UtilCollection> edgeItems = new();

        for (int i = 0; i < setNames.Count; i++)
        {
            for (int j = i + 1; j < setNames.Count; j++)
            {
                if (sets[setNames[i]].Intersect(sets[setNames[j]]).Any())
                {
                    edgeItems.Add(new UtilCollection(new List<UtilCollection>
                    {
                        new UtilCollection(setNames[i]),
                        new UtilCollection(setNames[j])
                    }));
                }
            }
        }

        graph = new UtilCollectionGraph(nodes, new UtilCollection(edgeItems));
    }
}