using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_DEUTSCH;

class DeutschDefaultVisualization : IVisualization<DEUTSCH>
{
    public string visualizationName { get; } = "Deutsch problem visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for the Deutsch problem";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Jason L. Wright" };
    public string visualizationType { get; } = "Quantum Circuit";

    // --- Methods Including Constructors ---
    public DeutschDefaultVisualization()
    {

    }
    public API_JSON visualize(DEUTSCH instance)
    {
        return new API_QUANTUMCIRCUIT();

    }

    public API_JSON SolvedVisualization(DEUTSCH instance, string solution)
    {
        var qc = new API_QUANTUMCIRCUIT();
        qc.solution = solution;

        // XXX for now, just return a hardcoded circuit, eventually we'll
        // XXX fetch it out of the instance

        qc.circuit = "OPENQASM 2.0;\ninclude \"qelib1.inc\";\nqreg q[2];\ncreg c[1];\nx q[1];\nh q[0];\nh q[1];\ncx q[0],q[1];\nx q[1];\nh q[0];\nmeasure q[0] -> c[0];";
        return qc;
    }
}
