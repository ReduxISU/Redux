using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_CRYPTARITHMS;
using API.Problems.NPComplete.NPC_CRYPTARITHMS.Solvers;

namespace API.Problems.NPComplete.NPC_CRYPTARITHMS.Visualizations;

class CryptarithmVisualization : IVisualization<CRYPTARITHMS>
{
    public string visualizationName { get; } = "Cryptarithm Visualization";
    public string visualizationDefinition { get; } = "A type of verbal arithmetic puzzle in which a set of words is written down in the form of a long addition sum or some other mathematical problem. The object is to replace the letters of the alphabet with decimal digits to make a valid arithmetic sum.";
    public string source { get; } = "Eppstein, D. On the np-completeness of cryptarithms. Computer Science Department of Columbia University, New York, NY.";
    public string sourceLink {get;} = "https://dl.acm.org/doi/pdf/10.1145/24658.2466";
    public string[] contributors { get; } = { "Dani Sleight", "Brayden Weisbeck" };
    // Pick the member matching the API_JSON shape you return below. If no renderer fits,
    // leave this as Unimplemented — the GUI shows an honest "no renderer" state instead of
    // a broken view. Do NOT add a new member without adding a renderer in
    // Redux_GUI/components/Visualization/svgs/Visualizations.js (CI enforces this).
    public VisualizationType visualizationType { get; } = VisualizationType.Unimplemented;
    public ISolver solver { get; } = new CryptarithmSolver(); //TODO fill in solver to use for this visualization

    // --- Methods Including Constructors ---
    public CryptarithmVisualization()
    {

    }
    public API_JSON visualize(CRYPTARITHMS instance)
    {
        //TODO: implement visualization
        
        return new API_empty();
    }

    public API_JSON SolvedVisualization(CRYPTARITHMS instance, string solution)
    {
        //TODO: implement SolvedVisualization (remove method if not implemented)

        return new API_empty();
    }
}