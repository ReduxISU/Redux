using System.Net;
using System.Text.Json;
using Xunit;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_MAXCUT;
using API.Problems.NPComplete.NPC_MAXCUT.Solvers;
using API.Problems.NPComplete.NPC_MAXCUT.Verifiers;
using API.Problems.NPComplete.NPC_MAXCUT.Visualizations;

namespace redux_tests;
#pragma warning disable CS1591

public class MAXCUT_Tests {
    // ----- Instantiation ----- //

    [Fact]
    public void MAXCUT_Default_Instantiation() {
        MAXCUT problem = new MAXCUT();
        Assert.Equal(MAXCUT._defaultInstance, problem.instance);
        Assert.Equal(MAXCUT._defaultInstance, problem.defaultInstance);
        Assert.Equal(5, problem.nodes.Count);
        Assert.Equal(6, problem.edges.Count);
    }

    [Fact]
    public void MAXCUT_Custom_Instantiation() {
        string instance = "({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})";
        MAXCUT problem = new MAXCUT(instance);
        Assert.Equal(instance, problem.instance);
        Assert.Equal(3, problem.nodes.Count);
        Assert.Equal(3, problem.edges.Count);
    }

    [Fact]
    public void MAXCUT_Two_Node_Graph() {
        string instance = "({1,2},{({1,2},7)})";
        MAXCUT problem = new MAXCUT(instance);
        Assert.Equal(2, problem.nodes.Count);
        Assert.Single(problem.edges);
    }

    // ----- Solver ----- //

    [Theory]
    [InlineData("({1,2,3,4,5},{({2,1},5),({1,3},4),({2,3},2),({3,5},1),({2,4},4),({4,5},2)})", 15)]
    [InlineData("({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})", 5)]
    [InlineData("({1,2},{({1,2},7)})", 7)]
    public void MAXCUT_Solver_Returns_Correct_Weight(string instance, int expectedWeight) {
        MAXCUT problem = new MAXCUT(instance);
        MaxCutSolver solver = new MaxCutSolver();
        string solution = solver.solve(problem);
        int actualWeight = ComputeCutWeight(problem, solution);
        Assert.Equal(expectedWeight, actualWeight);
    }

    [Fact]
    public void MAXCUT_Solver_Default_Instance_Weight_Is_15() {
        MAXCUT problem = new MAXCUT();
        string solution = new MaxCutSolver().solve(problem);
        Assert.Equal(15, ComputeCutWeight(problem, solution));
    }

    [Fact]
    public void MAXCUT_Solver_Single_Node_Returns_Empty() {
        string instance = "({1},{})";
        MAXCUT problem = new MAXCUT(instance);
        string solution = new MaxCutSolver().solve(problem);
        Assert.Equal("{}", solution);
    }

    [Fact]
    public void MAXCUT_Solver_Output_Passes_Verifier() {
        string[] instances = {
            MAXCUT._defaultInstance,
            "({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})",
            "({1,2},{({1,2},7)})",
            "({A,B,C,D},{({A,B},4),({B,C},3),({C,D},4),({D,A},3)})"
        };
        foreach (string inst in instances) {
            MAXCUT problem = new MAXCUT(inst);
            string solution = new MaxCutSolver().solve(problem);
            Assert.True(new MaxCutVerifier().verify(problem, solution), $"Solver output failed verifier for: {inst}");
        }
    }

    // ----- Verifier ----- //

    [Theory]
    // Default 5-node graph: S={1,4} gives cut weight 15 = max cut.
    [InlineData("({1,2,3,4,5},{({2,1},5),({1,3},4),({2,3},2),({3,5},1),({2,4},4),({4,5},2)})", "{1,4}", true)]
    // Complement S={2,3,5} also gives weight 15.
    [InlineData("({1,2,3,4,5},{({2,1},5),({1,3},4),({2,3},2),({3,5},1),({2,4},4),({4,5},2)})", "{2,3,5}", true)]
    // 3-node: max cut = 5, S={3} gives (1-3,3)+(2-3,2)=5.
    [InlineData("({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})", "{3}", true)]
    // S={1,2} also gives weight 5 for 3-node graph.
    [InlineData("({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})", "{1,2}", true)]
    // 2-node: max cut = 7.
    [InlineData("({1,2},{({1,2},7)})", "{1}", true)]
    public void MAXCUT_Verifier_Accepts_Valid_Maximum_Cut(string instance, string certificate, bool expected) {
        MAXCUT problem = new MAXCUT(instance);
        MaxCutVerifier verifier = new MaxCutVerifier();
        Assert.Equal(expected, verifier.verify(problem, certificate));
    }

