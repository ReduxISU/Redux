// LabeledEdge.cs
// A directed edge whose endpoints and label are plain strings.
// Used by the DFA/NFA automaton model (WeightedDirectedGraph), where
// "value" holds the transition symbol(s), e.g. "a,b".
// Split out of WeightedEdge (Michael Trosper's 1/13/2026 DFA additions) so that
// the node-based WeightedEdge and this string-based edge are no longer one fused type.

namespace API.Interfaces.Graphs;

class LabeledEdge {
        private string _from;
        private string _to;
        private string _value;

        public LabeledEdge(string from, string to, string value) {
                _from = from;
                _to = to;
                _value = value;
        }

        public string from {
                get => _from;
                set => _from = value;
        }

        public string to {
                get => _to;
                set => _to = value;
        }

        public string value {
                get => _value;
                set => _value = value;
        }
}
