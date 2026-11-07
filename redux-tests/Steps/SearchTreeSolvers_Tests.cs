using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_VERTEXCOVER;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Search Tree picture draws only the solvers whose steps carry a search tree, and those are exactly the solvers that
// declare ISearchTreeSolver: every solver is run on its problem's default instance, and its steps carry tree nodes just when
// it declares it.
public class SearchTreeSolvers_Tests {
    // Solvers that need an outside service to run (the quantum ones) are left out.
    private static bool Runnable(Type solver) => !solver.Name.Contains("Quantum", StringComparison.OrdinalIgnoreCase)
        && !solver.Name.Contains("Grover", StringComparison.OrdinalIgnoreCase);

    // The problem a solver solves (its ISolver<P>), when it can be made.
    private static Type? ProblemOf(Type solver) => solver.GetInterfaces()
        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>))?.GetGenericArguments()[0];

    public static TheoryData<string> SolverNames() {
        var data = new TheoryData<string>();
        foreach (var (name, type) in ProblemProvider.Solvers.OrderBy(s => s.Key, StringComparer.Ordinal))
            if (type.Assembly == typeof(ISolver).Assembly && Runnable(type) && ProblemOf(type) is { IsInterface: false, IsAbstract: false })
                data.Add(type.Name);
        return data;
    }

    [Theory]
    [MemberData(nameof(SolverNames))]
    public void StepsCarryATree_JustWhenTheSolverDeclaresIt(string name) {
        var type = ProblemProvider.Solvers.Values.Single(t => t.Name == name);
        string instance = ((IProblem)Activator.CreateInstance(ProblemOf(type)!)!).defaultInstance;
        var run = ((ISolver)Activator.CreateInstance(type)!).Run(instance, withSteps: true, TimeSpan.FromSeconds(20));
        bool carriesTree = run.Steps.Any(s => s.GetType().GetProperty(nameof(SolverStep<object>.Tree))?.GetValue(s) is IReadOnlyList<SearchNode> { Count: > 0 });
        Assert.Equal(typeof(ISearchTreeSolver).IsAssignableFrom(type), carriesTree);
    }
}

// The endpoints refuse a solver that records no tree for the Search Tree picture, instead of drawing an empty tree.
public class SearchTreeSolvers_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public SearchTreeSolvers_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private async Task<(HttpStatusCode Status, JsonElement Body)> Post(string path, string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/{path}?{query}", content, TestContext.Current.CancellationToken);
        string text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response.StatusCode, JsonDocument.Parse(text).RootElement.Clone());
    }

    [Theory]
    [InlineData("VertexCoverBruteForce")]
    [InlineData("GreedyVertexCover")]
    public async Task ASolverWithNoTree_IsRefused_AndListedAsUnavailable(string solver) {
        string instance = new VERTEXCOVER().defaultInstance;
        var (status, body) = await Post("visualize", $"visualization=VertexCoverSearchTreeVisualization&solver={solver}&format=frames", instance);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("solver_mismatch", body.GetProperty("error").GetString());

        (status, body) = await Post("visualizeRun", $"solver={solver}&visualizations=VertexCoverSearchTreeVisualization,VertexCoverDefaultVisualization", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(["VertexCoverDefaultVisualization"], VisualizeRun_Endpoint_Tests.Pictures(body).Select(p => p.GetProperty("visualization").GetString()));
        var refused = Assert.Single(VisualizeRun_Endpoint_Tests.Unavailable(body));
        Assert.Equal(("VertexCoverSearchTreeVisualization", "solver_mismatch"),
            (refused.GetProperty("visualization").GetString(), refused.GetProperty("reason").GetString()));
    }

    [Fact]
    public async Task TheSolverThatRecordsTheTree_IsStillDrawn() {
        string instance = new VERTEXCOVER().defaultInstance;
        var (status, body) = await Post("visualize", "visualization=VertexCoverSearchTreeVisualization&solver=VertexCoverBoundedSearchTree&format=frames", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("SearchTree", body.GetProperty("type").GetString());
    }
}
