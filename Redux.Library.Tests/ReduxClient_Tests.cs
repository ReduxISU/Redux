using Redux.Library;
using Xunit;

public class ReduxClient_Tests
{
    private readonly ReduxClient redux = new();

    [Fact]
    public void DiscoversProblems()
    {
        Assert.NotEmpty(redux.GetProblems());
    }

    [Fact]
    public void DiscoversSolvers()
    {
        Assert.Contains("SATBruteForceSolver", redux.GetSolvers());
    }

    [Fact]
    public void DiscoversVerifiers()
    {
        Assert.NotEmpty(redux.GetVerifiers());
    }

    [Fact]
    public void SolvesSAT()
    {
        string result = redux.Solve("SATBruteForceSolver", "(x1)");

        Assert.Equal("(x1:True)", result);
    }

    [Fact]
    public void RejectsUnknownSolver()
    {
        Assert.Throws<ArgumentException>(() =>
            redux.Solve("NonExistentSolver", "(x1)"));
    }
}
