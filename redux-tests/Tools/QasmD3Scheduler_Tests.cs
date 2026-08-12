using System.Collections.Generic;
using System.Linq;
using Xunit;
using API.Tools;

namespace redux_tests;
#pragma warning disable CS1591

public class QasmD3Scheduler_Tests
{

    // --- ParseQasm ---

    [Fact]
    public void ParseQasm_HeaderQregCreg_ProducesQubitsAndClassicalNoOps()
    {
        string qasm = "OPENQASM 2.0;\ninclude \"qelib1.inc\";\nqreg q[2];\ncreg c[1];\n";

        var (qubits, classical, ops) = QasmD3Scheduler.ParseQasm(qasm);

        Assert.Equal(new List<string> { "q0", "q1" }, qubits);
        Assert.Equal(new List<string> { "c0" }, classical);
        Assert.Empty(ops);
    }

    [Fact]
    public void ParseQasm_PlainGateLine_ProducesSingleOpWithTypeAndTarget()
    {
        string qasm = "qreg q[1];\nh q[0];\n";

        var (_, _, ops) = QasmD3Scheduler.ParseQasm(qasm);

        var op = Assert.Single(ops);
        Assert.Equal("h", op.Type);
        Assert.Equal(new[] { "q0" }, op.Targets);
    }

    [Fact]
    public void ParseQasm_MeasureLine_ProducesMeasureOpWithClassical()
    {
        string qasm = "qreg q[1];\ncreg c[1];\nmeasure q[0] -> c[0];\n";

        var (_, _, ops) = QasmD3Scheduler.ParseQasm(qasm);

        var op = Assert.Single(ops);
        Assert.Equal("m", op.Type);
        Assert.Equal(new[] { "q0" }, op.Targets);
        Assert.Equal(new[] { "c0" }, op.Classical);
    }

    [Fact]
    public void ParseQasm_ParametrizedGate_ParsesParamValue()
    {
        string qasm = "qreg q[1];\nrz(0.5) q[0];\n";

        var (_, _, ops) = QasmD3Scheduler.ParseQasm(qasm);

        var op = Assert.Single(ops);
        Assert.Equal("rz", op.Type);
        Assert.NotNull(op.Params);
        Assert.Contains(0.5, op.Params!);
    }

    [Fact]
    public void ParseQasm_MultiTargetGate_ProducesBothTargets()
    {
        string qasm = "qreg q[2];\ncx q[0],q[1];\n";

        var (_, _, ops) = QasmD3Scheduler.ParseQasm(qasm);

        var op = Assert.Single(ops);
        Assert.Equal("cx", op.Type);
        Assert.Contains("q0", op.Targets);
        Assert.Contains("q1", op.Targets);
        Assert.Equal(2, op.Targets.Length);
    }

    [Fact]
    public void ParseQasm_CommentsAndHeaders_AreSkippedWithoutProducingOpsOrThrowing()
    {
        string qasm = "// this is a comment\nOPENQASM 2.0;\ninclude \"qelib1.inc\";\n// another comment\nqreg q[1];\nh q[0];\n";

        var (qubits, _, ops) = QasmD3Scheduler.ParseQasm(qasm);

        Assert.Equal(new List<string> { "q0" }, qubits);
        var op = Assert.Single(ops);
        Assert.Equal("h", op.Type);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\n  \n")]
    public void ParseQasm_EmptyOrWhitespaceInput_ProducesEmptyListsWithoutThrowing(string qasm)
    {
        var (qubits, classical, ops) = QasmD3Scheduler.ParseQasm(qasm);

        Assert.Empty(qubits);
        Assert.Empty(classical);
        Assert.Empty(ops);
    }

    // --- ScheduleAsap ---

    [Fact]
    public void ScheduleAsap_DifferentQubitsSameType_ShareSameTimeSlot()
    {
        var qasm = "qreg q[2];\nh q[0];\nh q[1];\n";
        var (_, _, ops) = QasmD3Scheduler.ParseQasm(qasm);

        var gates = QasmD3Scheduler.ScheduleAsap(ops);

        Assert.Equal(2, gates.Count);
        Assert.Equal(gates[0].time, gates[1].time);
    }

    [Fact]
    public void ScheduleAsap_SameQubitConflict_ForcesIncreasingTimeSlots()
    {
        var qasm = "qreg q[1];\nh q[0];\nx q[0];\n";
        var (_, _, ops) = QasmD3Scheduler.ParseQasm(qasm);

        var gates = QasmD3Scheduler.ScheduleAsap(ops);

        Assert.Equal(2, gates.Count);
        Assert.True(gates[1].time > gates[0].time);
    }

    [Fact]
    public void ScheduleAsap_TypeMismatchWithoutResourceOverlap_StillForcesNewLayer()
    {
        // Two ops on disjoint qubits, different gate types: no resource conflict,
        // but the type-mismatch rule should still force a new layer.
        var ops = new List<QasmOp>
        {
            new QasmOp { Id = "h0", Type = "h", Targets = new[] { "q0" } },
            new QasmOp { Id = "x1", Type = "x", Targets = new[] { "q1" } },
        };

        var gates = QasmD3Scheduler.ScheduleAsap(ops);

        Assert.Equal(2, gates.Count);
        Assert.NotEqual(gates[0].time, gates[1].time);
        Assert.True(gates[1].time > gates[0].time);
    }

    [Fact]
    public void ScheduleAsap_MultipleMeasurementsInSameLayer_GetDistinctCloseIncreasingOffsets()
    {
        var ops = new List<QasmOp>
        {
            new QasmOp { Id = "m0", Type = "m", Targets = new[] { "q0" }, Classical = new[] { "c0" } },
            new QasmOp { Id = "m1", Type = "m", Targets = new[] { "q1" }, Classical = new[] { "c1" } },
            new QasmOp { Id = "m2", Type = "m", Targets = new[] { "q2" }, Classical = new[] { "c2" } },
        };

        var gates = QasmD3Scheduler.ScheduleAsap(ops);

        Assert.Equal(3, gates.Count);

        // All measurements land in the same nominal layer (no resource/type conflicts),
        // but each gets a distinct, strictly-increasing offset rather than an identical time.
        var times = gates.Select(g => g.time).ToList();
        Assert.Equal(times.Distinct().Count(), times.Count);
        Assert.True(times[0] < times[1]);
        Assert.True(times[1] < times[2]);

        // The offsets should be small (epsilon-spacing), keeping them within the same nominal layer.
        double nominalLayer = System.Math.Floor(times[0]);
        foreach (var t in times)
            Assert.True(t - nominalLayer < 1.0);
    }

    // --- NormalizeQubit ---

    [Theory]
    [InlineData("q[0]", "q0")]
    [InlineData("q0", "q0")]
    [InlineData("q[1];", "q1")]
    [InlineData(" q[2] ", "q2")]
    public void NormalizeQubit_HandlesBracketsAndTrimming(string input, string expected)
    {
        string result = QasmD3Scheduler.NormalizeQubit(input);

        Assert.Equal(expected, result);
    }
}
