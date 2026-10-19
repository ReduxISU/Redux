
using System.Text.Json;
using Redux.Library;
using Xunit;

public class ReductionVisualizationTests {
    private readonly ReduxClient redux = new();

    private const string CliqueInstance =
        "(({1,2,3},{{1,2},{2,3},{1,3}}),3)";

    [Fact]
    public void DiscoversReductions() {
        Assert.Contains(
            "sipserReductionVertexCover",
            redux.GetReductions()
        );
    }

    [Fact]
    public void DiscoversVisualizations() {
        Assert.Contains(
            "CliqueDefaultVisualization",
            redux.GetVisualizations()
        );
    }

    [Fact]
    public void ReducesCliqueToVertexCover() {
        string result = redux.Reduce(
            "sipserReductionVertexCover",
            CliqueInstance
        );

        Assert.Equal("(({1,2,3},{}),0)", result);
    }

    [Fact]
    public void VisualizesClique() {
        string json = redux.Visualize(
            "CliqueDefaultVisualization",
            CliqueInstance
        );

        using var document = JsonDocument.Parse(json);

        Assert.Equal(
            "graph",
            document.RootElement.GetProperty("kind").GetString()
        );

        Assert.Equal(
            3,
            document.RootElement.GetProperty("nodes").GetArrayLength()
        );

        Assert.Equal(
            3,
            document.RootElement.GetProperty("links").GetArrayLength()
        );
    }

    [Fact]
    public void VisualizesSolvedClique() {
        string solution = redux.Solve(
            "CliqueBruteForce",
            CliqueInstance
        );

        string json = redux.VisualizeSolved(
            "CliqueDefaultVisualization",
            CliqueInstance,
            solution
        );

        using var document = JsonDocument.Parse(json);

        var nodes = document.RootElement.GetProperty("nodes");

        Assert.All(
            nodes.EnumerateArray(),
            node => Assert.Equal(
                "Solution",
                node.GetProperty("color").GetString()
            )
        );
    }
}
