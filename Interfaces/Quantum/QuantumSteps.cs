using System.Text.Json;
using API.Interfaces.Steps;

namespace API.Interfaces.Quantum;

/// <summary>
/// Steps the quantum-algorithm solvers share (see <c>QuantumCircuitFrames</c> for how they are drawn).
/// <list type="bullet">
/// <item><see cref="Columns"/>: one Update per column of the instance's circuit (applying gates has no verdict), each with
/// the column as the focus and a caption read from simulating it. A solver that sends the circuit to a quantum server records these first: the circuit is known
/// before the server answers, and drawing it never needs the server.</item>
/// <item><see cref="Done"/>: the Done step with the problem's own verdict, only while recording; <see cref="Verdict"/> is that
/// verdict, for a solver whose Done says more than one caption can.</item>
/// </list>
/// </summary>
static class QuantumSteps {
    /// <summary>Whether <paramref name="rec"/> records anything (a plain solve passes the shared <c>Off</c> recorder).</summary>
    public static bool Recording(StepRecorder<QuantumProgress> rec) => !ReferenceEquals(rec, StepRecorder<QuantumProgress>.Off);

    /// <summary>One Update per column of <paramref name="spec"/>, column by column.</summary>
    public static void Columns(StepRecorder<QuantumProgress> rec, QuantumCircuitSpec spec) {
        if (!Recording(rec)) return;
        var simulation = new QuantumSimulation(spec);
        // How the steps show a column (its index): the circuit applied up to and including it, with the column as the focus.
        var steps = new RunSteps<int, QuantumProgress>(rec,
            partial: column => new QuantumProgress(column + 1),
            focus: spec.ColumnFocus);
        for (int column = 0; column < spec.Columns.Count; column++)
            steps.Update(column, $"{simulation.Caption(column)}");
    }

    /// <summary>
    /// The Done step: <paramref name="answer"/> with the verdict of <paramref name="verify"/> (an answer the verifier cannot
    /// read is not correct), only while recording.
    /// </summary>
    public static void Done(StepRecorder<QuantumProgress> rec, Func<bool> verify, QuantumProgress answer, Func<bool, string> caption) {
        if (!Recording(rec)) return;
        bool ok = Verdict(verify);
        rec.Done(answer, ok, caption(ok));
    }

    /// <summary>The verdict of <paramref name="verify"/>: false for an answer the verifier cannot read.</summary>
    public static bool Verdict(Func<bool> verify) {
        try {
            return verify();
        } catch (Exception ex) when (ex is not StepBudgetExceededException) {
            return false;
        }
    }

    /// <summary>
    /// How often each outcome came up, from a quantum server's response (<c>"counts": {"101": 1024}</c>); empty when it did
    /// not say.
    /// </summary>
    public static List<KeyValuePair<string, long>> Counts(JsonElement root) {
        var counts = new List<KeyValuePair<string, long>>();
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("counts", out var c) && c.ValueKind == JsonValueKind.Object)
            foreach (var p in c.EnumerateObject())
                if (p.Value.TryGetInt64(out long n)) counts.Add(new(p.Name, n));
        return counts;
    }

    /// <summary>"1,024 of 1,024 runs" for an outcome's share of the server's counts; empty when there are none.</summary>
    public static string Shots(IReadOnlyCollection<KeyValuePair<string, long>> counts, string outcome) {
        if (counts.Count == 0) return "";
        long total = counts.Sum(p => p.Value);
        long hits = counts.Where(p => p.Key == outcome).Sum(p => p.Value);
        return $" in {Words.Number(hits)} of {Words.Number(total)} {(total == 1 ? "run" : "runs")}";
    }

    /// <summary>The outcome that came up most often in a server's counts, read as <paramref name="bits"/> bits; null when
    /// there are no counts or the most frequent one is not such a bit string.</summary>
    public static long? MostFrequent(IReadOnlyCollection<KeyValuePair<string, long>> counts, int bits) {
        var top = counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key).FirstOrDefault();
        if (top == null || top.Length != bits || bits == 0 || bits > 62 || top.Any(c => c is not ('0' or '1'))) return null;
        return Convert.ToInt64(top, 2);
    }

    /// <summary>An oracle call's input as bits (<c>101</c>), or <c>0</c> when f has no input bits.</summary>
    public static string Input(int x, int bits) => bits == 0 ? "0" : QuantumCircuits.Bits(x, bits);
}
