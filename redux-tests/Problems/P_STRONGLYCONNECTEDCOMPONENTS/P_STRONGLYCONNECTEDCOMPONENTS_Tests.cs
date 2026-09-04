#pragma warning disable CS1591
using Xunit;
using API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS;
using API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS.Solvers;
using API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS.Verifiers;

namespace redux_tests.Problems.P {
    public class SCC_Tests {
        [Fact]
        public void Kosaraju_Finds_Components_In_Demo_Graph() {
            string input = "({0,1,2,3,4,5,6,7},{(0,1),(1,2),(2,3),(2,0),(3,4),(4,5),(5,6),(6,4),(4,7),(6,7)})";

            var problem = new STRONGLYCONNECTEDCOMPONENTS(input);
            var solver = new KosarajuSolver();

            string result = solver.solve(problem);

            Assert.Contains("{0,1,2}", result);
            Assert.Contains("{3}", result);
            Assert.Contains("{4,5,6}", result);
            Assert.Contains("{7}", result);
        }

        [Fact]
        public void Kosaraju_No_Cycle_Each_Node_Is_Separate() {
            string input = "({1,2,3},{(1,2),(2,3)})";

            var problem = new STRONGLYCONNECTEDCOMPONENTS(input);
            var solver = new KosarajuSolver();

            string result = solver.solve(problem);

            Assert.Contains("{1}", result);
            Assert.Contains("{2}", result);
            Assert.Contains("{3}", result);
        }

        [Fact]
        public void Kosaraju_All_Nodes_In_One_Component() {
            string input = "({1,2,3},{(1,2),(2,3),(3,1)})";

            var problem = new STRONGLYCONNECTEDCOMPONENTS(input);
            var solver = new KosarajuSolver();

            string result = solver.solve(problem);

            Assert.Contains("{1,2,3}", result);
        }

        [Fact]
        public void Kosaraju_Single_Node_Graph() {
            string input = "({1},{})";

            var problem = new STRONGLYCONNECTEDCOMPONENTS(input);
            var solver = new KosarajuSolver();

            string result = solver.solve(problem);

            Assert.Contains("{1}", result);
        }

        [Fact]
        public void Verifier_Accepts_Correct_SCC_Solution() {
            string input = "({1,2,3,4,5},{(1,2),(2,3),(3,1),(3,4),(4,5),(5,4)})";
            string solution = "{{1,2,3},{4,5}}";

            var problem = new STRONGLYCONNECTEDCOMPONENTS(input);
            var verifier = new SCCVerifier();

            Assert.True(verifier.verify(problem, solution));
        }

        [Fact]
        public void Verifier_Accepts_Same_SCCs_Different_Order() {
            string input = "({1,2,3,4,5},{(1,2),(2,3),(3,1),(3,4),(4,5),(5,4)})";
            string solution = "{{4,5},{3,2,1}}";

            var problem = new STRONGLYCONNECTEDCOMPONENTS(input);
            var verifier = new SCCVerifier();

            Assert.True(verifier.verify(problem, solution));
        }

        [Fact]
        public void Verifier_Rejects_Wrong_SCC_Solution() {
            string input = "({1,2,3},{(1,2),(2,3)})";
            string wrongSolution = "{{1,2,3}}";

            var problem = new STRONGLYCONNECTEDCOMPONENTS(input);
            var verifier = new SCCVerifier();

            Assert.False(verifier.verify(problem, wrongSolution));
        }

        // -------------------------------------------------------------------------
        // Format declarations
        // -------------------------------------------------------------------------

        [Fact]
        public void STRONGLYCONNECTEDCOMPONENTS_Instance_Format_Described() {
            var problem = new STRONGLYCONNECTEDCOMPONENTS();
            Assert.NotNull(problem.instanceFormat);
            Assert.NotEmpty(problem.instanceFormat);
            Assert.Contains("N,E", problem.instanceFormat);
        }

        [Fact]
        public void STRONGLYCONNECTEDCOMPONENTS_Certificate_Format_Described() {
            var problem = new STRONGLYCONNECTEDCOMPONENTS();
            Assert.NotNull(problem.certificateFormat);
            Assert.NotEmpty(problem.certificateFormat);
            Assert.Contains("strongly connected component", problem.certificateFormat);
        }

        [Fact]
        public void STRONGLYCONNECTEDCOMPONENTS_Certificate_Format_Example_Is_Actually_Valid() {
            // The example quoted in certificateFormat must be a real, verifiable
            // certificate for defaultInstance — not just descriptive prose.
            var problem = new STRONGLYCONNECTEDCOMPONENTS();
            var verifier = new SCCVerifier();
            Assert.True(verifier.verify(problem, "{{1,2,3},{4,5}}"));
        }
    }
}