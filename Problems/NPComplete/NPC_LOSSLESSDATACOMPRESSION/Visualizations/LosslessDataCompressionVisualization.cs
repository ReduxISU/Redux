using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Solvers;

namespace API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Visualizations;

class LosslessDataCompressionVisualization : IVisualization<LOSSLESSDATACOMPRESSION> {
        public string visualizationName { get; } = "Lossless Data Compression Visualization";
        public string visualizationDefinition { get; } = "TODO";
        public string source { get; } = "";
        public string sourceLink { get; } = "TODO";
        public string[] contributors { get; } = { "TODO" };
        public VisualizationType visualizationType { get; } = VisualizationType.Unimplemented; //either "Boolean Satisfiability" or "Graph D3" most likely
        public ISolver solver { get; } = new LosslessDataCompressionSolver();

        // --- Methods Including Constructors ---
        public LosslessDataCompressionVisualization() {

        }
        public API_JSON visualize(LOSSLESSDATACOMPRESSION instance) {
                //TODO: implement visualization

                return new API_empty();
        }

        public API_JSON SolvedVisualization(LOSSLESSDATACOMPRESSION instance, string solution) {
                //TODO: implement SolvedVisualization (remove method if not implemented)

                return new API_empty();
        }
}