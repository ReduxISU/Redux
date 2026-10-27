using System.Text.Json.Serialization;

namespace API.Interfaces;

/// <summary>
/// The renderer contract between this API and the GUI. Every member Redux_GUI is shown
/// (VisualizationTypeCatalog.GuiWireValues) except <see cref="Unimplemented"/> must have a matching key in
/// Redux_GUI/components/Visualization/svgs/Visualizations.js, which the Redux_GUI repo checks
/// with `npm run check:visualizations`, and those members are exactly the ones in
/// Documentation/visualization-types.json (checked by VisualizationType_Tests.ManifestMatchesEnum). A picture type added
/// for the visualization redesign stays out of both until Redux_GUI can draw it: it is listed only for clients that ask
/// with includePictures=true.
/// <para>
/// A tag set (#914): a visualization declares every member that fits, joined with <c>|</c>, in any order. The
/// values are powers of two in declared order; the API lists them in that order as <c>visualizationTypes</c>, and the
/// deprecated single <c>visualizationType</c> field holds the first of them. See <see cref="Tags"/>.
/// </para>
/// </summary>
[Flags]
[JsonConverter(typeof(TagJsonConverter<VisualizationType>))]
public enum VisualizationType {
    /// <summary>No renderer is claimed. The GUI shows an explicit "not renderable" state.</summary>
    Unimplemented = 0,
    /// <summary>Force-directed D3 graph.</summary>
    GraphD3 = 1 << 0,
    /// <summary>TikZ/LaTeX-rendered graph.</summary>
    GraphLaTeX = 1 << 1,
    /// <summary>D3 set/family-of-sets view.</summary>
    SetD3 = 1 << 2,
    /// <summary>CNF clause view.</summary>
    BooleanSatisfiability = 1 << 3,
    /// <summary>D3-drawn quantum circuit.</summary>
    QuantumCircuitD3 = 1 << 4,
    /// <summary>QASM circuit rendered by Q.js.</summary>
    QuantumCircuitQjs = 1 << 5,
    /// <summary>Pump scheduling timeline.</summary>
    PumpSchedule = 1 << 6,
    /// <summary>Step-table view backed by API_TableJSON.</summary>
    DynamicTable = 1 << 7,
    /// <summary>
    /// The shared Graph picture: a static payload plus one frame of named states per solver step. Reported only
    /// in the <c>format=frames</c> response of <c>/ProblemProvider/visualize</c>; visualizations keep reporting their legacy
    /// type (<see cref="GraphD3"/>, <see cref="GraphLaTeX"/>) in /info so old frontends are unaffected.
    /// </summary>
    Graph = 1 << 8,
    /// <summary>
    /// The Grouped Graph picture (the clause graph of SAT and 3SAT): literals grouped into one cluster per clause, a static
    /// payload plus one frame of named states per solver step. Like <see cref="Graph"/> it is reported only as the <c>type</c>
    /// of the <c>format=frames</c> response of <c>/ProblemProvider/visualize</c>; /info keeps the legacy
    /// <see cref="BooleanSatisfiability"/>.
    /// </summary>
    GroupedGraph = 1 << 9,
    /// <summary>
    /// The Reduction View: problem A and problem B side by side, the gadget map between them, and three phases of frames
    /// (build the target, solve it, map the answer back). Each pane is drawn with its problem's own picture. Like
    /// <see cref="Graph"/> it is reported only by its own endpoint (<c>/ProblemProvider/reductionView</c>, as <c>type</c>);
    /// /info is unaffected.
    /// </summary>
    Reduction = 1 << 10,
    /// <summary>
    /// The Bipartite Graph picture (Set Cover, Exact Cover, Exact Hitting Set): elements in one column, sets in the other, an
    /// edge per membership, a static payload plus one frame of named states per solver step. Like <see cref="Graph"/> it is
    /// reported only as the <c>type</c> of the <c>format=frames</c> response of <c>/ProblemProvider/visualize</c>; /info keeps
    /// the legacy <see cref="SetD3"/>.
    /// </summary>
    BipartiteGraph = 1 << 11,
    /// <summary>
    /// The Automaton picture (DFA, NFA): states layered from the start state, merged transition arrows, an input tape, a
    /// Garbage state for a DFA's missing moves and the runs of an NFA for the path picker; a static payload plus one frame of
    /// named states per solver step. Like <see cref="Graph"/> it is reported only as the <c>type</c> of the
    /// <c>format=frames</c> response of <c>/ProblemProvider/visualize</c>; /info keeps the legacy <see cref="GraphLaTeX"/>.
    /// </summary>
    Automaton = 1 << 12,
    /// <summary>
    /// The Layered Graph picture (Topological Sort, Feedback Arc Set, Feedback Node Set, Strongly Connected Components,
    /// Single-Source and Single-Pair Shortest Path): a directed graph ranked left to right with its back edges marked; a
    /// static payload plus one frame of named states per solver step. Like <see cref="Graph"/> it is reported only as the
    /// <c>type</c> of the <c>format=frames</c> response of <c>/ProblemProvider/visualize</c>; /info keeps the legacy type.
    /// </summary>
    LayeredGraph = 1 << 13,
    /// <summary>
    /// The Flow Network picture (Minimum S-T Cut): the source on the left, the sink on the right, every edge with its flow
    /// and capacity, one frame per augmenting path and a last frame with the S side, the T side and the cut; a static payload
    /// plus one frame of named states per solver step. Like <see cref="Graph"/> it is reported only as the <c>type</c> of the
    /// <c>format=frames</c> response of <c>/ProblemProvider/visualize</c>; /info keeps the legacy <see cref="GraphD3"/>.
    /// </summary>
    FlowNetwork = 1 << 14,
    /// <summary>
    /// The Packing picture (Partition, Subset Sum, Knapsack, Bin Packing): items in a pool and containers drawn to scale with
    /// their target or limit lines, and which container each item is in; a static payload plus one frame of named states per
    /// solver step. Like <see cref="Graph"/> it is reported only as the <c>type</c> of the <c>format=frames</c> response of
    /// <c>/ProblemProvider/visualize</c>; /info keeps the legacy <see cref="SetD3"/>.
    /// </summary>
    Packing = 1 << 15,
    /// <summary>
    /// The Table picture: rows and columns of text cells drawn in HTML, in one of three shapes: a dynamic-programming grid
    /// (Edit Distance), a constraint matrix (0-1 Integer Linear Programming) or a trace table that gains a row per step (the
    /// second views of DFA, NFA, SSSP and SPSP). A static payload plus one frame of values and named states per solver step.
    /// Like <see cref="Graph"/> it is reported only as the <c>type</c> of the <c>format=frames</c> response of
    /// <c>/ProblemProvider/visualize</c>; /info keeps the legacy type (<see cref="DynamicTable"/>).
    /// </summary>
    Table = 1 << 16,
    /// <summary>
    /// The Board picture (Sudoku, N-Queens): a grid of squares, the instance's clues, the digits and pieces a solver puts
    /// down, and links from a square ruled out to the square that rules it out; a static payload plus one frame of named
    /// states per solver step. Like <see cref="Graph"/> it is reported only as the <c>type</c> of the <c>format=frames</c>
    /// response of <c>/ProblemProvider/visualize</c>; /info keeps the legacy type.
    /// </summary>
    Board = 1 << 17,
    /// <summary>
    /// The Schedule picture (Job Sequencing, both Pump Scheduling problems): time runs left to right, drawn to scale. Jobs as
    /// bars with deadline ticks on one machine, or pumps on and off for each of 24 hours with the tank level and the cost of
    /// each hour; a static payload plus one frame of named states per solver step. Like <see cref="Graph"/> it is reported
    /// only as the <c>type</c> of the <c>format=frames</c> response of <c>/ProblemProvider/visualize</c>; /info keeps the
    /// legacy type.
    /// </summary>
    Schedule = 1 << 18,
    /// <summary>
    /// The Tripartite Matching picture (3-Dimensional Matching): the elements of X, Y and Z in three columns, each triple of
    /// M a line through its three elements, the triples a solver chooses, the elements they use once or more than once, and
    /// the triples a choice rules out; a static payload plus one frame of named states per solver step. Like
    /// <see cref="Graph"/> it is reported only as the <c>type</c> of the <c>format=frames</c> response of
    /// <c>/ProblemProvider/visualize</c>; /info keeps the legacy type.
    /// </summary>
    TripartiteMatching = 1 << 19,
    /// <summary>
    /// The Geometry picture (Convex Hull): points drawn to scale on real axes, the hulls a solver builds and merges, the chain
    /// it walks, the points it drops and, at the end, the hull's corners in order; a static payload plus one frame of named
    /// states per solver step. Like <see cref="Graph"/> it is reported only as the <c>type</c> of the <c>format=frames</c>
    /// response of <c>/ProblemProvider/visualize</c>; /info keeps the legacy type.
    /// </summary>
    Geometry = 1 << 20,
    /// <summary>
    /// The Code Tree picture (Lossless Data Compression): a Huffman tree growing bottom-up from the characters, merge by
    /// merge, its 0 and 1 edges, each character's code and the encoded bits; a static payload plus one frame of named states
    /// per solver step. Like <see cref="Graph"/> it is reported only as the <c>type</c> of the <c>format=frames</c> response
    /// of <c>/ProblemProvider/visualize</c>; /info keeps the legacy type.
    /// </summary>
    CodeTree = 1 << 21,
    /// <summary>
    /// The Quantum Circuit picture (Deutsch, Deutsch-Jozsa, Bernstein-Vazirani, Simon's Problem, Unstructured Search, Prime
    /// Factorization): one wire per qubit, gate columns in the order they run with the oracle as a box, the measured qubits,
    /// and per step the column applied, the classical oracle calls and the outcome probabilities; a static payload plus one
    /// frame of named states per solver step. Like <see cref="Graph"/> it is reported only as the <c>type</c> of the
    /// <c>format=frames</c> response of <c>/ProblemProvider/visualize</c>; /info keeps the legacy type
    /// (<see cref="QuantumCircuitD3"/>, <see cref="QuantumCircuitQjs"/>).
    /// </summary>
    QuantumCircuit = 1 << 22,
    /// <summary>
    /// The Search Tree picture, a second view of a backtracking or branch-and-bound solver: the tree of choices it made,
    /// growing step by step, with cut branches, backed-out branches and the answer's path; a static payload plus one frame
    /// per solver step. Like <see cref="Graph"/> it is reported only as the <c>type</c> of the <c>format=frames</c> response
    /// of <c>/ProblemProvider/visualize</c>; /info keeps the legacy type.
    /// </summary>
    SearchTree = 1 << 23,
    /// <summary>
    /// The Formula picture (SAT, 3SAT), a second view next to the Grouped Graph: the CNF formula as it is written, clause
    /// boxes joined by ∧ and literals by ∨, with an assignment strip of the variables (#925). The Grouped Graph's spec, ids and
    /// states, plus the state of each variable; a static payload plus one frame of named states per solver step. Like
    /// <see cref="Graph"/> it is reported only as the <c>type</c> of the <c>format=frames</c> response of
    /// <c>/ProblemProvider/visualize</c>; /info keeps the legacy <see cref="BooleanSatisfiability"/>.
    /// </summary>
    Formula = 1 << 24,
    /// <summary>
    /// The Nested Set picture (Set Cover, Exact Cover, Exact Hitting Set), a second view next to the Bipartite Graph: every
    /// element a dot and every set a region around its own elements, or a list of the sets when the regions would not read
    /// (#926). The Bipartite Graph's spec, ids and states without its edges; a static payload plus one frame of named states per
    /// solver step. Like <see cref="Graph"/> it is reported only as the <c>type</c> of the <c>format=frames</c> response of
    /// <c>/ProblemProvider/visualize</c>; /info keeps the legacy <see cref="SetD3"/>.
    /// </summary>
    NestedSet = 1 << 25,
}
