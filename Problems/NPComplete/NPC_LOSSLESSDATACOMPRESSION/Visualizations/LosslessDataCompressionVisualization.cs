using API.Interfaces;
using API.Interfaces.CodeTree;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Solvers;

namespace API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Visualizations;

/// <summary>
/// Lossless Data Compression as the Code Tree picture (format=frames): the Huffman tree growing merge by merge, its 0 and 1
/// edges, each character's code and the encoded bits. The list format is unchanged: still empty.
/// </summary>
class LosslessDataCompressionVisualization : IVisualization<LOSSLESSDATACOMPRESSION, API_empty>, ICodeTreeVisualization<LOSSLESSDATACOMPRESSION> {
    public string visualizationName { get; } = "Lossless Data Compression Visualization";
    public string visualizationDefinition { get; } = "TODO";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "TODO";
    public string[] contributors { get; } = { "TODO" };
    public VisualizationType visualizationType { get; } = VisualizationType.Unimplemented; //either "Boolean Satisfiability" or "Graph D3" most likely
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
    public API_empty visualize(LOSSLESSDATACOMPRESSION instance) {
        //TODO: implement visualization

        return new API_empty();
    }

    public API_JSON SolvedVisualization(LOSSLESSDATACOMPRESSION instance, string solution) {
        //TODO: implement SolvedVisualization (remove method if not implemented)

        return new API_empty();
    }
}