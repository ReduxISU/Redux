using Redux.Library;
using Xunit;

public class ReduxClient_Tests {
    private readonly ReduxClient redux = new();

    [Fact]
    public void DiscoversProblems() {
        Assert.NotEmpty(redux.GetProblems());
    }

    [Fact]
    public void DiscoversSolvers() {
        Assert.Contains("SATBruteForceSolver", redux.GetSolvers());
    }

    [Fact]
    public void DiscoversVerifiers() {
        Assert.NotEmpty(redux.GetVerifiers());
    }

    [Fact]
    public void SolvesSAT() {
        string result = redux.Solve("SATBruteForceSolver", "(x1)");

        Assert.Equal("(x1:True)", result);
    }

    [Fact]
    public void RejectsUnknownSolver() {
        Assert.Throws<ArgumentException>(() =>
            redux.Solve("NonExistentSolver", "(x1)"));
    }
    [Fact]
    public void ExcludesDummyImplementations() {
        Assert.DoesNotContain("DummySolver", redux.GetSolvers());
        Assert.DoesNotContain("DummyVerifier", redux.GetVerifiers());
        Assert.DoesNotContain("DummyVisualization", redux.GetVisualizations());
    }

    [Fact]
    public void DiscoversRealImplementations() {
        Assert.Contains("SATBruteForceSolver", redux.GetSolvers());
        Assert.Contains("CliqueDefaultVisualization", redux.GetVisualizations());
    }

    [Fact]
    public void KeepsAllProblemsIncludingThoseWithMissingDefaults() {
        // Placeholder implementations must not affect problem discovery.
        Assert.NotEmpty(redux.GetProblems());
        Assert.DoesNotContain("DummySolver", redux.GetProblems());
    }
}
