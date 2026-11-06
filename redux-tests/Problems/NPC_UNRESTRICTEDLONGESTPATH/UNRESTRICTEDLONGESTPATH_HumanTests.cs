using API.Interfaces;
using API.Problems.NPComplete.NPC_UNRESTRICTEDLONGESTPATH;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Human-generated tests for the Unrestricted Longest Path Problem.
// These check behavior from the problem definition: does graph G have a simple path
// with at least K edges? No path endpoints are given, and isolated vertices are allowed.
public class UNRESTRICTEDLONGESTPATH_HumanTests {

    // The solver uses randomized color coding, so a YES answer can occasionally be
    // missed on one run. Retrying keeps the tests from failing by bad luck.
    private static string SolveWithRetries(UNRESTRICTEDLONGESTPATH problem) {
        string answer = "{}";
        for (int attempt = 0; attempt < 10 && answer == "{}"; attempt++) {
            answer = problem.defaultSolver.solve(problem);
        }
        return answer;
    }

    // ---------- Parsing ----------

    [Fact]
    public void DefaultInstance_HasSixVerticesEightEdgesAndKOfFive() {
        var problem = new UNRESTRICTEDLONGESTPATH();
        Assert.Equal(6, problem.nodes.Count);
        Assert.Equal(8, problem.edges.Count);
        Assert.Equal(5, problem.K);
    }

    [Theory]
    [InlineData("(({a,b,c},{{a,b},{b,c}}),2)", 3, 2, 2)]   // simple path graph
    [InlineData("(({1,2,3,4},{{1,2}}),1)", 4, 1, 1)]       // isolated vertices are kept
    [InlineData("(({x,y},{}),1)", 2, 0, 1)]                // no edges at all
    [InlineData("(({a,b,c},{{a,b},{b,c},{a,c}}),2)", 3, 3, 2)] // triangle
    [InlineData("(({a},{}),0)", 1, 0, 0)]                  // single vertex, K = 0
    [InlineData(" ( ( { a , b } , { { a , b } } ) , 1 ) ", 2, 1, 1)] // extra spaces
    public void ValidInstances_ParseToExpectedSizes(string input, int nodeCount, int edgeCount, int k) {
        var problem = new UNRESTRICTEDLONGESTPATH(input);
        Assert.Equal(nodeCount, problem.nodes.Count);
        Assert.Equal(edgeCount, problem.edges.Count);
        Assert.Equal(k, problem.K);
    }

    [Theory]
    [InlineData("")]                          // empty
    [InlineData("not an instance")]           // garbage
    [InlineData("(({a,b},{{a,b}}),-1)")]      // negative K
    [InlineData("(({a,b},{{a,b}}),two)")]     // K is not a number
    [InlineData("(({a,b},{{a,a}}),1)")]       // self-loop
    [InlineData("(({a,b},{{a,c}}),1)")]       // edge uses a vertex that is not declared
    [InlineData("(({a,a},{}),0)")]            // duplicate vertex
    [InlineData("(({a,b},{{a,b}}),1) extra")] // trailing text
    public void InvalidInstances_AreRejected(string input) {
        Assert.ThrowsAny<Exception>(() => new UNRESTRICTEDLONGESTPATH(input));
    }

    // ---------- Verifier ----------

    [Theory]
    [InlineData("(({a,b,c},{{a,b},{b,c}}),2)", "{a,b,c}", true)]            // whole path
    [InlineData("(({a,b,c},{{a,b},{b,c}}),2)", "{c,b,a}", true)]            // reversed path is also fine
    [InlineData("(({a,b,c},{{a,b},{b,c}}),2)", "{a,b}", false)]              // too short for K = 2
    [InlineData("(({a,b,c},{{a,b},{b,c}}),1)", "{a,b}", true)]               // exactly K edges is enough
    [InlineData("(({a,b,c},{{a,b},{b,c}}),2)", "{a,c,b}", false)]            // a-c is not an edge
    [InlineData("(({a,b,c},{{a,b},{b,c},{a,c}}),2)", "{a,c,b}", true)]       // triangle, a different order
    [InlineData("(({a,b,c},{{a,b},{b,c},{a,c}}),3)", "{a,b,c,a}", false)]    // a vertex may not repeat
    [InlineData("(({a,b,c},{{a,b},{b,c}}),1)", "{a,z}", false)]              // z is not in the graph
    [InlineData("(({a},{}),0)", "{a}", true)]                                // K = 0 is satisfied by one vertex
    [InlineData("(({c,x,y,z},{{c,x},{c,y},{c,z}}),2)", "{x,c,y}", true)]     // star graph: two leaves through the center
    [InlineData("(({c,x,y,z},{{c,x},{c,y},{c,z}}),3)", "{x,c,y}", false)]    // star graph has no path with 3 edges
    public void Verifier_AcceptsAndRejectsExpectedPaths(string input, string certificate, bool expected) {
        var problem = new UNRESTRICTEDLONGESTPATH(input);
        Assert.Equal(expected, problem.defaultVerifier.verify(problem, certificate));
    }

    // ---------- Solver: NO instances are deterministic ----------

    [Theory]
    [InlineData("(({c,x,y,z},{{c,x},{c,y},{c,z}}),3)")]   // star: longest path has 2 edges
    [InlineData("(({a,b,c,d},{{a,b},{c,d}}),2)")]          // two separate edges, no path of 2 edges
    [InlineData("(({a,b,c},{}),1)")]                       // no edges
    [InlineData("(({a,b,c},{{a,b},{b,c}}),3)")]            // K needs more vertices than the graph has
    public void Solver_ReportsNoPathWhenNoneExists(string input) {
        var problem = new UNRESTRICTEDLONGESTPATH(input);
        Assert.Equal("{}", problem.defaultSolver.solve(problem));
    }

    // ---------- Solver: YES instances (answer must pass the verifier) ----------

    [Theory]
    [InlineData("(({a,b,c},{{a,b},{b,c}}),2)")]                                  // path graph
    [InlineData("(({a,b,c,d},{{a,b},{b,c},{c,d},{a,d}}),3)")]                    // 4-cycle
    [InlineData("(({a,b,c,d},{{a,b},{a,c},{a,d},{b,c},{b,d},{c,d}}),3)")]        // complete graph on 4 vertices
    [InlineData("(({a,b,c,d,e},{{a,b},{c,d},{d,e}}),2)")]                        // disconnected graph, long part is c-d-e
    [InlineData("(({a,b},{{a,b}}),1)")]                                          // smallest YES case
    public void Solver_FindsAPathThatTheVerifierAccepts(string input) {
        var problem = new UNRESTRICTEDLONGESTPATH(input);
        string answer = SolveWithRetries(problem);
        Assert.NotEqual("{}", answer);
        Assert.True(problem.defaultVerifier.verify(problem, answer));
    }

    [Fact]
    public void Solver_ReturnsASingleVertexWhenKIsZero() {
        var problem = new UNRESTRICTEDLONGESTPATH("(({a,b},{}),0)");
        string answer = problem.defaultSolver.solve(problem);
        Assert.True(problem.defaultVerifier.verify(problem, answer));
    }
}
