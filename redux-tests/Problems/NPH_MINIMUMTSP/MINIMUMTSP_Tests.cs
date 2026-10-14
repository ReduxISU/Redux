using Xunit;
using API.Interfaces;
using API.Problems.NPComplete.NPC_TSP;
using API.Problems.NPHard.NPH_MINIMUMTSP;
using API.Problems.NPHard.NPH_MINIMUMTSP.Solvers;
using API.Problems.NPHard.NPH_MINIMUMTSP.Verifiers;
namespace redux_tests;
#pragma warning disable CS1591

public class MINIMUMTSP_Tests {

    private const string DefaultInstance =
        "({a,b,c,d,e},{({a,b},3),({a,c},4),({a,d},2),({a,e},7),({b,c},4),({b,d},6),({b,e},3),({c,d},5),({c,e},8),({d,e},6)})";

    [Fact]
    public void defaultInstance_Test() {
        MINIMUMTSP problem = new MINIMUMTSP();
        Assert.Equal(DefaultInstance, problem.defaultInstance);
        Assert.Equal(new[] { "a", "b", "c", "d", "e" }, problem.nodes);
        Assert.Equal(10, problem.edges.Count);
        Assert.Equal(("a", "b", 3), problem.edges[0]);
    }

    [Fact]
    public void MINIMUMTSP_Declares_Formats() {
        MINIMUMTSP problem = new MINIMUMTSP();
        Assert.False(string.IsNullOrWhiteSpace(problem.instanceFormat));
        Assert.False(string.IsNullOrWhiteSpace(problem.certificateFormat));
        Assert.Contains(DefaultInstance, problem.instanceFormat);
    }

    [Fact]
    public void MINIMUMTSP_Certificate_Format_Example_Is_The_Default_Answer() {
        // The example quoted in certificateFormat is the default instance's cheapest tour, and the verifier accepts it.
        MINIMUMTSP problem = new MINIMUMTSP();
        Assert.True(problem.defaultVerifier.verify(problem, MinimumTSPVerifier.CertificateExample));
        Assert.Equal(MinimumTSPVerifier.CertificateExample, problem.defaultSolver.solve(problem));
    }

    [Fact]
    public void MINIMUMTSP_Reads_The_Same_Weighted_Graph_As_TSP() {
        // Same grammar as TSP without K, read by TSP's own edge reader.
        TSP tsp = new TSP("(" + DefaultInstance + ",100)");
        MINIMUMTSP problem = new MINIMUMTSP(DefaultInstance);
        Assert.Equal(tsp.nodes, problem.nodes);
        Assert.Equal(tsp.edges, problem.edges);
    }

    [Fact]
    public void MINIMUMTSP_Weight_Is_Undirected_And_Null_Without_An_Edge() {
        MINIMUMTSP problem = new MINIMUMTSP("({a,b,c},{({a,b},5),({b,c},7)})");
        Assert.Equal(5, problem.Weight("a", "b"));
        Assert.Equal(5, problem.Weight("b", "a"));
        Assert.Null(problem.Weight("a", "c"));
        Assert.Null(problem.Weight("a", "a"));
    }

    // -------------------------------------------------------------------------
    // Constructor — invalid instances (all must throw ProblemParseException)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("")]                                              // empty
    [InlineData("   ")]                                           // whitespace only
    [InlineData("abc")]                                           // bare string
    [InlineData("(({a,b,c},{({a,b},1),({b,c},1),({a,c},1)}),5)")] // TSP's instance, with K
    [InlineData("({a,b,c},{{a,b},{b,c}})")]                       // unweighted edges
    [InlineData("({a,b,c},{({a,b},x)})")]                         // non-integer weight
    [InlineData("({a,b},{({a,z},1)})")]                           // edge to a node not in the graph
    [InlineData("({a,b,c},{({a,b},1)")]                           // unbalanced / truncated
    public void MINIMUMTSP_Constructor_Throws_On_Invalid_Instance(string instance) {
        Assert.Throws<ProblemParseException>(() => new MINIMUMTSP(instance));
    }

    [Fact]
    public void MINIMUMTSP_Refuses_A_Loop_And_Names_It() {
        ProblemParseException e = Assert.Throws<ProblemParseException>(() => new MINIMUMTSP("({a,b},{({a,b},1),({a,a},3)})"));
        Assert.Contains("edge {a,a} joins a node to itself; Minimum Traveling Salesperson has no loops", e.Message);
    }

