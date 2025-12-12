using API.Interfaces;
using API.Tools;
using API.Tools.ApiParameters;
using System.Text.Json;

namespace API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Solvers;
class UnstructuredGroverSolver : ISolver<UNSTRUCTUREDSEARCH> {

    // --- Fields ---
    public string solverName { get; } = "Unstructured search solver using Grover's quantum algorithm";
    public string solverDefinition {get;} = "TODO";
    public string source {get;} = "TODO";
    public string[] contributors { get; } = { "Jason L. Wright" };

    private readonly QuantumServerAPI.ServerEnvironment _serverEnvironment;

    // --- Methods Including Constructors ---
    public UnstructuredGroverSolver()
    {
        _serverEnvironment = QuantumServerAPI.ServerEnvironment.ISU_AWS;
    }

    public UnstructuredGroverSolver(QuantumServerAPI.ServerEnvironment environment)
    {
        _serverEnvironment = environment;
    }

    static public int PowerOfTwo(int n)
    {
        if (n > 0 && (n & (n - 1)) == 0)
            return (int)Math.Log2(n);
        throw new ArithmeticException("not a power of two");
    }

    public string solve(UNSTRUCTUREDSEARCH problem){
        Console.WriteLine("solving...");
        // We have to convert the function values into a SAT problem
        int nbits = PowerOfTwo(problem.funcValues.Count);
        var exprs = new List<string>();
        for (int i = 0; i < problem.funcValues.Count; i++)
        {
            if (problem.funcValues[i] != 0)
            {
                var expr = "(";
                var sep = "";
                for (int b = 0; b < nbits; b++)
                {
                    if ((i & (1 << b)) != 0)
                        expr += $"{sep}x{b}";
                    else
                        expr += $"{sep}!x{b}";
                    sep = " & ";
                }
                expr += ")";
                exprs.Add(expr);
            }
        }

        var satproblem = String.Join(" | ",exprs);
        var solution = ReverseString(SolveAsSat(problem, satproblem));
        return Convert.ToInt32(solution, 2).ToString();
    }

    public static string ReverseString(string s)
    {
        var a = s.ToArray();
        Array.Reverse(a);
        return new string(a);
    }

    public class JSON_Sat_Problem
    {
        public string boolexpr {get; set;}
        public JSON_Sat_Problem(string expr)
        {
            boolexpr = expr;
        }
    }

    private string SolveAsSat(UNSTRUCTUREDSEARCH problem, string problemInstance)
    {
        try
        {
            var requestBody = new JSON_Sat_Problem(problemInstance);

            // Create the API client
            var client = new QuantumServerAPI(_serverEnvironment);

            // Make the API call to the quantum endpoint
            string response = client.PostAsync("/sat-quantum", requestBody).Result;

            // Parse the JSON response and extract just the answer
            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("qasm", out JsonElement circuitElement))
            {
                problem.circuit = circuitElement.GetString() ?? "";
            }
            if (root.TryGetProperty("answer_bitstring", out JsonElement answerElement))
            {
                return answerElement.GetString() ?? "No answer found";
            }

            // If no answer field, return the whole response
            return response;
        }
        catch (Exception ex)
        {
            // Return error information in case of failure
            return $"{{\"error\": \"{ex.Message}\"}}";
        }
    }

}
