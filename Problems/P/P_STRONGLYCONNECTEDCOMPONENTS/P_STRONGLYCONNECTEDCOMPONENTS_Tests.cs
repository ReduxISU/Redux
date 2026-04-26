using Xunit;
using API.Problems.P.P_STRONGLYCONNECTEDCOMPONENTS;
using API.Problems.P.P_STRONGLYCONNECTEDCOMPONENTS.Solvers;

namespace redux_tests.Problems.P
{
    public class SCC_Tests
    {
        [Fact]
        public void Kosaraju_Finds_Components_In_Demo_Graph()
        {
            string input = "({0,1,2,3,4,5,6,7},{(0,1),(1,2),(2,3),(2,0),(3,4),(4,5),(5,6),(6,4),(4,7),(6,7)})";

            var problem = new P_STRONGLYCONNECTEDCOMPONENTS(input);
            var solver = new KosarajuSolver();

            string result = solver.solve(problem);

            Assert.Contains("{0,1,2}", result);
            Assert.Contains("{3}", result);
            Assert.Contains("{4,5,6}", result);
            Assert.Contains("{7}", result);
        }

        [Fact]
        public void Kosaraju_No_Cycle_Each_Node_Is_Separate()
        {
            string input = "({1,2,3},{(1,2),(2,3)})";

            var problem = new P_STRONGLYCONNECTEDCOMPONENTS(input);
            var solver = new KosarajuSolver();

            string result = solver.solve(problem);

            Assert.Contains("{1}", result);
            Assert.Contains("{2}", result);
            Assert.Contains("{3}", result);
        }

        [Fact]
        public void Kosaraju_All_Nodes_In_One_Component()
        {
            string input = "({1,2,3},{(1,2),(2,3),(3,1)})";

            var problem = new P_STRONGLYCONNECTEDCOMPONENTS(input);
            var solver = new KosarajuSolver();

            string result = solver.solve(problem);

            Assert.Contains("{1,2,3}", result);
        }
        [Fact]
public void Kosaraju_Single_Node_Graph()
{
    string input = "({1},{})";

    var problem = new P_STRONGLYCONNECTEDCOMPONENTS(input);
    var solver = new KosarajuSolver();

    string result = solver.solve(problem);

    Assert.Contains("{1}", result);
}
    }
    
}
