using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Tables;
using API.Problems.NPComplete.NPC_AKARI;
using API.Problems.NPComplete.NPC_AKARI.Solvers;

namespace API.Problems.NPComplete.NPC_AKARI.Visualizations;

/// <summary>
/// Draws an Akari board as a table with one table cell per grid cell, using the shared DynamicTable renderer.
/// Walls are grey (clue walls show their number), white cells are blank, and in the solved picture bulbs are
/// highlighted and the cells they light are shaded.
/// </summary>
class AkariVisualization : IVisualization<AKARI, API_TableJSON> {
    public string visualizationName { get; } = "Akari Visualization";
    public string visualizationDefinition { get; } = "Shows the Akari board as a grid: walls are grey (clue walls display their number) and white cells are blank. The solved view marks each light bulb and shades the cells it lights.";
    public string source { get; } = "";
    public string sourceLink { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Carter Luker" };
    public VisualizationType visualizationType { get; } = VisualizationType.DynamicTable;
    public ISolver solver { get; } = new AkariConstraintPropagation();

    // VisColorsArray keys understood by the GUI.
    private const string WallColor = "ClauseHighlight";
    private const string BulbColor = "ElementHighlight";
    private const string LitColor = "Sand";
    private const string BulbText = "\U0001F4A1";
    private const string WallText = "";

    // --- Methods Including Constructors ---
    public AkariVisualization() {

    }

    public API_TableJSON visualize(AKARI instance) {
        return Draw(instance, null);
    }

    public API_JSON SolvedVisualization(AKARI instance, string solution) {
        bool[]? bulbs = ReadBulbs(instance, solution);
        // A solution that is empty, "{}" or not a placement for this board has nothing to draw over the board.
        return bulbs == null ? visualize(instance) : Draw(instance, bulbs);
    }

    // The bulb placement a certificate describes, or null if it is not one for this instance.
    private static bool[]? ReadBulbs(AKARI instance, string solution) {
        if (string.IsNullOrWhiteSpace(solution)) return null;
        try {
            int[][] certificateGrid = AKARI.ParseGrid(solution, allowBulbs: true, detail => new FormatException(detail));
            return instance.BulbsFrom(certificateGrid);
        } catch (FormatException) {
            return null;
        }
    }

    private static API_TableJSON Draw(AKARI instance, bool[]? bulbs) {
        var table = new API_TableJSON();
        for (int c = 0; c < instance.Cols; c++) {
            table.columns.Add(new TableColumn { key = c.ToString(), label = (c + 1).ToString() });
        }

        // A white cell is lit when either of its segments holds a bulb.
        var lit = new bool[instance.WhiteCount];
        if (bulbs != null) {
            var segmentLit = new bool[instance.SegmentCells.Length];
            for (int i = 0; i < instance.WhiteCount; i++) {
                if (!bulbs[i]) continue;
                segmentLit[instance.RowSegment[i]] = true;
                segmentLit[instance.ColSegment[i]] = true;
            }
            for (int i = 0; i < instance.WhiteCount; i++) {
                lit[i] = segmentLit[instance.RowSegment[i]] || segmentLit[instance.ColSegment[i]];
            }
        }

        for (int r = 0; r < instance.Rows; r++) {
            var row = new TableRow { id = (r + 1).ToString(), cellColors = new Dictionary<string, string>() };
            for (int c = 0; c < instance.Cols; c++) {
                string key = c.ToString();
                int cell = instance.grid[r][c];
                int i = instance.WhiteIndex[r][c];
                if (i < 0) {
                    row.cells[key] = cell == AKARI.WALL ? WallText : cell.ToString();
                    row.cellColors[key] = WallColor;
                } else if (bulbs != null && bulbs[i]) {
                    row.cells[key] = BulbText;
                    row.cellColors[key] = BulbColor;
                } else {
                    row.cells[key] = " ";
                    if (lit[i]) row.cellColors[key] = LitColor;
                }
            }
            table.rows.Add(row);
        }
        return table;
    }
}