    [Theory]
    // S={1,2} gives cut weight 10, not max cut (15).
    [InlineData("({1,2,3,4,5},{({2,1},5),({1,3},4),({2,3},2),({3,5},1),({2,4},4),({4,5},2)})", "{1,2}")]
    // S={2} for 3-node graph gives weight 3, not max cut (5).
    [InlineData("({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})", "{2}")]
    public void MAXCUT_Verifier_Rejects_Non_Maximum_Cut(string instance, string certificate) {
        MAXCUT problem = new MAXCUT(instance);
        MaxCutVerifier verifier = new MaxCutVerifier();
        Assert.False(verifier.verify(problem, certificate));
    }

    [Fact]
    public void MAXCUT_Verifier_Rejects_Node_Not_In_Graph() {
        MAXCUT problem = new MAXCUT("({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})");
        MaxCutVerifier verifier = new MaxCutVerifier();
        Assert.False(verifier.verify(problem, "{99}"));
    }

    [Fact]
    public void MAXCUT_Verifier_Rejects_Full_Node_Set() {
        MAXCUT problem = new MAXCUT("({1,2},{({1,2},7)})");
        MaxCutVerifier verifier = new MaxCutVerifier();
        Assert.False(verifier.verify(problem, "{1,2}"));
    }

    [Fact]
    public void MAXCUT_Verifier_Rejects_Empty_Certificate() {
        MAXCUT problem = new MAXCUT();
        MaxCutVerifier verifier = new MaxCutVerifier();
        Assert.False(verifier.verify(problem, "{}"));
    }

    // ----- Visualization ----- //

    [Fact]
    public void MAXCUT_Visualization_Returns_Graph() {
        MAXCUT problem = new MAXCUT();
        MaxCutVisualization viz = new MaxCutVisualization();
        var graph = (API_GraphJSON)viz.visualize(problem);
        Assert.Equal(5, graph.nodes.Count);
        Assert.Equal(6, graph.links.Count);
    }

    [Fact]
    public void MAXCUT_SolvedVisualization_Colors_Crossing_Edges() {
        // ({1,2,3},{({1,2},1),({2,3},2),({1,3},3)}), S={3}: crossing edges are (1-3) and (2-3).
        string instance = "({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})";
        MAXCUT problem = new MAXCUT(instance);
        MaxCutVisualization viz = new MaxCutVisualization();
        var graph = (API_GraphJSON)viz.SolvedVisualization(problem, "{3}");

        var link23 = graph.links.FirstOrDefault(l =>
            (l.source == "2" && l.target == "3") || (l.source == "3" && l.target == "2"));
        Assert.NotNull(link23);
        Assert.Equal("Solution", link23!.color);

        // Edge (1-2) is within T and should not be colored.
        var link12 = graph.links.FirstOrDefault(l =>
            (l.source == "1" && l.target == "2") || (l.source == "2" && l.target == "1"));
        Assert.NotNull(link12);
        Assert.NotEqual("Solution", link12!.color);
    }

    [Fact]
    public void MAXCUT_SolvedVisualization_Colors_S_Nodes() {
        string instance = "({1,2,3},{({1,2},1),({2,3},2),({1,3},3)})";
        MAXCUT problem = new MAXCUT(instance);
        MaxCutVisualization viz = new MaxCutVisualization();
        var graph = (API_GraphJSON)viz.SolvedVisualization(problem, "{3}");

        var node3 = graph.nodes.FirstOrDefault(n => n.name == "3");
        Assert.NotNull(node3);
        Assert.Equal("Solution", node3!.color);
    }

    [Fact]
    public void MAXCUT_SolvedVisualization_Empty_Solution_Returns_Plain_Graph() {
        MAXCUT problem = new MAXCUT();
        MaxCutVisualization viz = new MaxCutVisualization();
        var graph = (API_GraphJSON)viz.SolvedVisualization(problem, "{}");
        Assert.All(graph.nodes, n => Assert.NotEqual("Solution", n.color));
        Assert.All(graph.links, l => Assert.NotEqual("Solution", l.color));
    }

    // ----- Helper ----- //

