using System.Text;
using System.Text.Json;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Redux_GUI reads gadgets as {color, reductionFromIds, reductionToIds} and checks
// color === "ElementHighlight". Those three fields must stay byte-for-byte identical when new
// gadget fields are added, so this pins them for every reduction that emits gadgets on its
// default instance, serialized exactly the way /ProblemProvider/gadgets does.
// Regenerate deliberately with REDUX_UPDATE_SNAPSHOTS=1 (a diff here means the GUI contract moved).
public class Gadget_Legacy_Snapshot_Tests {
    private static string SnapshotPath =>
        Path.Combine(ProjectSourcePath.Value, "redux-tests", "Reductions", "Snapshots", "gadget_legacy_fields.txt");

    public static string DefaultInstanceLegacyFields() {
        var sb = new StringBuilder();
        foreach (var (_, type) in ProblemProvider.Reductions.OrderBy(p => p.Key, StringComparer.Ordinal)) {
            IReduction red;
            try { red = (IReduction)Activator.CreateInstance(type)!; } catch { continue; }
            if (red.gadgets.Count == 0) continue;
            sb.Append("# ").Append(type.Name).Append('\n');
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(red.gadgets, new JsonSerializerOptions() { WriteIndented = true }));
            foreach (JsonElement g in doc.RootElement.EnumerateArray()) {
                sb.Append(g.GetProperty("color").GetString()).Append(" | ")
                  .Append(string.Join(",", g.GetProperty("reductionFromIds").EnumerateArray().Select(e => e.GetString()))).Append(" | ")
                  .Append(string.Join(",", g.GetProperty("reductionToIds").EnumerateArray().Select(e => e.GetString()))).Append('\n');
            }
        }
        return sb.ToString();
    }

    [Fact]
    public void LegacyGadgetFields_AreUnchanged_ForEveryReductionOnItsDefaultInstance() {
        string actual = DefaultInstanceLegacyFields();
        if (Environment.GetEnvironmentVariable("REDUX_UPDATE_SNAPSHOTS") == "1") {
            File.WriteAllText(SnapshotPath, actual);
            return;
        }
        Assert.Equal(File.ReadAllText(SnapshotPath).Replace("\r\n", "\n"), actual);
    }
}
