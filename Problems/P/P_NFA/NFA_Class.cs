namespace API.Problems.P.P_NFA;
using API.Interfaces;
using API.Problems.P.P_NFA.Solvers;
using API.Problems.P.P_NFA.Verifiers;
using API.Problems.P.P_NFA.Visualizations;
using SPADE;
using System.Linq;
using System.Collections.Generic;
using API.Interfaces.Graphs;

class NFA : IGraphProblem<NFASolver, NFAVerifier, NFAVisualization, WeightedDirectedGraph>
{
    // ----- Fields ----- //
    public string problemName { get; } = "NFA Acceptance";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Nondeterministic_finite_automaton";
    public string formalDefinition { get; } = "Acceptance Problem of a NFA = {<N,w> | N is a Non-deterministic Finite Automata that accepts a string w}";
    public string problemDefinition { get; } = "Acceptance Problem of a NFA is a problem that aims to see if a string input will be accepted by a particular Non-deterministic Finite Automata model.";
    public string source { get; } = "N/A";
    public string sourceLink { get; } = "N/A";

    // Follows Formal Definition of NFA (Q, Σ, δ, q₀, F) With Input String //
    // Q = Set of Nodes //
    // Σ = Characters In the Alphabet //
    // δ = (node, char edge value, node) //
    // q₀ = Start State //
    // F = Set of Accept State(s) //
    private static readonly string _defaultInstance = "(({1,2,3},{a,b},{(1,a,2),(1,ε,2),(1,b,3),(2,a,2),(2,ε,3),(2,b,2),(3,a,2),(3,b,3)},1,{2}),a)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;
    public string wikiName { get; } = "N/A";
    public NFASolver defaultSolver { get; } = new NFASolver();
    public NFAVerifier defaultVerifier { get; } = new NFAVerifier();
    public NFAVisualization defaultVisualization { get; } = new NFAVisualization();
    public string[] contributors { get; } = { "Michael Trosper" };

    // Edge Structures //
    public record NFAEdge (string From, char Symbol, string To);
    //public record CombinedDFAEdge (string From, string Symbols, string To);

    // ----- Internal Graph ----- //
    //internal UtilCollectionGraph graph { get; set; }

    // ----- Explicit Interface Implementation ----- //
    //UtilCollectionGraph IGraphProblem<DFASolver, DFAVerifier, DFAVisualization, UtilCollectionGraph>.graph => graph;

    // ----- Internal Graph ----- //
    internal WeightedDirectedGraph graph { get; }

    // ----- Explicit Interface Implementation ----- //
    WeightedDirectedGraph IGraphProblem<NFASolver, NFAVerifier, NFAVisualization, WeightedDirectedGraph>.graph => graph;

    // ----- Deterministic Finite Automata Elements ----- //
    private List<string> _nodes = new();
    private List<char> _alphabet = new();
    private List<NFAEdge> _edges = new();
    private string _startState = string.Empty;
    private List<string> _acceptStates = new();
    private string _inputString = string.Empty;

    public List<string> nodes { get => _nodes; set => _nodes = value; }
    public List<char> alphabet { get => _alphabet; set => _alphabet = value; }
    public List<NFAEdge> edges { get => _edges; set => _edges = value; }
    public string startState { get => _startState; set => _startState = value; }
    public List<string> acceptStates { get => _acceptStates; set => _acceptStates = value; }
    public string inputString { get => _inputString; set => _inputString = value; }

    // ----- Constructors ----- //
    public NFA() : this(_defaultInstance) { }

    public NFA(string instance)
    {
        this.instance = instance;

        // ---- SPADE grammar: edges as ordered triples (cross) ----
        StringParser NFA_Graph = new(
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
        NFA_Graph.parse(instance);

        // Retrieve Components Or Flag Null //
        UtilCollection N = NFA_Graph["N"] ?? throw new InvalidOperationException("Failed to parse N (nodes).");
        UtilCollection A = NFA_Graph["A"] ?? throw new InvalidOperationException("Failed to parse A (alphabet).");
        UtilCollection E = NFA_Graph["E"] ?? throw new InvalidOperationException("Failed to parse E (edges).");
        UtilCollection S = NFA_Graph["S"] ?? throw new InvalidOperationException("Failed to parse S (start state).");
        UtilCollection F = NFA_Graph["F"] ?? throw new InvalidOperationException("Failed to parse F (accept states).");
        UtilCollection I = NFA_Graph["I"] ?? throw new InvalidOperationException("Failed to parse I (input string).");

        // Convert Nodes and Alphabet //
        nodes = N.ToList().Select(x => x.ToString()).ToList();
        alphabet = A.ToList().Select(x => x.ToString()[0]).ToList();

        // Parse and Validate Edges //
        edges = new List<NFAEdge>();
        foreach (var e in E.ToList())
        {
            if (e is UtilCollection tuple && tuple.Count() == 3)
            {
                string from = tuple[0].ToString();
                string symbolText = tuple[1].ToString();
                string to = tuple[2].ToString();

                // Normalize common epsilon representations to the single char U+03B5
                char symbol;
                if (symbolText == "ε" || string.Equals(symbolText, "\\u03B5", StringComparison.OrdinalIgnoreCase) || string.Equals(symbolText, "epsilon", StringComparison.OrdinalIgnoreCase) || symbolText == "eps")
                {
                    symbol = '\u03B5';
                }
                else if (symbolText.Length >= 1)
                {
                    symbol = symbolText[0];
                }
                else
                {
                    throw new InvalidOperationException("Edge symbol empty");
                }

                // Validate Edge //
                if (!nodes.Contains(from))
                    throw new InvalidOperationException($"Edge From node '{from}' not in N.");
                if (!alphabet.Contains(symbol) && symbol != '\u03B5')
                    throw new InvalidOperationException($"Edge Symbol '{symbolText}' not in A.");
                if (!nodes.Contains(to))
                    throw new InvalidOperationException($"Edge To node '{to}' not in N.");

                edges.Add(new NFAEdge(from, symbol, to));
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
        List<LabeledEdge> joinedEdges = new List<LabeledEdge>();
        // Track What Nodes Are Connected To One Another //
        var connections = new Dictionary<(string From, string To), string>();

        // Join Edges That Have the Same From and To Destination //
        foreach (var edge in edges)
        {
            var from = edge.From;
            var to = edge.To;

            var edgeValue = edge.Symbol == 'ε' ? "epsilon" : edge.Symbol.ToString();

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

        // Save New Edges As LabeledEdge Format For API Graph Conversion //
        foreach (var connectionPair in connections) { joinedEdges.Add(new LabeledEdge(connectionPair.Key.Item1, connectionPair.Key.Item2, connectionPair.Value)); }

        // Build Graph //
        graph = new WeightedDirectedGraph(nodes, joinedEdges, startState, acceptStates);
    }
}