    private static int ComputeCutWeight(MAXCUT problem, string certificate) {
        if (string.IsNullOrWhiteSpace(certificate) || certificate.Trim() == "{}") return 0;

        HashSet<string> sNodes = certificate.Trim()
            .TrimStart('{').TrimEnd('}')
            .Split(',')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToHashSet();

        return MaxCutSolver.CutWeight(problem.edges, sNodes);
    }
}

// MaxCutQAOASolver against a fake quantum service: no test here touches the network.
[Collection(QuantumServiceCollection.Name)]
public class MAXCUT_QAOA_Tests {
    [Fact]
    public void MAXCUT_QAOA_Sends_Nodes_And_Weighted_Edges() {
        string? body = null;
        using var service = FakeQuantumService.Responding(request => {
            body = request.Content!.ReadAsStringAsync().Result;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"answer\":\"{1,4}\"}") };
        });

        new MaxCutQAOASolver().solve(new MAXCUT());

        using JsonDocument doc = JsonDocument.Parse(body!);
        Assert.Equal(new[] { "1", "2", "3", "4", "5" },
            doc.RootElement.GetProperty("nodes").EnumerateArray().Select(n => n.GetString()));
        JsonElement first = doc.RootElement.GetProperty("edges")[0];
        Assert.Equal("2", first[0].GetString());
        Assert.Equal("1", first[1].GetString());
        Assert.Equal(5, first[2].GetInt32());
        Assert.Equal(6, doc.RootElement.GetProperty("edges").GetArrayLength());
    }

    [Theory]
    [InlineData("{1,4}")]
    [InlineData("{2,3,5}")]
    public void MAXCUT_QAOA_Passes_The_Answer_On_And_It_Verifies(string answer) {
        using var service = FakeQuantumService.Answering(JsonSerializer.Serialize(new { answer, cut_weight = 15 }));
        MAXCUT problem = new MAXCUT();

        string solution = new MaxCutQAOASolver().solve(problem);

        Assert.Equal(answer, solution);
        Assert.True(new MaxCutVerifier().verify(problem, solution));
    }

    // QAOA is approximate: a valid but non-maximum cut is still passed on; the verifier rejects it.
    [Fact]
    public void MAXCUT_QAOA_Passes_A_NonMaximum_Cut_On() {
        using var service = FakeQuantumService.Answering("{\"answer\":\"{1}\"}");
        MAXCUT problem = new MAXCUT();

        string solution = new MaxCutQAOASolver().solve(problem);

        Assert.Equal("{1}", solution);
        Assert.False(new MaxCutVerifier().verify(problem, solution));
    }

    // Anything but a non-empty proper subset of the nodes is no answer (#625).
    [Theory]
    [InlineData("{}")]
    [InlineData("{1,2,3,4,5}")]
    [InlineData("{1,9}")]
    [InlineData("{1,1}")]
    [InlineData("1,4")]
    public void MAXCUT_QAOA_Malformed_Answer_Throws_SolverUnavailable(string answer) {
        using var service = FakeQuantumService.Answering(JsonSerializer.Serialize(new { answer }));

        var ex = Assert.Throws<SolverUnavailableException>(() => new MaxCutQAOASolver().solve(new MAXCUT()));

        Assert.Contains("not a non-empty proper subset", ex.Message);
    }

    [Fact]
    public void MAXCUT_QAOA_Error_Reply_Throws_SolverUnavailable() {
        using var service = FakeQuantumService.Answering("{\"error\":\"need at least 2 nodes to form a cut\"}");

        Assert.Throws<SolverUnavailableException>(() => new MaxCutQAOASolver().solve(new MAXCUT("({1},{})")));
    }

    [Fact]
    public void MAXCUT_QAOA_Timer_Expired_Returns_TimedOut_Without_Asking() {
        using var service = FakeQuantumService.Answering("{\"answer\":\"{1,4}\"}");
        var solver = new MaxCutQAOASolver { timerHasExpired = true };

        Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(new MAXCUT()));
        Assert.Equal(0, service.Handler.Requests);
    }
}

// MaxCutQAOAVisualization against a fake quantum service answering with a real reply for a triangle (from the
// service's /maxcut-quantum). No test here touches the network.
[Collection(QuantumServiceCollection.Name)]
public class MAXCUT_QAOA_Visualization_Tests {
    private const string Triangle = "({1,2,3},{({1,2},4),({2,3},2),({1,3},3)})";

