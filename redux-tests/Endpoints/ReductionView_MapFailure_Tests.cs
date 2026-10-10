using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Reduction View's map phase: a solver answer the backward map cannot read is the server's fault, not the caller's, and
// a map step about a source element no target element stands for points at that element in the source picture.
public class ReductionView_MapFailure_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public ReductionView_MapFailure_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    // (x1 | x2) & (x1 | !x2): the clique brute force picks {x1_0, x1_1}, so no clique node mentions x2.
    private const string Sat = "SipserReduceToCliqueStandard", Formula = "(x1 | x2) & (x1 | !x2)";

    // ---- A solver answer the backward map cannot read ----

    // Only the solver's own answer can reach this path: the caller's input is checked before the solve. No solver of these
    // targets answers in a format the backward maps cannot read, so the backward map here is made to misread it.
    [Fact]
    public void Create_WhenTheBackwardMapCannotReadTheSolversAnswer_ThrowsSolverAnswerUnmappable() {
        var (red, steps) = ProblemProvider.RecordedReduction(Sat, Formula);
        var sourceVis = (IFramesVisualization)red.reductionFrom.defaultVisualization;
        var targetVis = (IFramesVisualization)red.reductionTo.defaultVisualization;
        ISolver solver = red.reductionTo.defaultVisualization.solver;
        string answer = solver.Run(red.reductionTo.instance, false).Answer;

        var ex = Assert.Throws<SolverAnswerUnmappableException>(() => ReductionView.Create(red, Formula, steps!, solver, sourceVis, targetVis,
            _ => red.MapBackRun("{not_a_node}", true)!));

        Assert.Equal(solver.solverName, ex.Solver);
        Assert.Equal(answer, ex.Answer);
        Assert.Contains("'not_a_node' is not a node of the problem", ex.Message);
    }

    [Fact]
    public void SolverAnswerUnmappable_Is500WithTheSolverAndItsAnswer_AndIsLogged() {
        var logger = new CapturingLogger();
        var controller = new ProblemProvider(null, logger);
        var ex = new SolverAnswerUnmappableException("Clique Brute Force", "{zz}", "'zz' is not a node of the problem", new FormatException("inner"));

        var result = Assert.IsType<ObjectResult>(controller.SolverAnswerUnmappable(Sat, ex));

        Assert.Equal(500, result.StatusCode);
        var body = JsonDocument.Parse(JsonSerializer.Serialize(result.Value)).RootElement;
        Assert.Equal(new[] { "error", "reduction", "solver", "answer", "detail" }, body.EnumerateObject().Select(p => p.Name));
        Assert.Equal("solver_answer_unmappable", body.GetProperty("error").GetString());
        Assert.Equal(Sat, body.GetProperty("reduction").GetString());
        Assert.Equal("Clique Brute Force", body.GetProperty("solver").GetString());
        Assert.Equal("{zz}", body.GetProperty("answer").GetString());
        Assert.Equal("'zz' is not a node of the problem", body.GetProperty("detail").GetString());
        var (level, message, logged) = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, level);
        Assert.Same(ex, logged);
        Assert.Contains("{zz}", message);
        Assert.Contains("Clique Brute Force", message);
    }

    private sealed class CapturingLogger : ILogger<ProblemProvider> {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
