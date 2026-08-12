using Xunit;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_MAXCUT;
using API.Problems.NPComplete.NPC_MAXCUT.Solvers;
using API.Problems.NPComplete.NPC_MAXCUT.Verifiers;
using API.Problems.NPComplete.NPC_MAXCUT.Visualizations;

namespace redux_tests;
#pragma warning disable CS1591

public class MAXCUT_Tests {
        // ----- Instantiation ----- //

        [Fact]
        public void MAXCUT_Default_Instantiation() {
                MAXCUT problem = new MAXCUT();
                Assert.Equal(MAXCUT._defaultInstance, problem.instance);
                Assert.Equal(MAXCUT._defaultInstance, problem.defaultInstance);
                Assert.Equal(5, problem.nodes.Count);
                Assert.Equal(6, problem.edges.Count);
        }

        [Fact]
        public void MAXCUT_Custom_Instantiation() {
                string instance = "({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})";
                MAXCUT problem = new MAXCUT(instance);
                Assert.Equal(instance, problem.instance);
                Assert.Equal(3, problem.nodes.Count);
                Assert.Equal(3, problem.edges.Count);
        }

        [Fact]
        public void MAXCUT_Two_Node_Graph() {
                string instance = "({1,2},{({1,2},7)})";
                MAXCUT problem = new MAXCUT(instance);
                Assert.Equal(2, problem.nodes.Count);
                Assert.Single(problem.edges);
        }

        // ----- Solver ----- //

        [Theory]
        [InlineData("({1,2,3,4,5},{({2,1},5),({1,3},4),({2,3},2),({3,5},1),({2,4},4),({4,5},2)})", 15)]
        [InlineData("({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})", 5)]
        [InlineData("({1,2},{({1,2},7)})", 7)]
        public void MAXCUT_Solver_Returns_Correct_Weight(string instance, int expectedWeight) {
                MAXCUT problem = new MAXCUT(instance);
                MaxCutSolver solver = new MaxCutSolver();
                string solution = solver.solve(problem);
                int actualWeight = ComputeCutWeight(problem, solution);
                Assert.Equal(expectedWeight, actualWeight);
        }

        [Fact]
        public void MAXCUT_Solver_Default_Instance_Weight_Is_15() {
                MAXCUT problem = new MAXCUT();
                string solution = new MaxCutSolver().solve(problem);
                Assert.Equal(15, ComputeCutWeight(problem, solution));
        }

        [Fact]
        public void MAXCUT_Solver_Single_Node_Returns_Empty() {
                string instance = "({1},{})";
                MAXCUT problem = new MAXCUT(instance);
                string solution = new MaxCutSolver().solve(problem);
                Assert.Equal("{}", solution);
        }

        [Fact]
        public void MAXCUT_Solver_Output_Passes_Verifier() {
                string[] instances = {
            MAXCUT._defaultInstance,
            "({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})",
            "({1,2},{({1,2},7)})",
            "({A,B,C,D},{({A,B},4),({B,C},3),({C,D},4),({D,A},3)})"
        };
                foreach (string inst in instances) {
                        MAXCUT problem = new MAXCUT(inst);
                        string solution = new MaxCutSolver().solve(problem);
                        Assert.True(new MaxCutVerifier().verify(problem, solution), $"Solver output failed verifier for: {inst}");
                }
        }

        // ----- Verifier ----- //

        [Theory]
        // Default 5-node graph: S={1,4} gives cut weight 15 = max cut.
        [InlineData("({1,2,3,4,5},{({2,1},5),({1,3},4),({2,3},2),({3,5},1),({2,4},4),({4,5},2)})", "{1,4}", true)]
        // Complement S={2,3,5} also gives weight 15.
        [InlineData("({1,2,3,4,5},{({2,1},5),({1,3},4),({2,3},2),({3,5},1),({2,4},4),({4,5},2)})", "{2,3,5}", true)]
        // 3-node: max cut = 5, S={3} gives (1-3,3)+(2-3,2)=5.
        [InlineData("({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})", "{3}", true)]
        // S={1,2} also gives weight 5 for 3-node graph.
        [InlineData("({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})", "{1,2}", true)]
        // 2-node: max cut = 7.
        [InlineData("({1,2},{({1,2},7)})", "{1}", true)]
        public void MAXCUT_Verifier_Accepts_Valid_Maximum_Cut(string instance, string certificate, bool expected) {
                MAXCUT problem = new MAXCUT(instance);
                MaxCutVerifier verifier = new MaxCutVerifier();
                Assert.Equal(expected, verifier.verify(problem, certificate));
        }