    private const string TriangleQasm = """
        OPENQASM 2.0;
        include "qelib1.inc";
        qreg q[3];
        creg meas[3];
        h q[0];
        h q[1];
        h q[2];
        cx q[0],q[1];
        rz(4.435217005656659) q[1];
        cx q[0],q[1];
        cx q[0],q[2];
        rz(3.326412754242494) q[2];
        cx q[0],q[2];
        cx q[1],q[2];
        rz(2.2176085028283294) q[2];
        cx q[1],q[2];
        rx(2.2549578148204157) q[0];
        rx(2.2549578148204157) q[1];
        rx(2.2549578148204157) q[2];
        cx q[0],q[1];
        rz(4.2976071195728105) q[1];
        cx q[0],q[1];
        cx q[0],q[2];
        rz(3.223205339679608) q[2];
        cx q[0],q[2];
        cx q[1],q[2];
        rz(2.1488035597864052) q[2];
        cx q[1],q[2];
        rx(5.893975021520123) q[0];
        rx(5.893975021520123) q[1];
        rx(5.893975021520123) q[2];
        barrier q[0],q[1],q[2];
        measure q[0] -> meas[0];
        measure q[1] -> meas[1];
        measure q[2] -> meas[2];
        """;

    private static string TriangleReply() => JsonSerializer.Serialize(new {
        answer = "{1}",
        cut_weight = 7,
        answer_bitstring = "100",
        seed = 6680,
        betas = new[] { 1.1274789074102078, 2.9469875107600614 },
        gammas = new[] { 4.435217005656659, 4.2976071195728105 },
        qasm = TriangleQasm,
    });

    private static JsonElement D3(API_JSON circuit) {
        var qc = Assert.IsType<API_QUANTUMCIRCUIT>(circuit);
        Assert.Equal(QuantumCircuitFormat.D3, qc.format);
        return qc.d3!.Value;
    }

    private static List<JsonElement> Gates(JsonElement d3) => d3.GetProperty("gates").EnumerateArray().ToList();

    [Fact]
    public void MAXCUT_QAOA_Visualization_Names_Wires_After_Nodes_And_Drops_The_Barrier() {
        using var service = FakeQuantumService.Answering(TriangleReply());
        JsonElement d3 = D3(new MaxCutQAOAVisualization().SolvedVisualization(new MAXCUT(Triangle), "{1}"));

        Assert.Equal(new[] { "1", "2", "3" }, d3.GetProperty("qubits").EnumerateArray().Select(q => q.GetString()));
        var counts = Gates(d3).GroupBy(g => g.GetProperty("type").GetString()!).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(new Dictionary<string, int> { ["h"] = 3, ["cx"] = 12, ["rz"] = 6, ["rx"] = 6, ["m"] = 3 }, counts);
        Assert.All(Gates(d3), g => Assert.All(g.GetProperty("targets").EnumerateArray(),
            t => Assert.Contains(t.GetString(), new[] { "1", "2", "3" })));
    }

    [Fact]
    public void MAXCUT_QAOA_Visualization_Rounds_Angles_For_Display() {
        using var service = FakeQuantumService.Answering(TriangleReply());
        JsonElement d3 = D3(new MaxCutQAOAVisualization().visualize(new MAXCUT(Triangle)));

        var angles = Gates(d3).Where(g => g.TryGetProperty("params", out var p) && p.ValueKind == JsonValueKind.Array)
            .Select(g => g.GetProperty("params")[0].GetDouble()).ToList();
        Assert.Equal(12, angles.Count);
        Assert.Contains(4.44, angles);
        Assert.All(angles, a => Assert.Equal(Math.Round(a, 2), a));
    }

