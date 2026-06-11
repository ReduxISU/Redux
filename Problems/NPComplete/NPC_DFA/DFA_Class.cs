namespace API.Problems.NPComplete.NPC_DFA;
using API.Interfaces;
using API.Problems.NPComplete.NPC_DFA.Solvers;
using API.Problems.NPComplete.NPC_DFA.Verifiers;
using API.Problems.NPComplete.NPC_DFA.Visualizations;
using SPADE;
using System.Linq;
using System.Collections.Generic;
using API.Interfaces.Graphs;

class DFA : IGraphProblem<DFASolver, DFAVerifier, DFAVisualization, WeightedDirectedGraph>
{
    // ----- Fields ----- //
    public string problemName { get; } = "DFA Acceptance";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Deterministic_finite_automaton";
    public string formalDefinition { get; } = "Acceptance Problem of a DFA = {<D,w> | D is a Deterministic Finite Automata that accepts a string w}";
    public string problemDefinition { get; } = "Acceptance Problem of a DFA is a problem that aims to see if a string input will be accepted by a particular Deterministic Finite Automata model.";
    public string source { get; } = "N/A";
    public string sourceLink { get; } = "N/A";

    // Follows Formal Definition of DFA (Q, Σ, δ, q₀, F) With Input String //
    // Q = Set of Nodes //
    // Σ = Characters In the Alphabet //
    // δ = (node, char edge value, node) //
    // q₀ = Start State //
    // F = Set of Accept State(s) //
    private static readonly string _defaultInstance = "(({1,2,3},{a,b},{(1,a,2),(1,b,3),(2,a,2),(2,b,2),(3,a,2),(3,b,3)},1,{2}),a)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;
    public string wikiName { get; } = "N/A";
    public DFASolver defaultSolver { get; } = new DFASolver();
    public DFAVerifier defaultVerifier { get; } = new DFAVerifier();
    public DFAVisualization defaultVisualization { get; } = new DFAVisualization();
    public string[] contributors { get; } = { "Michael Trosper" };

    // Edge Structures //
    public record DFAEdge (string From, char Symbol, string To);
    //public record CombinedDFAEdge (string From, string Symbols, string To);

    // ----- Internal Graph ----- //
    //internal UtilCollectionGraph graph { get; set; }

    // ----- Explicit Interface Implementation ----- //
    //UtilCollectionGraph IGraphProblem<DFASolver, DFAVerifier, DFAVisualization, UtilCollectionGraph>.graph => graph;

    // ----- Internal Graph ----- //
    internal WeightedDirectedGraph graph { get; }

    // ----- Explicit Interface Implementation ----- //
    WeightedDirectedGraph IGraphProblem<DFASolver, DFAVerifier, DFAVisualization, WeightedDirectedGraph>.graph => graph;

    // ----- Deterministic Finite Automata Elements ----- //
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

        // Parse the Instance //
        DFA_Graph.parse(instance);

        // Retrieve Components Or Flag Null //
        UtilCollection N = DFA_Graph["N"] ?? throw new InvalidOperationException("Failed to parse N (nodes).");
        UtilCollection A = DFA_Graph["A"] ?? throw new InvalidOperationException("Failed to parse A (alphabet).");
        UtilCollection E = DFA_Graph["E"] ?? throw new InvalidOperationException("Failed to parse E (edges).");
        UtilCollection S = DFA_Graph["S"] ?? throw new InvalidOperationException("Failed to parse S (start state).");
        UtilCollection F = DFA_Graph["F"] ?? throw new InvalidOperationException("Failed to parse F (accept states).");
        UtilCollection I = DFA_Graph["I"] ?? throw new InvalidOperationException("Failed to parse I (input string).");

        // Convert Nodes and Alphabet //
        nodes = N.ToList().Select(x => x.ToString()).ToList();
        alphabet = A.ToList().Select(x => x.ToString()[0]).ToList();

        // Parse and Validate Edges //
        edges = new List<DFAEdge>();
        foreach (var e in E.ToList())
        {
            if (e is UtilCollection tuple && tuple.Count() == 3)
            {
                string from = tuple[0].ToString();
                char symbol = tuple[1].ToString()[0];
                string to = tuple[2].ToString();

                // Validate Edge //
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

        // Parse Remaining Components //
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
        if (inputString.Length == 0) { inputString = "ε"; }

        // Join Multiple Edges To Same Output To Single Edge For Graph //

        // Save New Edges With Multiple Char Paths, Ex:(Node, "a,b", Node) //
        List<WeightedEdge> joinedEdges = new List<WeightedEdge>();
        // Track What Nodes Are Connected To One Another //
        var connections = new Dictionary<(string From, string To), string>();

        // Join Edges That Have the Same From and To Destination //
        foreach (var edge in edges)
        {
            var from = edge.From;
            var to = edge.To;
            var edgeValue = edge.Symbol.ToString();

            if (!connections.ContainsKey((from, to)))
            {
                connections.Add((from, to), edgeValue);
            }
            else
            {
                if (connections[(from, to)].Contains(edgeValue)) { continue; }
                else { connections[(from, to)] = connections[(from, to)] += "," + edgeValue; }
            }
        }

        // Save New Edges As WeightedEdge Format For API Graph Conversion //
        foreach (var connectionPair in connections) { joinedEdges.Add(new WeightedEdge(connectionPair.Key.Item1, connectionPair.Key.Item2, connectionPair.Value)); }

        // Build Graph //
        graph = new WeightedDirectedGraph(nodes, joinedEdges, startState, acceptStates);
    }
}