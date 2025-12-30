namespace API.Problems.NPComplete.NPC_ACCEPTDFA;
using API.Interfaces;
using API.Problems.NPComplete.NPC_ACCEPTDFA.Solvers;
using API.Problems.NPComplete.NPC_ACCEPTDFA.Verifiers;
using API.Problems.NPComplete.NPC_ACCEPTDFA.Visualizations;
using SPADE;
using Xunit;

public class DFA : IGraphProblem<DFASolver, DFAVerifier, DFAVisualization, UtilCollectionGraph>
{
    // ----- Fields ----- //
    public string problemName { get; } = "DFA Acceptance";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Deterministic_finite_automaton";
    public string formalDefinition { get; } = "Acceptance Problem of a DFA = {<D,w> | D is a Deterministic Finite Automata that accepts a string w}";
    public string problemDefinition { get; } = "Acceptance Problem of a DFA is a problem that aims to see if a string input will be accepted by a particular Deterministic Finite Automata model.";
    public string source { get; } = "N/A";
    public string sourceLink { get; } = "N/A";
    private static readonly string _defaultInstance = " {({1,2,3},{a,b},{{1,2,a},{1,3,b}}, 1, {3}), b}";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;
    public string wikiName { get; } = "N/A";
    public DFASolver defaultSolver { get; } = new DFASolver();
    public DFAVerifier defaultVerifier { get; } = new DFAVerifier();
    public DFAVisualization defaultVisualization { get; } = new DFAVisualization();
    public string[] contributors { get; } = { "Michael Trosper" };

    // ----- Internal Graph ----- //
    internal UtilCollectionGraph graph { get; set; }

    // ----- Explicit Interface Implementation ----- //
    UtilCollectionGraph IGraphProblem<DFASolver, DFAVerifier, DFAVisualization, UtilCollectionGraph>.graph
    {
        get => graph;
    }

    // ----- Graphing Elements ----- //
    private List<string> _nodes = new List<string>();
    private List<char> _alphabet = new List<char>();
    private List<Tuple<string, string, char>> _edges = new List<Tuple<string, string, char>>();
    private string _startState;
    private List<string> _acceptStates = new List<string>();
    private string _inputString;
    public List<string> nodes
    {
        get => _nodes;
        set => _nodes = value;
    }
    public List<char> alphabet
    {
        get => _alphabet;
        set => _alphabet = value;
    }
    public List<Tuple<string, string, char>> edges
    {
        get => _edges;
        set => _edges = value;
    }
    public string startState 
    {
        get => _startState;
        set => _startState = value;
    }
    public List<string> acceptStates
    {
        get => _acceptStates;
        set => _acceptStates = value;
    }
    public string inputString
    {
        get => _inputString;
        set => _inputString = value;
    }

    /*
    private Graph _dFAAsGraph;
    public Graph dFAAsGraph
    {
        get => _dFAAsGraph;
        set => _dFAAsGraph = value;
    }
    */

    // ----- Methods and Constructors ----- //
    public DFA() : this(_defaultInstance) { }

    public DFA(string instance)
    {
        this.instance = instance;

        // TODO: implement parsing of string instance of DFA. SPADE is a class meant to help with this step, see https://github.com/Jetison333/SPADE/blob/main/Documentation/index.md for more information.
        StringParser DFA_Graph = new("{((N,A,E,S,F),I) | N is set, A is a set, E subset (N unorderedcross N unorderedcross A), F is a set}");
        DFA_Graph.parse(instance);

        // DFA Components //
        nodes = DFA_Graph["N"].ToList().Select(node => node.ToString()).ToList();
        alphabet = DFA_Graph["A"].ToList().Select(character => (character.ToString()[0])).ToList();
        edges = DFA_Graph["E"].ToList().Select(edge =>
        {
            List<UtilCollection> cast = edge.ToList();
            return new Tuple<string, string, char>(cast[0].ToString(), cast[1].ToString(), char.Parse(cast[2].ToString()));
        }).ToList();
        startState = DFA_Graph["S"].ToString();
        acceptStates = DFA_Graph["F"].ToList().Select(node => node.ToString()).ToList();
        inputString = DFA_Graph["I"].ToString();

        // Might Need To Adjust Since Edges are Not Just (Node X Node) Like In Other Examples //
        graph = new UtilCollectionGraph(DFA_Graph["N"], DFA_Graph["E"]);
    }
}