    [Fact]
    public void MAXCUT_QAOA_Visualization_Overlays_Each_Cost_And_Mixer_Layer() {
        using var service = FakeQuantumService.Answering(TriangleReply());
        JsonElement d3 = D3(new MaxCutQAOAVisualization().visualize(new MAXCUT(Triangle)));

        var overlays = d3.GetProperty("overlays").EnumerateArray().ToList();
        Assert.Equal(new[] { "U_C(γ1)", "U_B(β1)", "U_C(γ2)", "U_B(β2)" }, overlays.Select(o => o.GetProperty("label").GetString()));

        // Every cost gate falls inside a cost overlay and every mixer gate inside a mixer overlay, and the layers
        // come in order without overlapping.
        bool Inside(JsonElement gate, string idPrefix) => overlays.Any(o =>
            o.GetProperty("id").GetString()!.StartsWith(idPrefix) &&
            gate.GetProperty("time").GetDouble() >= o.GetProperty("timeStart").GetInt32() &&
            gate.GetProperty("time").GetDouble() <= o.GetProperty("timeEnd").GetInt32());
        foreach (JsonElement gate in Gates(d3)) {
            string type = gate.GetProperty("type").GetString()!;
            if (type is "cx" or "rz") Assert.True(Inside(gate, "cost"), $"{type} at {gate.GetProperty("time")} is outside every cost overlay");
            if (type == "rx") Assert.True(Inside(gate, "mixer"), $"rx at {gate.GetProperty("time")} is outside every mixer overlay");
            if (type is "h" or "m") Assert.False(Inside(gate, "cost") || Inside(gate, "mixer"));
        }
        for (int i = 1; i < overlays.Count; i++)
            Assert.True(overlays[i].GetProperty("timeStart").GetInt32() > overlays[i - 1].GetProperty("timeEnd").GetInt32());
    }

    [Fact]
    public void MAXCUT_QAOA_SolvedVisualization_Shows_The_Measured_Cut() {
        using var service = FakeQuantumService.Answering(TriangleReply());
        var circuit = Assert.IsType<API_QUANTUMCIRCUIT>(new MaxCutQAOAVisualization().SolvedVisualization(new MAXCUT(Triangle), "{1}"));
        JsonElement metadata = circuit.d3!.Value.GetProperty("metadata");

        Assert.Equal("{1}", circuit.solution);
        Assert.Equal("{1}", metadata.GetProperty("solution").GetString());
        Assert.Equal("100", metadata.GetProperty("solutionBits").GetString());
        Assert.Equal("7", metadata.GetProperty("Cut weight").GetString());
        Assert.Equal("4.435, 4.298", metadata.GetProperty("γ (cost angles)").GetString());
        Assert.Equal("1.127, 2.947", metadata.GetProperty("β (mixer angles)").GetString());
        Assert.Equal(2, metadata.GetProperty("Layers (p)").GetInt32());
    }

    [Fact]
    public void MAXCUT_QAOA_Visualization_Plain_Picture_Has_No_Solution() {
        using var service = FakeQuantumService.Answering(TriangleReply());
        API_QUANTUMCIRCUIT circuit = new MaxCutQAOAVisualization().visualize(new MAXCUT(Triangle));

        Assert.Null(circuit.solution);
        Assert.False(circuit.d3!.Value.GetProperty("metadata").TryGetProperty("solution", out _));
        Assert.NotEmpty(Gates(circuit.d3!.Value));
    }

    [Fact]
    public void MAXCUT_QAOA_Visualization_No_Weighted_Edges_Draws_Wires_And_A_Note() {
        using var service = FakeQuantumService.Answering("{\"answer\":\"{a}\",\"cut_weight\":0,\"answer_bitstring\":\"10\",\"seed\":6680}");
        JsonElement d3 = D3(new MaxCutQAOAVisualization().SolvedVisualization(new MAXCUT("({a,b},{})"), "{a}"));

        Assert.Equal(new[] { "a", "b" }, d3.GetProperty("qubits").EnumerateArray().Select(q => q.GetString()));
        Assert.Empty(Gates(d3));
        Assert.Contains("no weighted edges", d3.GetProperty("metadata").GetProperty("Note").GetString());
    }

    [Fact]
    public void MAXCUT_QAOA_Visualization_Error_Reply_Throws_SolverUnavailable() {
        using var service = FakeQuantumService.Answering("{\"error\":\"too many nodes (17), limit is 16\"}");
        Assert.Throws<SolverUnavailableException>(() => new MaxCutQAOAVisualization().visualize(new MAXCUT(Triangle)));
    }

    [Fact]
    public void MAXCUT_QAOA_Visualization_Service_Down_Throws_SolverUnavailable() {
        using var service = FakeQuantumService.Down();
        var ex = Assert.Throws<SolverUnavailableException>(() => new MaxCutQAOAVisualization().visualize(new MAXCUT(Triangle)));
        Assert.Equal("MaxCutQAOASolver", ex.Solver);
    }
}