    // -------------------------------------------------------------------------
    // Verifier — validity only
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(DefaultInstance, "{a,c,b,e,d,a}")]   // the cheapest tour (19)
    [InlineData(DefaultInstance, "{a,c,b,e,d}")]     // the same, without the closing repeat
    [InlineData(DefaultInstance, "{d,e,b,c,a,d}")]   // the same cycle from another start, reversed
    [InlineData(DefaultInstance, "{a,b,c,d,e,a}")]   // a valid tour that is not the cheapest (25): validity only
    [InlineData(DefaultInstance, "{a, b, c, d, e}")] // spaces around names
    [InlineData("({a,b,c},{({a,b},1),({b,c},1),({a,c},1)})", "{a,b,c,a}")]
    public void MINIMUMTSP_Verifier_Accepts_Tours(string instance, string certificate) {
        MINIMUMTSP problem = new MINIMUMTSP(instance);
        Assert.True(problem.defaultVerifier.verify(problem, certificate));
    }

    [Theory]
    [InlineData(DefaultInstance, "{}")]                // the solvers' "no tour" answer is not a tour
    [InlineData(DefaultInstance, "{a,b,c,d,a}")]       // skips e
    [InlineData(DefaultInstance, "{a,b,c,d,d,e,a}")]   // repeats d
    [InlineData(DefaultInstance, "{a,b,c,d,b,a}")]     // repeats b in place of e
    [InlineData(DefaultInstance, "{a,b,c,d,z,a}")]     // z is not a city
    [InlineData(DefaultInstance, "{a,b,c,d,e,b}")]     // ends somewhere other than its start: lists b twice
    [InlineData("({a,b,c,d},{({a,b},1),({b,c},1),({c,d},1),({a,c},1)})", "{a,b,c,d,a}")] // no edge d-a back to the start
    [InlineData("({a,b,c,d},{({a,b},1),({b,c},1),({c,d},1),({d,a},1)})", "{a,c,b,d,a}")] // no edge a-c
    [InlineData("({a,b},{({a,b},1)})", "{a,b,a}")]     // two cities: going there and back is not a tour
    [InlineData("({a},{})", "{a}")]                    // one city
    public void MINIMUMTSP_Verifier_Rejects_Non_Tours(string instance, string certificate) {
        MINIMUMTSP problem = new MINIMUMTSP(instance);
        Assert.False(problem.defaultVerifier.verify(problem, certificate));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{a,,b,c,d,e}")]
    public void MINIMUMTSP_Verifier_Throws_On_Malformed_Certificate(string certificate) {
        MINIMUMTSP problem = new MINIMUMTSP();
        Assert.Throws<CertificateParseException>(() => problem.defaultVerifier.verify(problem, certificate));
    }

    [Fact]
    public void MINIMUMTSP_Verifier_Says_It_Checks_Validity_Not_Optimality() {
        MinimumTSPVerifier verifier = new MinimumTSPVerifier();
        Assert.Equal("Default Minimum Traveling Salesperson Verifier", verifier.verifierName);
        Assert.Contains("valid tour", verifier.verifierDefinition);
        Assert.Contains("does not check that the tour is the cheapest", verifier.verifierDefinition);
    }

    // -------------------------------------------------------------------------
    // BruteForceMinimumTSP — optimality on hand-checked graphs where the first
    // tour the solver finds is not the cheapest
    // -------------------------------------------------------------------------

    // Each row: instance, the cost of the first tour the solver meets (fixed first city, the rest in lexicographic
    // order), the cheapest tour (the first of the two directions it is met in), and its cost. Costs are worked by hand
    // in the comments.
    public static TheoryData<string, long, string, long> OptimalityCases => new() {
        // Default: 12 distinct tours of K5. First a,b,c,d,e,a = 3+4+5+6+7 = 25; then 23 (a,b,c,e,d,a), 21 (a,b,e,c,d,a);
        // cheapest a,c,b,e,d,a = 4+4+3+6+2 = 19, the only tour of 19 (reversed: a,d,e,b,c,a).
        { DefaultInstance, 25, "{a,c,b,e,d,a}", 19 },
        // Square a-b-c-d with heavy sides b-c, d-a (10) and light diagonals a-c, b-d (2). First a,b,c,d,a = 1+10+1+10
        // = 22; a,b,d,c,a = 1+2+1+2 = 6; a,c,b,d,a = 2+10+2+10 = 24. Cheapest 6.
        { "({a,b,c,d},{({a,b},1),({b,c},10),({c,d},1),({a,d},10),({a,c},2),({b,d},2)})", 22, "{a,b,d,c,a}", 6 },
        // The cheapest of K4's three tours is the last one met: a,b,c,d,a = 10+1+10+1 = 22, a,b,d,c,a = 10+1+10+1 = 22,
        // a,c,b,d,a = 1+1+1+1 = 4.
        { "({a,b,c,d},{({a,b},10),({c,d},10),({a,c},1),({b,d},1),({b,c},1),({a,d},1)})", 22, "{a,c,b,d,a}", 4 },
        // Not complete: edges a-b 6, b-c 1, a-c 9, a-e 5, b-d 2, b-e 8, c-e 2, d-e 3, a-d 4, and no c-d, so the first
        // ordering a,b,c,d,e,a is not a tour. First tour a,b,c,e,d,a = 6+1+2+3+4
        // = 16; cheapest a,d,b,c,e,a = 4+2+1+2+5 = 14 (also 20, 22, 25 among the rest).
        { "({a,b,c,d,e},{({a,b},6),({b,c},1),({a,c},9),({a,e},5),({b,d},2),({b,e},8),({c,e},2),({d,e},3),({a,d},4)})", 16, "{a,d,b,c,e,a}", 14 },
    };

    [Theory]
    [MemberData(nameof(OptimalityCases))]
    public void BruteForceMinimumTSP_Returns_The_Cheapest_Tour_Not_The_First(string instance, long firstCost, string cheapest, long cheapestCost) {
        MINIMUMTSP problem = new MINIMUMTSP(instance);
        string answer = new BruteForceMinimumTSP().solve(problem);

        Assert.Equal(cheapest, answer);
        Assert.True(problem.defaultVerifier.verify(problem, answer));
        Assert.Equal(cheapestCost, MinimumTSPVerifier.TourCost(problem, MinimumTSPVerifier.ReadCities(problem, answer)));
        Assert.True(firstCost > cheapestCost, "the case must have a first-found tour that is not the cheapest");
        Assert.Equal(firstCost, FirstTourCost(problem));
        Assert.Equal(cheapestCost, CheapestByEnumeration(problem));
    }

    [Theory]
    [InlineData("({},{})")]                                      // no cities
    [InlineData("({a},{})")]                                     // one city
    [InlineData("({a,b},{({a,b},1)})")]                          // two cities: no cycle through both
    [InlineData("({a,b,c},{({a,b},1),({b,c},1)})")]              // a path, no edge back
    [InlineData("({a,b,c,d},{({a,b},1),({b,c},1),({c,a},1),({c,d},1)})")] // d hangs off c
    public void BruteForceMinimumTSP_Returns_Empty_When_There_Is_No_Tour(string instance) {
        MINIMUMTSP problem = new MINIMUMTSP(instance);
        Assert.Equal("{}", new BruteForceMinimumTSP().solve(problem));
    }

    [Fact]
    public void BruteForceMinimumTSP_Triangle_Has_One_Tour() {
        MINIMUMTSP problem = new MINIMUMTSP("({x,y,z},{({x,y},2),({y,z},3),({x,z},4)})");
        Assert.Equal("{x,y,z,x}", new BruteForceMinimumTSP().solve(problem));
    }

    [Fact]
    public void BruteForceMinimumTSP_Keeps_The_First_Of_Equally_Cheap_Tours() {
        // Every tour of a K4 with equal weights costs 4; the first one met is kept.
        MINIMUMTSP problem = new MINIMUMTSP("({a,b,c,d},{({a,b},1),({a,c},1),({a,d},1),({b,c},1),({b,d},1),({c,d},1)})");
        Assert.Equal("{a,b,c,d,a}", new BruteForceMinimumTSP().solve(problem));
    }

    [Fact]
    public void BruteForceMinimumTSP_Matches_TSP_At_The_Optimum() {
        // The cheapest tour's cost is the smallest K for which TSP's answer is yes.
        MINIMUMTSP problem = new MINIMUMTSP();
        TSP atOptimum = new TSP("(" + DefaultInstance + ",19)");
        TSP belowOptimum = new TSP("(" + DefaultInstance + ",18)");
        string answer = problem.defaultSolver.solve(problem);
        Assert.True(atOptimum.defaultVerifier.verify(atOptimum, answer));
        Assert.Equal("{}", belowOptimum.defaultSolver.solve(belowOptimum));
    }

    // The cost of the first ordering (first city fixed, the rest in lexicographic order) that is a tour.
    private static long FirstTourCost(MINIMUMTSP problem) {
        int[] order = Enumerable.Range(0, problem.nodes.Count).ToArray();
        var weight = BruteForceMinimumTSP.WeightTable(problem);
        do {
            if (BruteForceMinimumTSP.Cost(weight, order) is long cost) return cost;
        } while (BruteForceMinimumTSP.NextPermutation(order, 1));
        throw new InvalidOperationException("no tour");
    }

    // An independent minimum over every permutation of every city, through the verifier's own cost.
    private static long CheapestByEnumeration(MINIMUMTSP problem) {
        long best = long.MaxValue;
        foreach (var perm in Permutations(problem.nodes)) {
            if (MinimumTSPVerifier.TourCost(problem, perm) is long cost && cost < best) best = cost;
        }
        return best;
    }

    private static IEnumerable<List<string>> Permutations(List<string> items) {
        if (items.Count == 0) { yield return new List<string>(); yield break; }
        for (int i = 0; i < items.Count; i++) {
            var rest = items.Where((_, j) => j != i).ToList();
            foreach (var tail in Permutations(rest)) {
                tail.Insert(0, items[i]);
                yield return tail;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Parallel edges (#769): the lightest edge between two cities is the one a tour uses
    // -------------------------------------------------------------------------

    // a and b are joined twice (10, then 1). With the lightest, a-b-c-d-a costs 4 and every other tour 12. Before #769 the
    // constructor rejected the instance ("more than one edge joins a and b").
    private const string ParallelInstance =
        "({a,b,c,d},{({a,b},10),({a,b},1),({b,c},1),({c,d},1),({d,a},1),({a,c},5),({b,d},5)})";

    [Fact]
    public void MINIMUMTSP_Parallel_Edges_Merge_Into_The_Lightest() {
        MINIMUMTSP problem = new MINIMUMTSP(ParallelInstance);
        Assert.Equal(6, problem.edges.Count);
        Assert.Equal(7, problem.listedEdges.Count);
        Assert.Equal(1, problem.Weight("a", "b"));
        Assert.Equal(1, problem.Weight("b", "a"));
    }

    [Fact]
    public void MINIMUMTSP_Parallel_Edges_Solver_And_Verifier_Use_The_Lightest() {
        MINIMUMTSP problem = new MINIMUMTSP(ParallelInstance);
        string answer = new BruteForceMinimumTSP().solve(problem);
        Assert.True(problem.defaultVerifier.verify(problem, answer));
        Assert.Equal(4, MinimumTSPVerifier.TourCost(problem, MinimumTSPVerifier.ReadCities(problem, answer)));
    }

    [Fact]
    public void MINIMUMTSP_Parallel_Edges_Written_Either_Way_Round_Merge() {
        // {b,a} names the same pair as {a,b}.
        MINIMUMTSP problem = new MINIMUMTSP("({a,b,c},{({a,b},3),({b,a},2),({b,c},1),({a,c},1)})");
        Assert.Equal(2, problem.Weight("a", "b"));
    }

    [Fact]
    public void MINIMUMTSP_Reversed_Copy_Of_An_Edge_Is_Listed_Once() {
        // ({a,b},5) and ({b,a},5) are the same edge written twice, so the picture must not label the pair "5,5".
        MINIMUMTSP problem = new MINIMUMTSP("({a,b,c},{({a,b},5),({b,a},5),({b,c},1),({a,c},1)})");
        Assert.Equal(3, problem.listedEdges.Count);
        Assert.Equal(3, problem.edges.Count);
    }
}
