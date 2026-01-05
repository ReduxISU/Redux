namespace API.Problems.NPComplete.NPC_ACCEPTDFA;
using API.Interfaces;
using API.Problems.NPComplete.NPC_ACCEPTDFA.Solvers;
using API.Problems.NPComplete.NPC_ACCEPTDFA.Verifiers;
using API.Problems.NPComplete.NPC_ACCEPTDFA.Visualizations;
using SPADE;
using Xunit;
using System.Linq;
using System.Collections.Generic;

// This is For DFA //
public class DFA : IGraphProblem<DFASolver, DFAVerifier, DFAVisualization, UtilCollectionGraph>
{
    // ----- Fields ----- //
    public string problemName { get; } = "DFA Acceptance";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Deterministic_finite_automaton";
    public string formalDefinition { get; } = "Acceptance Problem of a DFA = {<D,w> | D is a Deterministic Finite Automata that accepts a string w}";
    public string problemDefinition { get; } = "Acceptance Problem of a DFA is a problem that aims to see if a string input will be accepted by a particular Deterministic Finite Automata model.";
    public string source { get; } = "N/A";
    public string sourceLink { get; } = "N/A";

    // ✅ Updated default instance: edges use {} instead of ()
    private static readonly string _defaultInstance = "(({1,2,3},{a,b},{(1,a,2),(1,b,3)},1,{3}),b)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;
    public string wikiName { get; } = "N/A";
    public DFASolver defaultSolver { get; } = new DFASolver();
    public DFAVerifier defaultVerifier { get; } = new DFAVerifier();
    public DFAVisualization defaultVisualization { get; } = new DFAVisualization();
    public string[] contributors { get; } = { "Michael Trosper" };

    // Edge Structure //
    public record DFAEdge(string From, char Symbol, string To);

    // ----- Internal Graph ----- //
    internal UtilCollectionGraph graph { get; set; }

    // ----- Explicit Interface Implementation ----- //
    UtilCollectionGraph IGraphProblem<DFASolver, DFAVerifier, DFAVisualization, UtilCollectionGraph>.graph => graph;

    // ----- Graphing Elements ----- //
    private List<string> _nodes = new();
    private List<char> _alphabet = new();
    private List<DFAEdge> _edges = new();
    private string _startState;
    private List<string> _acceptStates = new();
    private string _inputString;

    public List<string> nodes { get => _nodes; set => _nodes = value; }
    public List<char> alphabet { get => _alphabet; set => _alphabet = value; }
    public List<DFAEdge> edges { get => _edges; set => _edges = value; }
    public string startState { get => _startState; set => _startState = value; }
    public List<string> acceptStates { get => _acceptStates; set => _acceptStates = value; }
    public string inputString { get => _inputString; set => _inputString = value; }

    // ----- Constructors ----- //
    public DFA() : this(_defaultInstance) { }

    public DFA(string instance)
    {
        this.instance = instance;

        // ---- SPADE grammar: edges as ordered triples (cross) ----
        StringParser DFA_Graph = new(
            "{((N,A,E,S,F),I) | " +
            "N is set, " +
            "A is set, " +
            "E is N cross A cross N, " +
            "S is string, " +
            "F is set, " +
            "I is string" +
            "}"
        );

        // ---- Parse the instance ----
        DFA_Graph.parse(instance);

        // ---- Retrieve components with null checks ----
        UtilCollection N = DFA_Graph["N"] ?? throw new InvalidOperationException("Failed to parse N (nodes).");
        UtilCollection A = DFA_Graph["A"] ?? throw new InvalidOperationException("Failed to parse A (alphabet).");
        UtilCollection E = DFA_Graph["E"] ?? throw new InvalidOperationException("Failed to parse E (edges).");
        UtilCollection S = DFA_Graph["S"] ?? throw new InvalidOperationException("Failed to parse S (start state).");
        UtilCollection F = DFA_Graph["F"] ?? throw new InvalidOperationException("Failed to parse F (accept states).");
        UtilCollection I = DFA_Graph["I"] ?? throw new InvalidOperationException("Failed to parse I (input string).");

        // ---- Convert nodes and alphabet ----
        nodes = N.ToList().Select(x => x.ToString()).ToList();
        alphabet = A.ToList().Select(x => x.ToString()[0]).ToList();

        // ---- Parse and validate edges ----
        edges = new List<DFAEdge>();
        foreach (var e in E.ToList())
        {
            if (e is UtilCollection tuple && tuple.Count() == 3)
            {
                string from = tuple[0].ToString();
                char symbol = tuple[1].ToString()[0];
                string to = tuple[2].ToString();

                // Validate edge
                if (!nodes.Contains(from))
                    throw new InvalidOperationException($"Edge From node '{from}' not in N.");
                if (!alphabet.Contains(symbol))
                    throw new InvalidOperationException($"Edge Symbol '{symbol}' not in A.");
                if (!nodes.Contains(to))
                    throw new InvalidOperationException($"Edge To node '{to}' not in N.");

                edges.Add(new DFAEdge(from, symbol, to));
            }
            else throw new InvalidOperationException("Each edge must be a tuple with 3 elements");
        }

        // ---- Parse remaining components ----
        startState = S.ToString();
        if (!nodes.Contains(startState))
            throw new InvalidOperationException($"Start state '{startState}' not in N.");

        acceptStates = F.ToList().Select(x => x.ToString()).ToList();
        foreach (var f in acceptStates)
        {
            if (!nodes.Contains(f))
                throw new InvalidOperationException($"Accept state '{f}' not in N.");
        }

        inputString = I.ToString();

        // ---- Build internal graph ----
        graph = new UtilCollectionGraph(N, E);
    }
}