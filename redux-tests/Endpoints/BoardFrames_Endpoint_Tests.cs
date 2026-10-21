using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Board picture on the actual /ProblemProvider/visualize?format=frames output, for Sudoku and N-Queens with every one of
// their solvers, plus the guards that keep everything old frontends read exactly as it was:
//   - the list format of Sudoku's visualization (Snapshots/board_list_snapshots.tsv, captured before the picture existed),
//   - /info for the two problems, their three solvers, their verifiers and Sudoku's visualization (info.<name>.json, captured
//     the same way).
// The deliberate exceptions:
//   - N-Queens' default visualization moved off DummyVisualization to NQueensVisualization: see the "rewire" test.
//   - Sudoku's list format was an empty placeholder (Unimplemented, answering []). It is now the grid as a DynamicTable, so
//     its rows in board_list_snapshots.tsv and info.sudoku.json / info.sudokuvisualization.json were retaken: see the
//     "SudokuList" tests. Its format=frames picture is unchanged.
public class BoardFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public BoardFrames_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private static string ReadSnapshot(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file)).Replace("\r\n", "\n");

    private async Task<(HttpStatusCode Status, string Body)> Visualize(string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?{query}", content, TestContext.Current.CancellationToken);
        return (response.StatusCode, (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n"));
    }

    private static string Compact(string json) => JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);

    private static string Readable(JsonElement element) =>
        JsonSerializer.Serialize(element, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static readonly HashSet<string> Vocabulary = new() {
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked",
    };

    public static TheoryData<string, string, string> FramesCases {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var (solver, instance, answer) in BoardSolvers_Tests.Rows()) {
                // N-Queens Backtracking needs seconds from n = 20 up, which a busy test run can stretch past the endpoint's
                // time budget; BoardSolvers_Tests checks those rows without one.
                if (solver == "nqueensbacktracking" && int.Parse(instance.Trim()) >= 20) continue;
                data.Add(solver, instance, answer);
            }
            return data;
        }
    }

    public static TheoryData<string, string, string> ListCases {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var line in ReadSnapshot("board_list_snapshots.tsv").Split('\n').Where(l => l.Length > 0)) {
                var parts = line.Split('\t');
                data.Add(parts[0], JsonSerializer.Deserialize<string>(parts[1])!, parts[2]);
            }
            return data;
        }
    }

    public static TheoryData<string> TouchedInterfaces => new([
        "sudokusolver", "sudokuverifier", "nqueensbacktracking", "nqueensconstructive", "nqueensverifier",
    ]);

    // ---- What old frontends read must not change. ----

    [Theory]
    [MemberData(nameof(TouchedInterfaces))]
    public async Task Info_IsUnchangedByTheBoardPicture(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body.Replace("\r\n", "\n"));
        Assert.DoesNotContain("\"Board\"", body);
        Assert.DoesNotContain("StepShape", body);
        Assert.DoesNotContain("Describe", body);
        Assert.DoesNotContain("ParseAnswer", body);
    }

    // N-Queens' default visualization moved off DummyVisualization to NQueensVisualization. Its /info snapshot was retaken
    // after the rewire; the diff against the pre-change capture is only that nested object. No reduction goes into or out
    // of N-Queens, so nothing else embeds it.
    // The view is opt-in (IOptInVisualization), so it is the default only for a client that asks with includePictures=true;
    // Redux_GUI still sees the placeholder (GuiView_Endpoint_Tests).
    [Fact]
    public async Task Rewire_NQueensInfo_ShowsNQueensVisualization_AndNothingElse() {
        var response = await _client.GetAsync("/ProblemProvider/info?interface=nqueens&includePictures=true", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n");
        Assert.Equal(ReadSnapshot("info.nqueens.json"), body);
        Assert.DoesNotContain("DummyVisualization", body);
        Assert.DoesNotContain("\"Board\"", body);
        var vis = JsonDocument.Parse(body).RootElement.GetProperty("defaultVisualization");
        Assert.Equal("N-Queens Board Visualization", vis.GetProperty("visualizationName").GetString());
        Assert.Equal("DynamicTable", vis.GetProperty("visualizationType").GetString());
        Assert.Equal("N-Queens Backtracking", vis.GetProperty("solver").GetProperty("solverName").GetString());
        var own = await _client.GetAsync("/ProblemProvider/info?interface=nqueensvisualization&includePictures=true", TestContext.Current.CancellationToken);
        Assert.Equal(Compact(await own.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)), Compact(vis.GetRawText()));
    }

    [Theory]
    [MemberData(nameof(ListCases))]
    public async Task List_MatchesPreChangeSnapshot(string vis, string instance, string expected) {
        var (status, body) = await Visualize($"visualization={vis}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(expected, Compact(body));
    }

    // ---- Sudoku's list format: the grid as a DynamicTable instead of an empty placeholder. ----

    // /info for Sudoku and its visualization, retaken after the change: the only difference from the pre-change capture is
    // the visualization's visualizationType (Unimplemented to DynamicTable) and its visualizationDefinition (was "TODO").
    // Only a client that asks with includePictures=true is told DynamicTable: the view declares its legacy type
    // ([LegacyVisualizationType(Unimplemented)]), which Redux_GUI is still told (GuiView_Endpoint_Tests).
    [Theory]
    [InlineData("sudoku")]
    [InlineData("sudokuvisualization")]
    public async Task SudokuList_Info_ReportsDynamicTable_AndNothingOfThePicture(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}&includePictures=true", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n");
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body);
        var root = JsonDocument.Parse(body).RootElement;
        var vis = name == "sudoku" ? root.GetProperty("defaultVisualization") : root;
        Assert.Equal("DynamicTable", vis.GetProperty("visualizationType").GetString());
        Assert.DoesNotContain("\"Board\"", body);
        Assert.DoesNotContain("StepShape", body);
        Assert.DoesNotContain("Describe", body);
        Assert.DoesNotContain("ParseAnswer", body);
    }

    [Fact]
    public async Task SudokuList_IsTheGridBeforeAndAfter_TheSolversSquaresMarkedSolution() {
        string instance = "0,4,8,1,9,5,2,7,3;3,2,1,7,4,8,5,9,6;9,7,5,3,2,6,4,8,1;5,9,2,6,7,1,8,3,4;8,6,7,4,3,2,1,5,9;1,3,4,8,5,9,6,2,7;2,1,9,5,6,3,7,4,8;7,5,6,9,8,4,3,1,2;4,8,3,2,1,7,9,6,5";
        var (status, body) = await Visualize("visualization=sudokuvisualization", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var list = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();
        Assert.Equal(2, list.Count);
        Assert.All(list, t => Assert.Equal("table", t.GetProperty("kind").GetString()));
        Assert.Equal(["", "1", "2", "3", "4", "5", "6", "7", "8", "9"], list[0].GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("label").GetString()));
        Assert.Equal(9, list[0].GetProperty("rows").GetArrayLength());
        // Before: the clues, R1C1 empty, nothing colored.
        var before = list[0].GetProperty("rows")[0];
        Assert.Equal("1", before.GetProperty("cells").GetProperty("row").GetString());
        Assert.Equal("", before.GetProperty("cells").GetProperty("c1").GetString());
        Assert.Equal("4", before.GetProperty("cells").GetProperty("c2").GetString());
        Assert.All(list[0].GetProperty("rows").EnumerateArray(), r => Assert.Equal(JsonValueKind.Null, r.GetProperty("cellColors").ValueKind));
        // After: the one square the solver filled, R1C1 = 6, is the only one marked.
        var after = list[1].GetProperty("rows")[0];
        Assert.Equal("6", after.GetProperty("cells").GetProperty("c1").GetString());
        Assert.Equal("{\"c1\":\"Solution\"}", Readable(after.GetProperty("cellColors")));
        Assert.All(list[1].GetProperty("rows").EnumerateArray().Skip(1), r => Assert.Equal(JsonValueKind.Null, r.GetProperty("cellColors").ValueKind));
    }

    [Fact]
    public async Task SudokuList_NoSolution_ShowsOnlyTheClues() {
        string instance = "5,0,0,0,0,0,0,0,0;0,5,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0";
        var (_, body) = await Visualize("visualization=sudokuvisualization", instance);
        var list = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();
        Assert.Equal(2, list.Count);
        Assert.Equal(Readable(list[0]), Readable(list[1]));
    }

    // The new visualization reports its legacy list type in /info and nothing of the picture.
    [Fact]
    public async Task NQueensVisualization_Info_IsTheListTypeOnly() {
        var response = await _client.GetAsync("/ProblemProvider/info?interface=nqueensvisualization", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(["visualizationName", "visualizationDefinition", "source", "sourceFile", "contributors", "visualizationType", "solver"],
            root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("DynamicTable", root.GetProperty("visualizationType").GetString());
        Assert.Equal("N-Queens Backtracking", root.GetProperty("solver").GetProperty("solverName").GetString());
        Assert.DoesNotContain("\"Board\"", body);
        Assert.DoesNotContain("Describe", body);
    }

    [Fact]
    public async Task NQueensVisualization_ListFormat_IsTheTableBeforeAndAfter() {
        var (status, body) = await Visualize("visualization=nqueensvisualization", "4");
        Assert.Equal(HttpStatusCode.OK, status);
        var list = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();
        Assert.Equal(2, list.Count);
        Assert.Equal(
            "{\"kind\":\"table\",\"title\":\"4 queens on a 4 x 4 board\",\"columns\":[{\"key\":\"row\",\"label\":\"Row\"},{\"key\":\"column\",\"label\":\"Column\"}],"
            + "\"rows\":[{\"id\":\"r0\",\"color\":null,\"cells\":{\"row\":\"0\",\"column\":\"\"},\"cellColors\":null},"
            + "{\"id\":\"r1\",\"color\":null,\"cells\":{\"row\":\"1\",\"column\":\"\"},\"cellColors\":null},"
            + "{\"id\":\"r2\",\"color\":null,\"cells\":{\"row\":\"2\",\"column\":\"\"},\"cellColors\":null},"
            + "{\"id\":\"r3\",\"color\":null,\"cells\":{\"row\":\"3\",\"column\":\"\"},\"cellColors\":null}]}",
            Readable(list[0]));
        Assert.Equal(["1", "3", "0", "2"], list[1].GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("cells").GetProperty("column").GetString()));
        Assert.All(list[1].GetProperty("rows").EnumerateArray(), r => Assert.Equal("Solution", r.GetProperty("color").GetString()));

        // No placement: the table stays blank.
        var (_, none) = await Visualize("visualization=nqueensvisualization&solver=nqueensconstructive", "3");
        var tables = JsonDocument.Parse(none).RootElement.EnumerateArray().ToList();
        Assert.All(tables[^1].GetProperty("rows").EnumerateArray(), r => Assert.Equal("", r.GetProperty("cells").GetProperty("column").GetString()));
    }

    // ---- The picture ----

    [Theory]
    [MemberData(nameof(FramesCases))]
    public async Task Frames_EverySolver_HasTheBoardPayloadAndFramesThatAgreeWithTheAnswer(string solver, string instance, string answer) {
        string vis = BoardSolvers_Tests.VisualizationOf[solver];
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(["type", "payload", "frames"], root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Board", root.GetProperty("type").GetString());

        var payload = root.GetProperty("payload");
        bool sudoku = vis == "sudokuvisualization";
        Assert.Equal(["kind", "game", "size", .. sudoku ? new[] { "box" } : [], "labelsFrom", "clues"], payload.EnumerateObject().Select(p => p.Name));
        Assert.Equal("board", payload.GetProperty("kind").GetString());
        Assert.Equal(sudoku ? "sudoku" : "queens", payload.GetProperty("game").GetString());
        int size = payload.GetProperty("size").GetInt32();
        Assert.Equal(sudoku ? 9 : int.Parse(instance.Trim()), size);
        Assert.Equal(sudoku ? 1 : 0, payload.GetProperty("labelsFrom").GetInt32());
        if (sudoku) Assert.Equal(3, payload.GetProperty("box").GetInt32());
        var squares = Enumerable.Range(0, size).SelectMany(i => Enumerable.Range(0, size).Select(j => $"r{i}c{j}")).ToHashSet();
        var clues = new Dictionary<string, string>();
        foreach (var clue in payload.GetProperty("clues").EnumerateArray()) {
            Assert.Equal(["id", "r", "c", "value"], clue.EnumerateObject().Select(p => p.Name));
            string id = clue.GetProperty("id").GetString()!;
            Assert.Equal($"r{clue.GetProperty("r").GetInt32()}c{clue.GetProperty("c").GetInt32()}", id);
            clues[id] = clue.GetProperty("value").GetString()!;
        }
        if (!sudoku) Assert.Empty(clues);

        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        foreach (var f in frames) {
            Assert.Equal(["event", "caption", "focus", "values", "pieces", "cells", "links", .. f.TryGetProperty("ok", out _) ? new[] { "ok" } : []],
                f.EnumerateObject().Select(p => p.Name));
            string ev = f.GetProperty("event").GetString()!;
            Assert.False(string.IsNullOrWhiteSpace(f.GetProperty("caption").GetString()));
            Assert.All(f.GetProperty("focus").EnumerateArray(), id => Assert.Contains(id.GetString()!, squares));
            // Every clue is on the board in every frame, unchanged.
            var values = f.GetProperty("values").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
            Assert.All(clues, c => Assert.Equal(c.Value, values[c.Key]));
            Assert.All(values.Keys, id => Assert.Contains(id, squares));
            if (!sudoku) Assert.Empty(values);
            if (sudoku) Assert.Empty(f.GetProperty("pieces").EnumerateArray());
            foreach (var p in f.GetProperty("pieces").EnumerateArray()) {
                Assert.Equal(["id", "r", "c", "kind"], p.EnumerateObject().Select(x => x.Name));
                Assert.Equal("queen", p.GetProperty("kind").GetString());
                Assert.Contains(p.GetProperty("id").GetString()!, squares);
            }
            var cells = f.GetProperty("cells").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetProperty("state").GetString()!);
            foreach (var (id, state) in cells) {
                Assert.Contains(id, squares);
                Assert.Contains(state, Vocabulary);
                // A clue is fixed: never the solver's.
                if (clues.ContainsKey(id)) Assert.Equal("Rejected", state);
            }
            foreach (var l in f.GetProperty("links").EnumerateArray()) {
                Assert.Equal(["from", "to", "state"], l.EnumerateObject().Select(x => x.Name));
                Assert.Contains(l.GetProperty("from").GetString()!, squares);
                Assert.Contains(l.GetProperty("to").GetString()!, squares);
                Assert.Equal("Rejected", l.GetProperty("state").GetString());
                Assert.Equal("Rejected", cells[l.GetProperty("from").GetString()!]);
            }
            // The solvers are incremental: what they have placed is Solution, and only the square under test is highlighted.
            if (ev == "Try") Assert.True(cells.Count(c => c.Value == "ElementHighlight") <= 1);
            Assert.DoesNotContain(cells, c => c.Value is "Untraveled" or "Covered" or "Blocked");
        }

        // One Done frame, last, with the verifier's verdict, showing the answer or only the clues.
        var done = Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.Equal(done, frames[^1]);
        bool ok = done.GetProperty("ok").GetBoolean();
        Assert.Empty(done.GetProperty("links").EnumerateArray());
        var doneCells = done.GetProperty("cells").EnumerateObject().ToList();
        if (ok) {
            Assert.All(doneCells, c => Assert.Equal("Solution", c.Value.GetProperty("state").GetString()));
            if (sudoku) {
                Assert.Equal(81, done.GetProperty("values").EnumerateObject().Count());
                string grid = string.Join(";", Enumerable.Range(0, 9).Select(r => string.Join(",", Enumerable.Range(0, 9).Select(c => done.GetProperty("values").GetProperty($"r{r}c{c}").GetString()))));
                Assert.Equal(answer, grid);
            } else {
                var queens = done.GetProperty("pieces").EnumerateArray().Select(p => $"({p.GetProperty("r").GetInt32()},{p.GetProperty("c").GetInt32()})");
                Assert.Equal(answer, "{" + string.Join(",", queens) + "}");
            }
        } else {
            Assert.Equal("{}", answer);
            Assert.Empty(doneCells);
            Assert.Empty(done.GetProperty("pieces").EnumerateArray());
            Assert.Equal(clues.Keys.Order(StringComparer.Ordinal), done.GetProperty("values").EnumerateObject().Select(p => p.Name));
        }
    }

    [Fact]
    public async Task Frames_SudokuDefault_MatchesTheMockup() {
        var (_, body) = await Visualize("visualization=sudokuvisualization&format=frames", BoardSolvers_Tests.SudokuDefault);
        var root = JsonDocument.Parse(body).RootElement;
        var payload = root.GetProperty("payload");
        Assert.Equal("{\"id\":\"r0c3\",\"r\":0,\"c\":3,\"value\":\"1\"}", Readable(payload.GetProperty("clues")[0]));
        Assert.Equal(30, payload.GetProperty("clues").GetArrayLength());
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(151, frames.Count);
        // The mockup's frame 6: R1C6 being tried, the four digits before it Solution, the clues only as values.
        var f = frames[4];
        Assert.Equal("R1C6 can only hold 8. Try it.", f.GetProperty("caption").GetString());
        Assert.Equal("{\"r0c0\":{\"state\":\"Solution\"},\"r0c1\":{\"state\":\"Solution\"},\"r0c2\":{\"state\":\"Solution\"},\"r0c4\":{\"state\":\"Solution\"},\"r0c5\":{\"state\":\"ElementHighlight\"}}",
            Readable(f.GetProperty("cells")));
        Assert.Equal(["6", "4", "5", "1", "7", "8", "2", null, "3"],
            Enumerable.Range(0, 9).Select(c => f.GetProperty("values").TryGetProperty($"r0c{c}", out var v) ? v.GetString() : null));
        Assert.Equal("[\"r0c5\"]", Readable(f.GetProperty("focus")));
        Assert.Equal("[]", Readable(f.GetProperty("links")));

        var done = frames[^1];
        Assert.True(done.GetProperty("ok").GetBoolean());
        Assert.Equal("Every square is filled and no row, column or box repeats a digit. Solved after 429 tries. (396 not shown)", done.GetProperty("caption").GetString());
        Assert.Equal(51, done.GetProperty("cells").EnumerateObject().Count());
    }

    [Fact]
    public async Task Frames_NQueensFour_MatchesTheMockup() {
        var (_, body) = await Visualize("visualization=nqueensvisualization&format=frames", "4");
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal("{\"kind\":\"board\",\"game\":\"queens\",\"size\":4,\"labelsFrom\":0,\"clues\":[]}", Readable(root.GetProperty("payload")));
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(13, frames.Count);
        Assert.Equal(
            "{\"event\":\"Try\",\"caption\":\"Row 1: columns 0 and 1 are attacked (0 along its column by the queen at (0,0)). Place the queen at column 2.\","
            + "\"focus\":[\"r1c2\"],\"values\":{},"
            + "\"pieces\":[{\"id\":\"r0c0\",\"r\":0,\"c\":0,\"kind\":\"queen\"},{\"id\":\"r1c2\",\"r\":1,\"c\":2,\"kind\":\"queen\"}],"
            + "\"cells\":{\"r0c0\":{\"state\":\"Solution\"},\"r1c0\":{\"state\":\"Rejected\"},\"r1c1\":{\"state\":\"Rejected\"},\"r1c2\":{\"state\":\"ElementHighlight\"}},"
            + "\"links\":[{\"from\":\"r1c0\",\"to\":\"r0c0\",\"state\":\"Rejected\"},{\"from\":\"r1c1\",\"to\":\"r0c0\",\"state\":\"Rejected\"}]}",
            Readable(frames[1]));
        Assert.Equal(
            "{\"event\":\"Reject\",\"caption\":\"Row 2: every column is attacked, so back up and move the queen in row 1.\","
            + "\"focus\":[\"r2c0\",\"r2c1\",\"r2c2\",\"r2c3\",\"r1c2\"],\"values\":{},"
            + "\"pieces\":[{\"id\":\"r0c0\",\"r\":0,\"c\":0,\"kind\":\"queen\"},{\"id\":\"r1c2\",\"r\":1,\"c\":2,\"kind\":\"queen\"}],"
            + "\"cells\":{\"r0c0\":{\"state\":\"Solution\"},\"r1c2\":{\"state\":\"Rejected\"},\"r2c0\":{\"state\":\"Rejected\"},\"r2c1\":{\"state\":\"Rejected\"},"
            + "\"r2c2\":{\"state\":\"Rejected\"},\"r2c3\":{\"state\":\"Rejected\"}},"
            + "\"links\":[{\"from\":\"r2c0\",\"to\":\"r0c0\",\"state\":\"Rejected\"},{\"from\":\"r2c1\",\"to\":\"r1c2\",\"state\":\"Rejected\"},"
            + "{\"from\":\"r2c2\",\"to\":\"r0c0\",\"state\":\"Rejected\"},{\"from\":\"r2c3\",\"to\":\"r1c2\",\"state\":\"Rejected\"}]}",
            Readable(frames[2]));
        Assert.Equal(
            "{\"event\":\"Done\",\"caption\":\"All 4 queens are placed and none attacks another: {(0,1),(1,3),(2,0),(3,2)}. Found after 8 placements.\","
            + "\"focus\":[],\"values\":{},"
            + "\"pieces\":[{\"id\":\"r0c1\",\"r\":0,\"c\":1,\"kind\":\"queen\"},{\"id\":\"r1c3\",\"r\":1,\"c\":3,\"kind\":\"queen\"},"
            + "{\"id\":\"r2c0\",\"r\":2,\"c\":0,\"kind\":\"queen\"},{\"id\":\"r3c2\",\"r\":3,\"c\":2,\"kind\":\"queen\"}],"
            + "\"cells\":{\"r0c1\":{\"state\":\"Solution\"},\"r1c3\":{\"state\":\"Solution\"},\"r2c0\":{\"state\":\"Solution\"},\"r3c2\":{\"state\":\"Solution\"}},"
            + "\"links\":[],\"ok\":true}",
            Readable(frames[^1]));
    }

    [Fact]
    public async Task Frames_SudokuClashingClues_ShowTheClashLinked() {
        string instance = "1,0,0,1,0,0,0,0,0;" + string.Join(";", Enumerable.Repeat("0,0,0,0,0,0,0,0,0", 8));
        var (_, body) = await Visualize("visualization=sudokuvisualization&format=frames", instance);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(["Reject", "Done"], frames.Select(f => f.GetProperty("event").GetString()));
        Assert.Equal("{\"r0c0\":{\"state\":\"Rejected\"},\"r0c3\":{\"state\":\"Rejected\"}}", Readable(frames[0].GetProperty("cells")));
        Assert.Equal("[{\"from\":\"r0c3\",\"to\":\"r0c0\",\"state\":\"Rejected\"}]", Readable(frames[0].GetProperty("links")));
        Assert.False(frames[1].GetProperty("ok").GetBoolean());
    }

    [Theory]
    [InlineData("sudokuvisualization", "1,0,0,0;0,0,3,0;0,4,0,0;0,0,0,2")]
    [InlineData("sudokuvisualization", "0,0,0,1,0,0,2,0,3;0,2,0,0,4,0,5,0,6")]
    [InlineData("nqueensvisualization", "-1")]
    [InlineData("nqueensvisualization", "four")]
    public async Task Frames_UnreadableInstance_IsA400_NotAPicture(string vis, string instance) {
        var (status, body) = await Visualize($"visualization={vis}&format=frames", instance);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("instance_parse_error", body);
    }

    // #799 on the real endpoint: an entry that is not a (row,col) pair is not a queen.
    [Theory]
    [InlineData("{1,2,3,4}")]
    [InlineData("{(a,b),(1,3),(2,0),(3,2)}")]
    public async Task Verify_NQueensEntriesThatAreNotPairs_AreFalse_NotA500(string certificate) {
        var content = new StringContent(JsonSerializer.Serialize(new { ProblemInstance = "4", Certificate = certificate }), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/verify?verifier=nqueensverifier", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"False\"", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    // #798 on the real endpoint: a negative n is a 400 for both solvers.
    [Theory]
    [InlineData("nqueensbacktracking")]
    [InlineData("nqueensconstructive")]
    public async Task Solve_NegativeN_IsA400(string solver) {
        var content = new StringContent(JsonSerializer.Serialize("-1"), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/solve?solver={solver}", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("instance_parse_error", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}

// Stopped runs, under the 0.05 s budget of TinyBudgetAppFactory.
[Collection(TimingCollection.Name)]
public class BoardFrames_StoppedRun_Endpoint_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public BoardFrames_StoppedRun_Endpoint_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    // A puzzle made to defeat cell-by-cell backtracking (its first row is empty and the digits it needs come late): millions
    // of tries, far past 0.05 s.
    internal const string SlowSudoku =
        "0,0,0,0,0,0,0,0,0;0,0,0,0,0,3,0,8,5;0,0,1,0,2,0,0,0,0;0,0,0,5,0,7,0,0,0;0,0,4,0,0,0,1,0,0;0,9,0,0,0,0,0,0,0;5,0,0,0,0,0,0,7,3;0,0,2,0,1,0,0,0,0;0,0,0,0,4,0,0,0,9";

    private async Task<List<JsonElement>> StoppedFrames(string query, string instance) {
        var sw = Stopwatch.StartNew();
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?{query}&format=frames", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"took {sw.Elapsed}");
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal("Board", root.GetProperty("type").GetString());
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count >= 2);
        var done = frames[^1];
        Assert.Equal("Done", done.GetProperty("event").GetString());
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.Matches(@"^Stopped after 0\.05 s and [\d,]+ steps?; the solver had not finished\.", done.GetProperty("caption").GetString());
        Assert.Empty(done.GetProperty("cells").EnumerateObject());
        Assert.Empty(done.GetProperty("pieces").EnumerateArray());
        Assert.Empty(done.GetProperty("links").EnumerateArray());
        Assert.Empty(done.GetProperty("focus").EnumerateArray());
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        return frames;
    }

    [Fact]
    public async Task Frames_StoppedSudoku_EndsWithTheCluesOnly_NotOk() {
        var frames = await StoppedFrames("visualization=sudokuvisualization", SlowSudoku);
        var done = frames[^1];
        var clues = SlowSudoku.Split(';').SelectMany((row, r) => row.Split(',').Select((v, c) => (Id: $"r{r}c{c}", V: v))).Where(x => x.V != "0").ToList();
        Assert.Equal(clues.Select(x => $"{x.Id}={x.V}").Order(StringComparer.Ordinal),
            done.GetProperty("values").EnumerateObject().Select(p => $"{p.Name}={p.Value.GetString()}"));
        // The steps before it are the solver's tries.
        Assert.Equal("Try", frames[0].GetProperty("event").GetString());
    }

    [Fact]
    public async Task Frames_StoppedNQueens_EndsWithAnEmptyBoard_NotOk() {
        var frames = await StoppedFrames("visualization=nqueensvisualization&solver=nqueensbacktracking", "30");
        Assert.Empty(frames[^1].GetProperty("values").EnumerateObject());
    }
}