        [Theory]
        // S={1,2} gives cut weight 10, not max cut (15).
        [InlineData("({1,2,3,4,5},{({2,1},5),({1,3},4),({2,3},2),({3,5},1),({2,4},4),({4,5},2)})", "{1,2}")]
        // S={2} for 3-node graph gives weight 3, not max cut (5).
        [InlineData("({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})", "{2}")]
        public void MAXCUT_Verifier_Rejects_Non_Maximum_Cut(string instance, string certificate) {
                MAXCUT problem = new MAXCUT(instance);
                MaxCutVerifier verifier = new MaxCutVerifier();
                Assert.False(verifier.verify(problem, certificate));
        }

        [Fact]
        public void MAXCUT_Verifier_Rejects_Node_Not_In_Graph() {
                MAXCUT problem = new MAXCUT("({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})");
                MaxCutVerifier verifier = new MaxCutVerifier();
                Assert.False(verifier.verify(problem, "{99}"));
        }

        [Fact]
        public void MAXCUT_Verifier_Rejects_Full_Node_Set() {
                MAXCUT problem = new MAXCUT("({1,2},{({1,2},7)})");
                MaxCutVerifier verifier = new MaxCutVerifier();
                Assert.False(verifier.verify(problem, "{1,2}"));
        }

        [Fact]
        public void MAXCUT_Verifier_Rejects_Empty_Certificate() {
                MAXCUT problem = new MAXCUT();
                MaxCutVerifier verifier = new MaxCutVerifier();
                Assert.False(verifier.verify(problem, "{}"));
        }

        // ----- Visualization ----- //

        [Fact]
        public void MAXCUT_Visualization_Returns_Graph() {
                MAXCUT problem = new MAXCUT();
                MaxCutVisualization viz = new MaxCutVisualization();
                var graph = (API_GraphJSON)viz.visualize(problem);
                Assert.Equal(5, graph.nodes.Count);
                Assert.Equal(6, graph.links.Count);
        }

        [Fact]
        public void MAXCUT_SolvedVisualization_Colors_Crossing_Edges() {
                // ({1,2,3},{({1,2},1),({2,3},2),({1,3},3)}), S={3}: crossing edges are (1-3) and (2-3).
                string instance = "({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})";
                MAXCUT problem = new MAXCUT(instance);
                MaxCutVisualization viz = new MaxCutVisualization();
                var graph = (API_GraphJSON)viz.SolvedVisualization(problem, "{3}");

                var link23 = graph.links.FirstOrDefault(l =>
                    (l.source == "2" && l.target == "3") || (l.source == "3" && l.target == "2"));
                Assert.NotNull(link23);
                Assert.Equal("Solution", link23!.color);

                // Edge (1-2) is within T and should not be colored.
                var link12 = graph.links.FirstOrDefault(l =>
                    (l.source == "1" && l.target == "2") || (l.source == "2" && l.target == "1"));
                Assert.NotNull(link12);
                Assert.NotEqual("Solution", link12!.color);
        }

        [Fact]
        public void MAXCUT_SolvedVisualization_Colors_S_Nodes() {
                string instance = "({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})";
                MAXCUT problem = new MAXCUT(instance);
                MaxCutVisualization viz = new MaxCutVisualization();
                var graph = (API_GraphJSON)viz.SolvedVisualization(problem, "{3}");

                var node3 = graph.nodes.FirstOrDefault(n => n.name == "3");
                Assert.NotNull(node3);
                Assert.Equal("Solution", node3!.color);
        }

        [Fact]
        public void MAXCUT_SolvedVisualization_Empty_Solution_Returns_Plain_Graph() {
                MAXCUT problem = new MAXCUT();
                MaxCutVisualization viz = new MaxCutVisualization();
                var graph = (API_GraphJSON)viz.SolvedVisualization(problem, "{}");
                Assert.All(graph.nodes, n => Assert.NotEqual("Solution", n.color));
                Assert.All(graph.links, l => Assert.NotEqual("Solution", l.color));
        }

        // ----- Helper ----- //

        private static int ComputeCutWeight(MAXCUT problem, string certificate) {
                if (string.IsNullOrWhiteSpace(certificate) || certificate.Trim() == "{}") return 0;

                HashSet<string> sNodes = certificate.Trim()
                    .TrimStart('{').TrimEnd('}')
                    .Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToHashSet();

                return MaxCutSolver.CutWeight(problem.edges, sNodes);
        }
}
