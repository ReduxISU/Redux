using Xunit;
using API.Interfaces.Graphs;

namespace redux_tests;
#pragma warning disable CS1591

// The shared parallel-edge rule (#769): cut problems add, path/tour/tree problems keep the lightest.
public class ParallelEdges_Tests {
    private static readonly List<(string, string, int)> Listed = new() {
        ("1", "2", 5), ("2", "3", 4), ("2", "1", 2), ("1", "2", 3),
    };

    [Fact]
    public void Lightest_Keeps_One_Edge_Per_Pair_With_The_Smallest_Weight() {
        Assert.Equal(new List<(string, string, int)> { ("1", "2", 2), ("2", "3", 4) }, ParallelEdges.Lightest(Listed));
    }

    [Fact]
    public void Summed_Keeps_One_Edge_Per_Pair_With_The_Total_Weight() {
        Assert.Equal(new List<(string, string, int)> { ("1", "2", 10), ("2", "3", 4) }, ParallelEdges.Summed(Listed));
    }

    [Fact]
    public void Directed_Edges_Merge_Only_In_The_Same_Direction() {
        // 1->2 twice merges; 2->1 runs the other way, so it stays its own edge.
        Assert.Equal(new List<(string, string, int)> { ("1", "2", 3), ("2", "3", 4), ("2", "1", 2) },
            ParallelEdges.Lightest(Listed, directed: true));
        Assert.Equal(new List<(string, string, int)> { ("1", "2", 8), ("2", "3", 4), ("2", "1", 2) },
            ParallelEdges.Summed(Listed, directed: true));
    }

    [Fact]
    public void A_Graph_Without_Parallel_Edges_Is_Unchanged() {
        var edges = new List<(string, string, int)> { ("a", "b", 1), ("b", "c", 2) };
        Assert.Equal(edges, ParallelEdges.Lightest(edges));
        Assert.Equal(edges, ParallelEdges.Summed(edges));
    }
}
