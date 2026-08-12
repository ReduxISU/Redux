using API.Interfaces;
using API.Problems.NPComplete.NPC_CLIQUE.Solvers;
using API.Problems.NPComplete.NPC_CLIQUE.Verifiers;
using API.Problems.NPComplete.NPC_CLIQUE.Visualizations;
using SPADE;

namespace API.Problems.NPComplete.NPC_CLIQUE;

class CLIQUE : IGraphProblem<CliqueBruteForce, CliqueVerifier, CliqueDefaultVisualization, UtilCollectionGraph> {

        // --- Fields ---
        public string problemName { get; } = "Clique";
        public string problemLink { get; } = "https://en.wikipedia.org/wiki/Clique";
        public string formalDefinition { get; } = "Clique = {<G, k> | G is an graph that has a set of k mutually adjacent nodes}";
        public string problemDefinition { get; } = "A clique is the problem of uncovering a subset of vertices in an undirected graph G = (V, E) such that every two distinct vertices are adjacent";
        public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
        public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
        public const string InstanceGrammar = "{((N,E),K) | N is set, E subset N unorderedcross N, K is int}";
        private static string _defaultInstance = "(({1,2,3,4,5,6},{{4,1},{1,2},{4,3},{3,2},{2,4},{5,2},{3,5},{5,4},{3,6},{6,4},{1,6}}),4)";
        public string defaultInstance { get; } = _defaultInstance;
        public string instanceFormat { get; } = $"Format: {InstanceGrammar} Example: {_defaultInstance}";
        public string certificateFormat { get; } =
            $"Format: {CliqueVerifier.CertificateGrammar} Example: {CliqueVerifier.CertificateExample}";
        public string instance { get; set; } = string.Empty;
        public string wikiName { get; } = "";
        private List<string> _nodes = new List<string>();
        private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
        private int _K;
        public CliqueBruteForce defaultSolver { get; } = new CliqueBruteForce();
        public CliqueVerifier defaultVerifier { get; } = new CliqueVerifier();
        public CliqueDefaultVisualization defaultVisualization { get; } = new CliqueDefaultVisualization();
        public UtilCollectionGraph graph { get; set; }
        public string[] contributors { get; } = { "Kaden Marchetti", "Alex Diviney" };
        // Declared, not derived. CLIQUE is NP-complete (Karp, 1972).
        public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;

        // --- Properties ---
        public List<string> nodes {
                get {
                        return _nodes;
                }
                set {
                        _nodes = value;
                }
        }
        public List<KeyValuePair<string, string>> edges {
                get {
                        return _edges;
                }
                set {
                        _edges = value;
                }
        }

        public int K {
                get {
                        return _K;
                }
                set {
                        _K = value;
                }
        }

        // --- Methods Including Constructors ---
        public CLIQUE() : this(_defaultInstance) {

        }
        public CLIQUE(string GInput) {
                if (string.IsNullOrWhiteSpace(GInput)) {
                        throw new ProblemParseException("CLIQUE", GInput, "instance is empty");
                }

                instance = GInput;
                StringParser cliqueGraph = new(InstanceGrammar);
                try {
                        cliqueGraph.parse(GInput);
                        nodes = cliqueGraph["N"].ToList().Select(node => node.ToString()).ToList();
                        edges = cliqueGraph["E"].ToList().Select(edge => {
                                List<UtilCollection> cast = edge.ToList();
                                return new KeyValuePair<string, string>(cast[0].ToString(), cast[1].ToString());
                        }).ToList();
                        _K = int.Parse(cliqueGraph["K"].ToString());

                        graph = new UtilCollectionGraph(cliqueGraph["N"], cliqueGraph["E"]);
                }
                catch (Exception ex) when (ex is not ProblemParseException) {
                        throw new ProblemParseException("CLIQUE", GInput, ex.Message);
                }
        }

}