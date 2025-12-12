using System.Text.Json;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_DEUTSCH;

class DeutschD3Visualization : IVisualization<DEUTSCH>
{
    public string visualizationName { get; } = "Quantum Circuit (D3)";
    public string visualizationDefinition { get; } = "Deutsch circuit formatted for D3 rendering";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andreas Kramer" };
    public string visualizationType { get; } = "Quantum Circuit D3";

    public DeutschD3Visualization() { }

    public API_JSON visualize(DEUTSCH instance)
    {
        return BuildD3Payload(instance, solution: null);
    }

    public API_JSON SolvedVisualization(DEUTSCH instance, string solution)
    {
        return BuildD3Payload(instance, solution);
    }

    private API_JSON BuildD3Payload(DEUTSCH instance, string? solution)
    {
        bool[] f = instance.funcValues;
        bool isConstant = (f[0] == f[1]);

        var payload = new
        {
            qubits = new[] { "q0", "q1" },
            classical = new[] { "c0" },
            gates = new object[]
            {
                new { id = "x0",  type = "x",  targets = new[] { "q1" },              time = 0 },

                new { id = "h0",  type = "h",  targets = new[] { "q0" },              time = 1 },
                new { id = "h1",  type = "h",  targets = new[] { "q1" },              time = 1 },

                isConstant
                    ? new { id = "x1",  type = "x",  targets = new[] { "q1" },         time = 2 }
                    : new { id = "cx1", type = "cx", targets = new[] { "q0", "q1" },   time = 2 },

                new { id = "h2",  type = "h",  targets = new[] { "q0" },              time = 3 },

                new
                {
                    id = "m0",
                    type = "m",
                    targets = new[] { "q0" },
                    classical = new[] { "c0" },
                    time = 4
                }
            },


            metadata = new
            {
                solution,
                oracleType = isConstant ? "constant" : "balanced"
            }
        };

        return new API_D3CIRCUIT
        {
            payload = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true })
        };
    }
}

