using System.Diagnostics;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Performance regression tests (issue #621). Past slowdowns (#461: repeated
// startup reflection scans, #456: uncached filesystem walk per request, #463:
// re-parse in a solver loop) were only ever found by reading code — nothing here
// would have run in CI and flagged them automatically. These tests exist to close
// that gap, not to pin exact numbers: every budget is deliberately generous
// (roughly 5-10x a locally measured median, with a floor so sub-millisecond
// medians don't turn into flaky micro-budgets on a noisier CI runner).
//
// All tests are tagged [Trait("Category", "Performance")] so CI (.github/workflows/
// main.yml) can run them as their own step, separate from the
// regular unit tests.
//
// Timings were measured locally (Release build, otherwise-idle machine, .NET 10)
// by running each scenario 5-10 times and recording the median; see the comment
// next to each budget below for the numbers that budget was derived from.
internal static class PerfTiming {
    public static double Median(List<double> values) {
        var sorted = values.OrderBy(x => x).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
    }

    // Times `runs` sequential GETs to `url` and returns the median, in milliseconds.
    // Callers that care about warm (post-JIT) performance should issue one throwaway
    // request before calling this.
    public static async Task<double> MedianRequestMs(HttpClient client, string url, int runs, CancellationToken ct) {
        var times = new List<double>(runs);
        for (int i = 0; i < runs; i++) {
            var sw = Stopwatch.StartNew();
            var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
        }
        return Median(times);
    }
}

// ── Cold-start timings ──────────────────────────────────────────────────────
//
// Each test here builds its own AppFactory rather than sharing an
// IClassFixture<AppFactory>, specifically so the request under test really is
// the first one the app ever serves (an IClassFixture instance is shared across
// every [Fact] in its class, so a second test would see an already-warm app).
public class ColdStart_Performance_Tests {
    [Fact]
    [Trait("Category", "Performance")]
    public async Task FirstRequestAfterStart_Health_UnderBudget() {
        using var factory = new AppFactory();
        using var client = factory.CreateClient();

        var sw = Stopwatch.StartNew();
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        sw.Stop();
        response.EnsureSuccessStatusCode();

        // Measured locally: 868-1412ms across 5 cold runs (median ~960ms). This mostly
        // pays for WebApplicationFactory/Kestrel host startup (DI container, middleware
        // pipeline), not /health's own logic -- /health does no reflection or problem-data
        // work, so a regression here means something in the general startup path slowed
        // down, not /health specifically.
        const double budgetMs = 6000;
        Assert.True(sw.Elapsed.TotalMilliseconds < budgetMs,
            $"First request to /health took {sw.Elapsed.TotalMilliseconds:F1}ms, budget {budgetMs}ms.");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task FirstRequestAfterStart_AllInfo_UnderBudget() {
        using var factory = new AppFactory();
        using var client = factory.CreateClient();

        var sw = Stopwatch.StartNew();
        var response = await client.GetAsync("/Navigation/Batch/allInfo", TestContext.Current.CancellationToken);
        sw.Stop();
        response.EnsureSuccessStatusCode();

        // Measured locally: 2136-3368ms across 5 cold runs (median ~2380ms) for the very
        // first call. allInfo Activator.CreateInstance's every reflected interface type
        // and serializes the lot (cached afterwards via Lazy<string>) -- this is exactly
        // the shape of regression #461 ("repeated startup reflection scans") would take,
        // so it gets its own cold-start budget separate from /health's.
        const double budgetMs = 15000;
        Assert.True(sw.Elapsed.TotalMilliseconds < budgetMs,
            $"First request to /Navigation/Batch/allInfo took {sw.Elapsed.TotalMilliseconds:F1}ms, budget {budgetMs}ms.");
    }
}

// ── Warm endpoint timings ───────────────────────────────────────────────────
//
// Shares one AppFactory/HttpClient across all tests (the app is already up), and
// each test issues one throwaway warm-up request before timing, so these measure
// steady-state per-request cost rather than JIT/first-call overhead.
public class Endpoint_Performance_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public Endpoint_Performance_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private async Task<double> WarmMedianMs(string url, int runs = 7) {
        await _client.GetAsync(url, TestContext.Current.CancellationToken); // warm-up: pay JIT cost here, not in the measured runs
        return await PerfTiming.MedianRequestMs(_client, url, runs, TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task Health_WarmMedian_UnderBudget() {
        var median = await WarmMedianMs("/health");
        // Measured locally: median 0.46-0.55ms across 5 runs of 10 warm requests each.
        // Budget is a floor, not 5-10x, since a few-hundred-microsecond median would
        // make for a flaky single-digit-millisecond budget on a busier CI runner.
        const double budgetMs = 50;
        Assert.True(median < budgetMs,
            $"/health warm median {median:F2}ms, budget {budgetMs}ms.");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task AllInfo_WarmMedian_UnderBudget() {
        var median = await WarmMedianMs("/Navigation/Batch/allInfo");
        // Measured locally: median 1.48-3.82ms across 5 runs of 10 warm requests each
        // (allInfo's payload is cached via Lazy<string> after the first call, so warm
        // calls are just re-serving cached bytes).
        const double budgetMs = 100;
        Assert.True(median < budgetMs,
            $"/Navigation/Batch/allInfo warm median {median:F2}ms, budget {budgetMs}ms.");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task AllProblems_WarmMedian_UnderBudget() {
        var median = await WarmMedianMs("/Navigation/ALL_ProblemsRefactor");
        // Measured locally: median 0.57-0.76ms across 5 runs of 10 warm requests each.
        const double budgetMs = 50;
        Assert.True(median < budgetMs,
            $"/Navigation/ALL_ProblemsRefactor warm median {median:F2}ms, budget {budgetMs}ms.");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task ReductionGraph_WarmMedian_UnderBudget() {
        var median = await WarmMedianMs("/Navigation/Reductions");
        // Measured locally: median 0.74-1.56ms across 5 runs of 10 warm requests each.
        const double budgetMs = 50;
        Assert.True(median < budgetMs,
            $"/Navigation/Reductions warm median {median:F2}ms, budget {budgetMs}ms.");
    }
}

// ── Reduction reduce() timings ──────────────────────────────────────────────
//
// One case per reduction graph edge, same enumeration as
// redux-tests/Metadata/ReductionSmoke_Tests.cs (AllReductionEdges), driven the
// same way: construct the FROM problem's defaultInstance, then construct the
// reduction from it (which runs reduce() internally). Unlike ReductionSmoke_Tests
// this is purely a timing guard -- it does not re-check correctness.
public class ReductionReduce_Performance_Tests {
    [Theory]
    [Trait("Category", "Performance")]
    [MemberData(nameof(ReductionSmoke_Tests.AllReductionEdges), MemberType = typeof(ReductionSmoke_Tests))]
    public void Reduce_WarmMedian_UnderBudget(string reductionClassName) {
        Assert.True(ProblemProvider.Reductions.TryGetValue(reductionClassName.ToLower(), out var reductionType),
            $"{reductionClassName}: not found in ProblemProvider.Reductions.");

        var generic = reductionType!.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReduction<,>));
        Assert.True(generic != null, $"{reductionClassName}: does not implement IReduction<,>.");
        var fromType = generic!.GetGenericArguments()[0];

        var fromDefault = Activator.CreateInstance(fromType) as IProblem;
        Assert.True(fromDefault != null, $"{reductionClassName}: {fromType.Name} did not default-construct into an IProblem.");
        string defaultInstance = fromDefault!.defaultInstance;

        // Warm-up (pays JIT cost for this specific reduction type), then time N runs.
        Activator.CreateInstance(reductionType, defaultInstance);
        var times = new List<double>();
        for (int i = 0; i < 5; i++) {
            var sw = Stopwatch.StartNew();
            Activator.CreateInstance(reductionType, defaultInstance);
            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
        }
        var median = PerfTiming.Median(times);

        // Measured locally across all 19 reduction edges: per-edge median 1.82ms
        // overall, slowest single edge (KarpSATToSAT3) 6.16ms on a cold JIT call. This
        // budget applies uniformly rather than per-reduction since the graph gains new
        // edges over time and a per-case number would need constant upkeep.
        const double budgetMs = 100;
        Assert.True(median < budgetMs,
            $"{reductionClassName}: reduce() warm median {median:F2}ms, budget {budgetMs}ms.");
    }
}
