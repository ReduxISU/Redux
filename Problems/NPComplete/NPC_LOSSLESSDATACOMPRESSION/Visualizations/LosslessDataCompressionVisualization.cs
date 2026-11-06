using API.Interfaces;
using API.Interfaces.CodeTree;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Tables;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Solvers;

namespace API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Visualizations;

/// <summary>
/// Lossless Data Compression as the Code Tree picture (format=frames): the Huffman tree growing merge by merge, its 0 and 1
/// edges, each character's code and the encoded bits. The list format is the code table (<c>DynamicTable</c>): one row per
/// distinct character with how often it appears and, once solved, its code and the bits it takes, marked Solution.
/// </summary>
[LegacyVisualizationType(VisualizationType.Unimplemented)]
class LosslessDataCompressionVisualization : IVisualization<LOSSLESSDATACOMPRESSION, API_TableJSON>, ICodeTreeVisualization<LOSSLESSDATACOMPRESSION> {
    public string visualizationName { get; } = "Lossless Data Compression Visualization";
    public string visualizationDefinition { get; } = "Lists each distinct character of the text with how often it appears. Once solved, each row shows the character's Huffman code and the bits it takes in the encoded text (its count times its code's length); the title gives the total.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "TODO";
    public string[] contributors { get; } = { "Bektur Akkabakov", "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.DynamicTable;
    public ISolver solver { get; } = new LosslessDataCompressionSolver();

    // The Code Tree picture (format=frames only). Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(HuffmanForest);
    CodeTreeSpec ICodeTreeVisualization<LOSSLESSDATACOMPRESSION>.Describe(LOSSLESSDATACOMPRESSION problem) => Spec(problem);
    HuffmanForest ICodeTreeVisualization<LOSSLESSDATACOMPRESSION>.ParseAnswer(LOSSLESSDATACOMPRESSION problem, string answer) =>
        CodeTreeFrames.ParseCodes(Spec(problem), answer);

    /// <summary>The instance as the Code Tree picture describes it: the text, as the solver reads it.</summary>
    internal static CodeTreeSpec Spec(LOSSLESSDATACOMPRESSION problem) => new(problem.instance ?? "");

    // --- Methods Including Constructors ---
    public LosslessDataCompressionVisualization() {

    }

    // The list format: the code table, one row per distinct character in character order (as the Code Tree picture's
    // leaves), with how often it appears and, once solved, its code and the bits it takes (count x code length), marked
    // Solution. With no codes (an answer that does not parse, or no answer) the code columns stay empty.
    private static API_TableJSON Table(LOSSLESSDATACOMPRESSION problem, HuffmanForest answer) {
        var spec = Spec(problem);
        var symbols = CodeTreeFrames.Symbols(spec);
        string title = $"{CodeTreeFrames.Count(spec.Text.Length)} character{(spec.Text.Length == 1 ? "" : "s")}, {symbols.Count} different";
        var table = new API_TableJSON();
        table.columns.Add(new TableColumn { key = "symbol", label = "Character" });
        table.columns.Add(new TableColumn { key = "count", label = "Count" });
        table.columns.Add(new TableColumn { key = "code", label = "Code" });
        table.columns.Add(new TableColumn { key = "bits", label = "Bits" });
        long total = 0;
        bool solved = symbols.Count > 0 && answer.Codes.Count == symbols.Count;
        for (int i = 0; i < symbols.Count; i++) {
            var (symbol, count) = symbols[i];
            string code = solved ? answer.Codes[CodeTreeFrames.LeafId(i)] : "";
            total += (long)count * code.Length;
            table.rows.Add(new TableRow {
                id = CodeTreeFrames.LeafId(i),
                color = solved ? "Solution" : null,
                cells = new Dictionary<string, string> {
                    ["symbol"] = CodeTreeFrames.Label(symbol),
                    ["count"] = CodeTreeFrames.Count(count),
                    ["code"] = code,
                    ["bits"] = solved ? CodeTreeFrames.Count((long)count * code.Length) : "",
                },
            });
        }
        table.title = solved ? $"{title}, encoded in {CodeTreeFrames.Count(total)} bit{(total == 1 ? "" : "s")}" : title;
        return table;
    }

    public API_TableJSON visualize(LOSSLESSDATACOMPRESSION problem) => Table(problem, new HuffmanForest([], []));

    public API_JSON SolvedVisualization(LOSSLESSDATACOMPRESSION problem, string solution) =>
        Table(problem, CodeTreeFrames.ParseCodes(Spec(problem), solution));
}