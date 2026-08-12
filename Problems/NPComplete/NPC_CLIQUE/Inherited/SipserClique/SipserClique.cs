using API.Interfaces;
using API.Problems.NPComplete.NPC_CLIQUE.Solvers;
using API.Problems.NPComplete.NPC_CLIQUE.Verifiers;
using API.Problems.NPComplete.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_CLIQUE.Inherited;

namespace API.Problems.NPComplete.NPC_CLIQUE.Inherited;

class SipserClique : CLIQUE {

        // --- Fields ---
        // Adding cluster field to class
        private List<SipserNode> _clusterNodes = new List<SipserNode>();
        private int _numberOfClusters;
        public SipserClique() : base() {

        }
        public SipserClique(string Ginput) : base(Ginput) {
                foreach (string elem in this.nodes) {
                        //  Console.WriteLine("Node name: "+elem);
                        _clusterNodes.Add(new SipserNode(elem, "0"));
                }
                _numberOfClusters = 1;

        }

        public SipserClique(string Ginput, Dictionary<string, bool> solutionDict) : base(Ginput) {
                foreach (string elem in this.nodes) {
                        //  Console.WriteLine("Node name: "+elem + " Solution State: "+solutionDict[elem].ToString() );
                        _clusterNodes.Add(new SipserNode(elem, "0", solutionDict[elem].ToString()));
                }
                _numberOfClusters = 1;

        }



        // --- Properties ---
        public List<SipserNode> clusterNodes {
                get {
                        return _clusterNodes;
                }
                set {
                        _clusterNodes = value;
                }
        }
        public int numberOfClusters {
                get {
                        return _numberOfClusters;
                }
                set {
                        _numberOfClusters = value;
                }
        }
}