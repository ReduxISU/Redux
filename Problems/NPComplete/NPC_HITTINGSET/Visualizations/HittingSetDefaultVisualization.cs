using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using SPADE;
using API.Problems.NPComplete.NPC_HITTINGSET.Solvers;
namespace API.Problems.NPComplete.NPC_HITTINGSET.Visualizations;

class HittingSetDefaultVisualization : IVisualization<HITTINGSET> {
        public string visualizationName { get; } = "Hitting Set Visualization";
        public string visualizationDefinition { get; } = "This is a default visualization for Hitting Set";
        public string source { get; } = "";
        public string[] contributors { get; } = { "Andrija Sevaljevic" };
        public VisualizationType visualizationType { get; } = VisualizationType.SetD3;
        public ISolver solver { get; } = new HittingSetBruteForce();

        // --- Methods Including Constructors ---
        public HittingSetDefaultVisualization() {

        }
        public API_JSON visualize(HITTINGSET hittingSet) {
                return new API_SET(new UtilCollection(hittingSet.instance));
        }
        public API_JSON SolvedVisualization(HITTINGSET hittingSet, string solution) {
                //return new API_SET(new UtilCollection(hittingSet.instance));
                API_SET ec = new API_SET(new UtilCollection(hittingSet.instance));

                return new API_SET(new UtilCollection(hittingSet.instance));
        }
}