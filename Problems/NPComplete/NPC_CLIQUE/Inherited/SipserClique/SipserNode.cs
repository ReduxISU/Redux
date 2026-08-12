using API.Problems.NPComplete.NPC_CLIQUE;
namespace API.Problems.NPComplete.NPC_CLIQUE.Inherited;

class SipserNode : CliqueNode {

        // --- Fields ---


        // --- Methods Including Constructors ---
        protected string _solutionState;

        public SipserNode(string nodeName, string nodeCluster) : base(nodeName, nodeCluster) {
                _solutionState = "";
        }

        public SipserNode(string nodeName, string nodeCluster, string solState) {
                this._name = nodeName;
                this._cluster = nodeCluster;
                this._solutionState = solState;
        }


        public string solutionState {
                get {
                        return _solutionState;
                }
                set {
                        _solutionState = value;
                }
        }


}