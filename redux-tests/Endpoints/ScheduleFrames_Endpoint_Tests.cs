using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces.Schedule;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Schedule picture on the actual /ProblemProvider/visualize?format=frames output, for Job Sequencing and both Pump
// Scheduling problems with their solvers, plus the guards that keep everything old frontends read exactly as it was:
//   - the list format of both pump visualizations (Snapshots/schedule_list_snapshots.tsv, captured before the picture existed),
//   - /info for the three problems, their solvers, their verifiers and the pump visualizations (info.<name>.json, captured the
//     same way).
// The deliberate exception is Job Sequencing's default visualization, which moved off DummyVisualization to
// JobSeqVisualization: see the "rewire" test.
public class ScheduleFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public ScheduleFrames_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private static string ReadSnapshot(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file)).Replace("\r\n", "\n");

    private async Task<(HttpStatusCode Status, string Body)> Post(string path, string json) {
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/{path}", content, TestContext.Current.CancellationToken);
        return (response.StatusCode, (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n"));
    }

    private Task<(HttpStatusCode Status, string Body)> Visualize(string query, string instance) =>
        Post($"visualize?{query}", JsonSerializer.Serialize(instance));

    private static string Compact(string json) => JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);

    private static string Readable(JsonElement element) =>
        JsonSerializer.Serialize(element, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static Dictionary<string, string> StatesOf(JsonElement map) =>
        map.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetProperty("state").GetString()!);

    private static readonly HashSet<string> Vocabulary = new() {
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked",
    };

    public static TheoryData<string, string, string> FramesCases {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var (solver, instance, answer) in ScheduleSolvers_Tests.Rows()) data.Add(solver, instance, answer);
            return data;
        }
    }

    public static TheoryData<string, string, string> ListCases {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var line in ReadSnapshot("schedule_list_snapshots.tsv").Split('\n').Where(l => l.Length > 0)) {
                var parts = line.Split('\t');
                data.Add(parts[0], JsonSerializer.Deserialize<string>(parts[1])!, parts[2]);
            }
            return data;
        }
    }

    public static TheoryData<string> TouchedInterfaces => new([
        "jobseqbruteforce", "jobseqverifier",
        "pumpschedulingcm", "pumpschedulingcmsolver", "pumpschedulingcmverifier", "pumpschedulingcmvisualization",
        "pumpschedulingem", "pumpschedulingemsolver", "pumpschedulingemverifier", "pumpschedulingemvisualization",
    ]);

    // ---- What old frontends read must not change. ----

    [Theory]
    [MemberData(nameof(TouchedInterfaces))]
    public async Task Info_IsUnchangedByTheSchedulePicture(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body.Replace("\r\n", "\n"));
        Assert.DoesNotContain("\"Schedule\"", InfoJson.WithoutPictureType(body));
        Assert.DoesNotContain("StepShape", body);
        Assert.DoesNotContain("Describe", body);
        Assert.DoesNotContain("ParseAnswer", body);
    }

    // Job Sequencing's default visualization moved off DummyVisualization to JobSeqVisualization. Its /info snapshot was
    // retaken after the rewire; the diff against the pre-change capture is only that nested object. No reduction goes into
    // or out of Job Sequencing, so nothing else embeds it.
    [Fact]
    public async Task Rewire_JobSeqInfo_ShowsJobSeqVisualization_AndNothingElse() {
        var response = await _client.GetAsync("/ProblemProvider/info?interface=jobseq", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ReadSnapshot("info.jobseq.json"), body.ReplaceLineEndings("\n"));
        Assert.DoesNotContain("DummyVisualization", body);
        Assert.DoesNotContain("\"Schedule\"", InfoJson.WithoutPictureType(body));
        var vis = JsonDocument.Parse(body).RootElement.GetProperty("defaultVisualization");
        Assert.Equal("Job Sequencing Schedule Visualization", vis.GetProperty("visualizationName").GetString());
        Assert.Equal("DynamicTable", vis.GetProperty("visualizationType").GetString());
        Assert.Equal("Job Sequencing Brute Force", vis.GetProperty("solver").GetProperty("solverName").GetString());
        var own = await _client.GetAsync("/ProblemProvider/info?interface=jobseqvisualization", TestContext.Current.CancellationToken);
        Assert.Equal(Compact(await own.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)), Compact(vis.GetRawText()));
    }

    [Theory]
    [MemberData(nameof(ListCases))]
    public async Task List_MatchesPreChangeSnapshot(string vis, string instance, string expected) {
        var (status, body) = await Visualize($"visualization={vis}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(expected, Compact(body));
    }

    // The new visualization reports its legacy list type in /info and nothing of the picture.
    [Fact]
    public async Task JobSeqVisualization_Info_IsTheListTypeOnly() {
        var response = await _client.GetAsync("/ProblemProvider/info?interface=jobseqvisualization", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(["visualizationName", "visualizationDefinition", "source", "sourceFile", "contributors", "visualizationType", "pictureType", "solver"],
            root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("DynamicTable", root.GetProperty("visualizationType").GetString());
        Assert.Equal("Job Sequencing Brute Force", root.GetProperty("solver").GetProperty("solverName").GetString());
        Assert.DoesNotContain("\"Schedule\"", InfoJson.WithoutPictureType(body));
        Assert.DoesNotContain("Describe", body);
    }

    [Fact]
    public async Task JobSeqVisualization_ListFormat_IsTheJobTableBeforeAndAfter() {
        var (status, body) = await Visualize("visualization=jobseqvisualization", "((4,2,5),(9,13,2),(1,4,3),4)");
        Assert.Equal(HttpStatusCode.OK, status);
        var list = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();
        Assert.Equal(2, list.Count);
        Assert.Equal(
            "{\"kind\":\"table\",\"title\":\"3 jobs, at most 4 total penalty for late jobs\",\"columns\":[{\"key\":\"job\",\"label\":\"Job\"},{\"key\":\"time\",\"label\":\"Time\"},"
            + "{\"key\":\"deadline\",\"label\":\"Deadline\"},{\"key\":\"penalty\",\"label\":\"Penalty\"},{\"key\":\"runs\",\"label\":\"Runs\"},{\"key\":\"late\",\"label\":\"Late\"}],"
            + "\"rows\":[{\"id\":\"J1\",\"color\":null,\"cells\":{\"job\":\"0\",\"time\":\"4\",\"deadline\":\"9\",\"penalty\":\"1\",\"runs\":\"\",\"late\":\"\"},\"cellColors\":null},"
            + "{\"id\":\"J2\",\"color\":null,\"cells\":{\"job\":\"1\",\"time\":\"2\",\"deadline\":\"13\",\"penalty\":\"4\",\"runs\":\"\",\"late\":\"\"},\"cellColors\":null},"
            + "{\"id\":\"J3\",\"color\":null,\"cells\":{\"job\":\"2\",\"time\":\"5\",\"deadline\":\"2\",\"penalty\":\"3\",\"runs\":\"\",\"late\":\"\"},\"cellColors\":null}]}",
            Readable(list[0]));
        // (0,1,2): job 2 ends at 11, after its deadline of 2, and its penalty of 3 fits K = 4.
        Assert.Equal(["0 to 4 no Solution", "4 to 6 no Solution", "6 to 11 yes Rejected"], list[1].GetProperty("rows").EnumerateArray()
            .Select(r => $"{r.GetProperty("cells").GetProperty("runs").GetString()} {r.GetProperty("cells").GetProperty("late").GetString()} {r.GetProperty("color").GetString()}"));

        // No answer: the table stays blank.
        var (_, none) = await Visualize("visualization=jobseqvisualization", ScheduleSolvers_Tests.NothingFits);
        var tables = JsonDocument.Parse(none).RootElement.EnumerateArray().ToList();
        Assert.All(tables[^1].GetProperty("rows").EnumerateArray(), r => Assert.Equal("", r.GetProperty("cells").GetProperty("runs").GetString()));
    }

    // ---- The picture ----

    [Theory]
    [MemberData(nameof(FramesCases))]
    public async Task Frames_EverySolver_HasTheSchedulePayloadAndFramesThatAgreeWithTheAnswer(string solver, string instance, string answer) {
        string vis = ScheduleSolvers_Tests.VisualizationOf[solver];
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(["type", "payload", "frames"], root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Schedule", root.GetProperty("type").GetString());
        var payload = root.GetProperty("payload");
        Assert.Equal("schedule", payload.GetProperty("kind").GetString());
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        var done = Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.Equal(done, frames[^1]);
        Assert.All(frames, f => Assert.False(string.IsNullOrWhiteSpace(f.GetProperty("caption").GetString())));
        Assert.All(frames.SkipLast(1), f => Assert.False(f.TryGetProperty("ok", out _)));
        bool ok = done.GetProperty("ok").GetBoolean();

        if (solver == "jobseqbruteforce") {
            Assert.Equal(["kind", "rule", "jobs", "k", "span"], payload.EnumerateObject().Select(p => p.Name));
            Assert.Equal("jobSequencing", payload.GetProperty("rule").GetString());
            var jobs = payload.GetProperty("jobs").EnumerateArray().ToList();
            Assert.All(jobs, (j, i) => {
                Assert.Equal(["id", "label", "time", "deadline", "penalty"], j.EnumerateObject().Select(p => p.Name));
                Assert.Equal(($"J{i + 1}", $"Job {i}"), (j.GetProperty("id").GetString(), j.GetProperty("label").GetString()));
            });
            var ids = jobs.Select(j => j.GetProperty("id").GetString()!).ToHashSet();
            var byId = jobs.ToDictionary(j => j.GetProperty("id").GetString()!);
            foreach (var f in frames) {
                Assert.Equal(["event", "caption", "focus", "order", "runs", "jobs", "penalty", .. f.TryGetProperty("ok", out _) ? new[] { "ok" } : []],
                    f.EnumerateObject().Select(p => p.Name));
                Assert.All(f.GetProperty("focus").EnumerateArray(), id => Assert.Contains(id.GetString()!, ids));
                var states = StatesOf(f.GetProperty("jobs"));
                Assert.All(states, s => { Assert.Contains(s.Key, ids); Assert.Contains(s.Value, Vocabulary); });
                // Runs follow the order back to back; a run that ends after its deadline is late and Rejected, and its penalty counts.
                int t = 0, penalty = 0;
                var order = f.GetProperty("order").EnumerateArray().Select(x => x.GetString()!).ToList();
                var runs = f.GetProperty("runs").EnumerateArray().ToList();
                Assert.Equal(order, runs.Select(r => r.GetProperty("job").GetString()));
                foreach (var r in runs) {
                    var job = byId[r.GetProperty("job").GetString()!];
                    Assert.Equal(t, r.GetProperty("start").GetInt32());
                    t += job.GetProperty("time").GetInt32();
                    Assert.Equal(t, r.GetProperty("end").GetInt32());
                    bool late = t > job.GetProperty("deadline").GetInt32();
                    Assert.Equal(late, r.GetProperty("late").GetBoolean());
                    if (late) { penalty += job.GetProperty("penalty").GetInt32(); Assert.Equal("Rejected", states[r.GetProperty("job").GetString()!]); }
                }
                Assert.Equal(penalty, f.GetProperty("penalty").GetInt32());
                // Whole orders: what is placed is highlighted while tried, the answer once accepted.
                string ev = f.GetProperty("event").GetString()!;
                string placed = ev == "Reject" ? "ElementHighlight" : "Solution";
                Assert.All(states.Where(s => s.Value != "Rejected"), s => Assert.Equal(placed, s.Value));
            }
            if (ok) {
                Assert.Equal(answer, "(" + string.Join(",", done.GetProperty("order").EnumerateArray().Select(x => int.Parse(x.GetString()![1..]) - 1)) + ")");
                Assert.True(done.GetProperty("penalty").GetInt32() <= payload.GetProperty("k").GetInt32());
            } else {
                Assert.Equal("{}", answer);
                Assert.Empty(done.GetProperty("order").EnumerateArray());
                Assert.Empty(done.GetProperty("jobs").EnumerateObject());
            }
        } else {
            bool em = solver == "pumpschedulingemsolver";
            Assert.Equal(["kind", "rule", "hours", "pumps", "tank", "rates", .. em ? new[] { "budget" } : []], payload.EnumerateObject().Select(p => p.Name));
            Assert.Equal(em ? "emergencyResilience" : "costMinimization", payload.GetProperty("rule").GetString());
            var hours = payload.GetProperty("hours").EnumerateArray().ToList();
            Assert.Equal(Enumerable.Range(0, 24).Select(h => $"t{h}"), hours.Select(h => h.GetProperty("id").GetString()));
            Assert.All(hours, h => Assert.Equal(["id", "label", "demand", "peak"], h.EnumerateObject().Select(p => p.Name)));
            var pumps = payload.GetProperty("pumps").EnumerateArray().ToList();
            Assert.All(pumps, (p, i) => {
                Assert.Equal(["id", "name", "flow", "power", "startupCost"], p.EnumerateObject().Select(x => x.Name));
                Assert.Equal($"P{i + 1}", p.GetProperty("id").GetString());
            });
            var tank = payload.GetProperty("tank");
            Assert.Equal(["capacity", "level", "minimum"], tank.EnumerateObject().Select(p => p.Name));
            var hourIds = hours.Select(h => h.GetProperty("id").GetString()!).ToHashSet();
            var cellIds = pumps.SelectMany(p => hourIds.Select(h => p.GetProperty("id").GetString() + h)).ToHashSet();
            foreach (var f in frames) {
                Assert.Equal(["event", "caption", "focus", "reached", "schedule", "upTo", "hours", "cells",
                    .. f.TryGetProperty("budget", out _) ? new[] { "budget" } : [], .. f.TryGetProperty("ok", out _) ? new[] { "ok" } : []],
                    f.EnumerateObject().Select(p => p.Name));
                Assert.Equal(em, f.TryGetProperty("budget", out _));
                Assert.All(f.GetProperty("focus").EnumerateArray(), id => Assert.Contains(id.GetString()!, hourIds));
                var hourStates = StatesOf(f.GetProperty("hours"));
                var cellStates = StatesOf(f.GetProperty("cells"));
                Assert.All(hourStates, s => { Assert.Contains(s.Key, hourIds); Assert.Contains(s.Value, Vocabulary); });
                Assert.All(cellStates, s => { Assert.Contains(s.Key, cellIds); Assert.Equal("Solution", s.Value); });
                Assert.All(f.GetProperty("reached").EnumerateArray(), r => Assert.Equal(["states", "low", "high", "best"], r.EnumerateObject().Select(p => p.Name)));
                var schedule = f.GetProperty("schedule").EnumerateArray().ToList();
                int upTo = f.GetProperty("upTo").GetInt32();
                if (schedule.Count == 0) {
                    Assert.Equal(-1, upTo);
                    Assert.Empty(cellStates);
                    continue;
                }
                Assert.Equal(24, schedule.Count);
                // Each hour on the schedule: its pumps are its Solution cells, and an hour drawn so far outside the tank's limits is Rejected.
                for (int h = 0; h < 24; h++) {
                    var row = schedule[h];
                    Assert.Equal(["hour", "on", "starts", "inflow", "level", "energy", "startup"], row.EnumerateObject().Select(p => p.Name));
                    Assert.Equal($"t{h}", row.GetProperty("hour").GetString());
                    Assert.All(row.GetProperty("on").EnumerateArray(), p => Assert.True(cellStates.ContainsKey(p.GetString() + $"t{h}")));
                    double level = row.GetProperty("level").GetDouble();
                    bool outside = level < tank.GetProperty("minimum").GetDouble() - 1e-6 || level > tank.GetProperty("capacity").GetDouble() + 1e-6;
                    if (outside && h <= upTo) Assert.Equal("Rejected", hourStates[$"t{h}"]);
                }
                Assert.Equal(cellStates.Count, schedule.Sum(r => r.GetProperty("on").GetArrayLength()));
            }
            if (answer == "{}") {
                Assert.False(ok);
                Assert.Empty(done.GetProperty("schedule").EnumerateArray());
            } else {
                // The Done frame's schedule is the answer's.
                var rows = pumps.Select((p, i) => $"({p.GetProperty("name").GetString()},"
                    + string.Join(",", done.GetProperty("schedule").EnumerateArray().Select(r => r.GetProperty("on").EnumerateArray().Any(x => x.GetString() == $"P{i + 1}") ? "1" : "0")) + ")");
                Assert.EndsWith("(" + string.Join(",", rows) + "))", answer);
            }
        }
    }

    [Fact]
    public async Task Frames_JobSeqDefault_MatchesTheMockup() {
        var (_, body) = await Visualize("visualization=jobseqvisualization&format=frames", ScheduleSolvers_Tests.JobDefault);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(
            "{\"kind\":\"schedule\",\"rule\":\"jobSequencing\",\"jobs\":[{\"id\":\"J1\",\"label\":\"Job 0\",\"time\":4,\"deadline\":9,\"penalty\":1},"
            + "{\"id\":\"J2\",\"label\":\"Job 1\",\"time\":2,\"deadline\":13,\"penalty\":4},{\"id\":\"J3\",\"label\":\"Job 2\",\"time\":5,\"deadline\":2,\"penalty\":3},"
            + "{\"id\":\"J4\",\"label\":\"Job 3\",\"time\":9,\"deadline\":17,\"penalty\":2},{\"id\":\"J5\",\"label\":\"Job 4\",\"time\":4,\"deadline\":21,\"penalty\":5},"
            + "{\"id\":\"J6\",\"label\":\"Job 5\",\"time\":3,\"deadline\":16,\"penalty\":8}],\"k\":4,\"span\":27}",
            Readable(root.GetProperty("payload")));
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(152, frames.Count);
        // The mockup's step: (0,1,2,5,3,4), jobs 2, 3 and 4 late.
        Assert.Equal(
            "{\"event\":\"Reject\",\"caption\":\"Try (0,1,2,5,3,4): jobs 2, 3 and 4 finish late, penalty 10 > K = 4.\",\"focus\":[\"J3\",\"J4\",\"J5\"],"
            + "\"order\":[\"J1\",\"J2\",\"J3\",\"J6\",\"J4\",\"J5\"],"
            + "\"runs\":[{\"job\":\"J1\",\"start\":0,\"end\":4,\"late\":false},{\"job\":\"J2\",\"start\":4,\"end\":6,\"late\":false},{\"job\":\"J3\",\"start\":6,\"end\":11,\"late\":true},"
            + "{\"job\":\"J6\",\"start\":11,\"end\":14,\"late\":false},{\"job\":\"J4\",\"start\":14,\"end\":23,\"late\":true},{\"job\":\"J5\",\"start\":23,\"end\":27,\"late\":true}],"
            + "\"jobs\":{\"J1\":{\"state\":\"ElementHighlight\"},\"J2\":{\"state\":\"ElementHighlight\"},\"J3\":{\"state\":\"Rejected\"},\"J4\":{\"state\":\"Rejected\"},"
            + "\"J5\":{\"state\":\"Rejected\"},\"J6\":{\"state\":\"ElementHighlight\"}},\"penalty\":10}",
            Readable(frames[4]));
        // The mockup's solved view: (1,3,5,4,0,2), jobs 0 and 2 late, penalty 4.
        Assert.Equal(
            "{\"event\":\"Done\",\"caption\":\"(1,3,5,4,0,2) is the first order that keeps the late penalty within K = 4, found after 191 orders. (40 not shown)\",\"focus\":[],"
            + "\"order\":[\"J2\",\"J4\",\"J6\",\"J5\",\"J1\",\"J3\"],"
            + "\"runs\":[{\"job\":\"J2\",\"start\":0,\"end\":2,\"late\":false},{\"job\":\"J4\",\"start\":2,\"end\":11,\"late\":false},{\"job\":\"J6\",\"start\":11,\"end\":14,\"late\":false},"
            + "{\"job\":\"J5\",\"start\":14,\"end\":18,\"late\":false},{\"job\":\"J1\",\"start\":18,\"end\":22,\"late\":true},{\"job\":\"J3\",\"start\":22,\"end\":27,\"late\":true}],"
            + "\"jobs\":{\"J1\":{\"state\":\"Rejected\"},\"J2\":{\"state\":\"Solution\"},\"J3\":{\"state\":\"Rejected\"},\"J4\":{\"state\":\"Solution\"},"
            + "\"J5\":{\"state\":\"Solution\"},\"J6\":{\"state\":\"Solution\"}},\"penalty\":4,\"ok\":true}",
            Readable(frames[^1]));
    }

    [Fact]
    public async Task Frames_PumpCMDefault_MatchesTheMockup() {
        var (_, body) = await Visualize("visualization=pumpschedulingcmvisualization&format=frames", ScheduleSolvers_Tests.CmDefault);
        var root = JsonDocument.Parse(body).RootElement;
        var payload = root.GetProperty("payload");
        Assert.Equal("{\"id\":\"t8\",\"label\":\"08:00\",\"demand\":1000,\"peak\":true}", Readable(payload.GetProperty("hours")[8]));
        Assert.Equal("[{\"id\":\"P1\",\"name\":\"PumpA\",\"flow\":200,\"power\":5,\"startupCost\":2.5},{\"id\":\"P2\",\"name\":\"PumpB\",\"flow\":350,\"power\":8.5,\"startupCost\":4},"
            + "{\"id\":\"P3\",\"name\":\"PumpC\",\"flow\":500,\"power\":12,\"startupCost\":6}]", Readable(payload.GetProperty("pumps")));
        Assert.Equal("{\"capacity\":10000,\"level\":5000,\"minimum\":0}", Readable(payload.GetProperty("tank")));
        Assert.Equal("{\"onPeak\":0.12,\"offPeak\":0.06}", Readable(payload.GetProperty("rates")));
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(50, frames.Count);
        Assert.Equal(
            "{\"event\":\"Try\",\"caption\":\"Forward pass, hour 0: 8 reachable (tank level, pumps on) states, levels 4,400 to 5,450 gal, cheapest so far $0.00.\","
            + "\"focus\":[\"t0\"],\"reached\":[{\"states\":8,\"low\":4400,\"high\":5450,\"best\":0}],\"schedule\":[],\"upTo\":-1,\"hours\":{\"t0\":{\"state\":\"ElementHighlight\"}},\"cells\":{}}",
            Readable(frames[0]));
        // The mockup's solved view: PumpA 23 hours, PumpB 24, PumpC none, $32.12, the tank down to 0 at midnight.
        var done = frames[^1];
        Assert.Equal("Cheapest schedule: $32.12 for the day. The tank stays between 0 and 5,000 gal.", done.GetProperty("caption").GetString());
        var cells = done.GetProperty("cells").EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal((23, 24, 0), (cells.Count(c => c.StartsWith("P1t")), cells.Count(c => c.StartsWith("P2t")), cells.Count(c => c.StartsWith("P3t"))));
        Assert.Equal("{\"hour\":\"t0\",\"on\":[\"P1\",\"P2\"],\"starts\":[\"P1\",\"P2\"],\"inflow\":550,\"level\":4950,\"energy\":0.81,\"startup\":6.5}",
            Readable(done.GetProperty("schedule")[0]));
        Assert.Equal(32.12, Math.Round(done.GetProperty("schedule").EnumerateArray().Sum(r => r.GetProperty("energy").GetDouble() + r.GetProperty("startup").GetDouble()), 2));
        Assert.Equal(0, done.GetProperty("schedule")[23].GetProperty("level").GetDouble());
    }

    [Fact]
    public async Task Frames_PumpEMDefault_CarriesTheBudgetItSettledOn() {
        var (_, body) = await Visualize("visualization=pumpschedulingemvisualization&format=frames", ScheduleSolvers_Tests.EmDefault);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(0, root.GetProperty("payload").GetProperty("budget").GetDouble());
        Assert.Equal("{\"capacity\":10000,\"level\":5000,\"minimum\":2000}", Readable(root.GetProperty("payload").GetProperty("tank")));
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(51, frames.Count);
        Assert.Equal(
            "{\"event\":\"Try\",\"caption\":\"The budget is 0, which means 1.5 times the cheapest schedule that keeps the tank within its limits. That costs $37.24, so the budget is $55.86.\","
            + "\"focus\":[],\"reached\":[],\"schedule\":[],\"upTo\":-1,\"hours\":{},\"cells\":{},\"budget\":55.86}",
            Readable(frames[0]));
        Assert.All(frames, f => Assert.Equal(55.86, f.GetProperty("budget").GetDouble()));
        var done = frames[^1];
        Assert.True(done.GetProperty("ok").GetBoolean());
        double spent = done.GetProperty("schedule").EnumerateArray().Sum(r => r.GetProperty("energy").GetDouble() + r.GetProperty("startup").GetDouble());
        Assert.Equal(55.55, Math.Round(spent, 2));
        Assert.All(done.GetProperty("schedule").EnumerateArray(), r => Assert.InRange(r.GetProperty("level").GetDouble(), 2000, 10000));
    }

    [Fact]
    public async Task Frames_PumpRounded_ShowsWhereTheTankRunsDry() {
        var (_, body) = await Visualize("visualization=pumpschedulingcmvisualization&format=frames", ScheduleSolvers_Tests.CmRounded);
        var done = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().Last();
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.Equal(-0.2, done.GetProperty("schedule")[2].GetProperty("level").GetDouble());
        Assert.Equal("Rejected", StatesOf(done.GetProperty("hours"))["t2"]);
        Assert.False(StatesOf(done.GetProperty("hours")).ContainsKey("t1"));
    }

    [Theory]
    [InlineData("jobseqvisualization", "((4,2,5),(9,13),(1,4,3),4)")]       // #800: lists of different lengths
    [InlineData("jobseqvisualization", "((-4,2,5),(9,13,1),(1,4,3),4)")]    // #800: a negative time
    [InlineData("jobseqvisualization", "((),(),(),4)")]                     // #800: no jobs
    [InlineData("pumpschedulingcmvisualization", "((10000,5000),(" + ScheduleSolvers_Tests.Demand + ",(8,24),(0.12,0.06))," + ScheduleSolvers_Tests.Pumps3 + ")")]  // #804
    [InlineData("pumpschedulingemvisualization", "((10000,5000,2000),(" + ScheduleSolvers_Tests.Demand + ",(8),(0.12,0.06)),((A,-200,5.0,2.5)),0)")]           // #804
    public async Task Frames_UnreadableInstance_IsA400_NotAPicture(string vis, string instance) {
        var (status, body) = await Visualize($"visualization={vis}&format=frames", instance);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("instance_parse_error", body);
    }

    // #801 and #803 on the real endpoints: no answer is {}, and the verifiers say False to it.
    [Theory]
    [InlineData("jobseqbruteforce", "jobseqverifier", ScheduleSolvers_Tests.NothingFits)]
    [InlineData("pumpschedulingcmsolver", "pumpschedulingcmverifier", ScheduleSolvers_Tests.CmDemandTooHigh)]
    [InlineData("pumpschedulingemsolver", "pumpschedulingemverifier", ScheduleSolvers_Tests.EmTinyBudget)]
    public async Task Solve_NoAnswer_IsBraces_AndVerifiesFalse(string solver, string verifier, string instance) {
        var (status, body) = await Post($"solve?solver={solver}", JsonSerializer.Serialize(instance));
        Assert.Equal((HttpStatusCode.OK, "\"{}\""), (status, body));
        var (vStatus, vBody) = await Post($"verify?verifier={verifier}", JsonSerializer.Serialize(new { ProblemInstance = instance, Certificate = "{}" }));
        Assert.Equal((HttpStatusCode.OK, "\"False\""), (vStatus, vBody));
    }

    // Wall-clock assertions run alone, after the parallel tests (#837).
    [Collection(TimingCollection.Name)]
    public class Timing : IClassFixture<AppFactory> {
        private readonly ScheduleFrames_Endpoint_Tests _outer;

        public Timing(AppFactory factory) {
            _outer = new ScheduleFrames_Endpoint_Tests(factory);
        }

        // #802 on the real endpoint: the first order that fits comes back at once, however many orders there are.
        [Fact]
        public async Task Solve_TwelveJobsThatFitInOrder_AnswersAtOnce() {
            string ones = string.Join(",", Enumerable.Repeat("1", 12));
            var sw = Stopwatch.StartNew();
            var (status, body) = await _outer.Post("solve?solver=jobseqbruteforce", JsonSerializer.Serialize($"(({ones}),({string.Join(",", Enumerable.Repeat("12", 12))}),({ones}),0)"));
            Assert.Equal((HttpStatusCode.OK, "\"(0,1,2,3,4,5,6,7,8,9,10,11)\""), (status, body));
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"took {sw.Elapsed}");
        }
    }
}

// Stopped runs, under the 0.05 s budget of TinyBudgetAppFactory.
[Collection(TimingCollection.Name)]
public class ScheduleFrames_StoppedRun_Endpoint_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public ScheduleFrames_StoppedRun_Endpoint_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    // Eleven jobs that all take 3 and are due at 3, with K = 0: no order fits, and there are 39,916,800 to try.
    internal const string ElevenLate = "((3,3,3,3,3,3,3,3,3,3,3),(3,3,3,3,3,3,3,3,3,3,3),(1,1,1,1,1,1,1,1,1,1,1),0)";

    private async Task<List<JsonElement>> StoppedFrames(string query, string instance) {
        var sw = Stopwatch.StartNew();
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?{query}&format=frames", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"took {sw.Elapsed}");
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal("Schedule", root.GetProperty("type").GetString());
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count >= 2);
        var done = frames[^1];
        Assert.Equal("Done", done.GetProperty("event").GetString());
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.Matches(@"^Stopped after 0\.05 s and [\d,]+ steps?; the solver had not finished\.", done.GetProperty("caption").GetString());
        Assert.Empty(done.GetProperty("focus").EnumerateArray());
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        return frames;
    }

    [Fact]
    public async Task Frames_StoppedJobSeq_EndsBlank_NotOk() {
        var frames = await StoppedFrames("visualization=jobseqvisualization", ElevenLate);
        var done = frames[^1];
        Assert.Empty(done.GetProperty("order").EnumerateArray());
        Assert.Empty(done.GetProperty("runs").EnumerateArray());
        Assert.Empty(done.GetProperty("jobs").EnumerateObject());
        Assert.Equal(0, done.GetProperty("penalty").GetInt32());
        Assert.Equal("Reject", frames[0].GetProperty("event").GetString());
    }

    [Theory]
    [InlineData("pumpschedulingcmvisualization", ScheduleSolvers_Tests.CmDefault)]
    [InlineData("pumpschedulingemvisualization", ScheduleSolvers_Tests.EmDefault)]
    public async Task Frames_StoppedPump_EndsBlank_NotOk(string vis, string instance) {
        var frames = await StoppedFrames($"visualization={vis}", instance);
        var done = frames[^1];
        Assert.Empty(done.GetProperty("reached").EnumerateArray());
        Assert.Empty(done.GetProperty("schedule").EnumerateArray());
        Assert.Equal(-1, done.GetProperty("upTo").GetInt32());
        Assert.Empty(done.GetProperty("hours").EnumerateObject());
        Assert.Empty(done.GetProperty("cells").EnumerateObject());
        Assert.False(done.TryGetProperty("budget", out _));
        Assert.Equal("Try", frames[0].GetProperty("event").GetString());
    }
}
