using System.Globalization;
using System.Numerics;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Tables;
using API.Problems.NPHard.NPH_ILP.Solvers;
using API.Problems.NPHard.NPH_ILP.Verifiers;

namespace API.Problems.NPHard.NPH_ILP.Visualizations;

class IntegerLinearProgrammingDefaultVisualization : IVisualization<INTEGERLINEARPROGRAMMING, API_TableJSON> {
    public string visualizationName => "Integer Linear Programming Table";
    public string visualizationDefinition => "Displays objective and constraint coefficients, integer assignments, objective values, and constraint slack.";
    public VisualizationType visualizationType => VisualizationType.DynamicTable;
    public string source => "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Prashant Pant" };
    public ISolver solver { get; } = new IntegerLinearProgrammingSolver();

    public API_TableJSON visualize(INTEGERLINEARPROGRAMMING problem) => Build(problem, "Integer Linear Program", null);

    public API_JSON SolvedVisualization(INTEGERLINEARPROGRAMMING problem, string solution) {
        if (string.IsNullOrWhiteSpace(solution)) return visualize(problem);
        if (solution is "infeasible" or "unbounded" or "timeout")
            return Build(problem, solution switch {
                "infeasible" => "Infeasible: no integer assignment satisfies all constraints",
                "unbounded" => "Unbounded: the objective can increase without limit",
                _ => "Search stopped: time or work limit reached; no optimality claim"
            }, null);
        var assignment = IntegerLinearProgrammingVerifier.ParseAssignment(problem, solution);
        bool feasible = new IntegerLinearProgrammingVerifier().verify(problem, solution);
        return Build(problem, feasible ? "Solution assignment (feasible)" : "Solution assignment (infeasible)", assignment);
    }

    public List<API_JSON> StepsVisualization(INTEGERLINEARPROGRAMMING problem, List<object> steps) =>
        steps.OfType<IntegerLinearProgrammingSolver.Step>()
            .Select(step => (API_JSON)Build(problem, step.Status, step.Assignment)).ToList();

    private static API_TableJSON Build(INTEGERLINEARPROGRAMMING problem, string title, IReadOnlyList<BigInteger>? x) {
        var table = new API_TableJSON { title = title };
        table.columns.Add(new TableColumn { key = "name", label = "Row" });
        for (int j = 0; j < problem.c.Count; j++)
            table.columns.Add(new TableColumn { key = $"x{j + 1}", label = $"x{j + 1}" });
        foreach (var (key, label) in new[] { ("relation", "Relation"), ("rhs", "Bound"),
                     ("value", "Value"), ("slack", "Slack"), ("status", "Status") })
            table.columns.Add(new TableColumn { key = key, label = label });

        var objective = Row("objective", "Objective", problem.c);
        objective.cells["relation"] = "maximize";
        objective.cells["value"] = x is null ? "-" : Text(IntegerLinearProgrammingSolver.Dot(problem.c, x));
        table.rows.Add(objective);
        if (x is not null) table.rows.Add(Row("assignment", "Assignment", x));
        for (int i = 0; i < problem.A.Count; i++) {
            var row = Row($"constraint{i + 1}", $"Constraint {i + 1}", problem.A[i]);
            row.cells["relation"] = "<=";
            row.cells["rhs"] = Text(problem.b[i]);
            if (x is not null) {
                var value = IntegerLinearProgrammingSolver.Dot(problem.A[i], x);
                var slack = problem.b[i] - value;
                row.cells["value"] = Text(value);
                row.cells["slack"] = Text(slack);
                row.cells["status"] = slack.Sign < 0 ? "Violated" : slack.IsZero ? "Tight" : "Satisfied";
            }
            table.rows.Add(row);
        }
        return table;
    }

    private static TableRow Row(string id, string name, IReadOnlyList<BigInteger> coefficients) {
        var row = new TableRow { id = id };
        row.cells["name"] = name;
        for (int j = 0; j < coefficients.Count; j++) row.cells[$"x{j + 1}"] = Text(coefficients[j]);
        return row;
    }
    private static string Text(BigInteger value) => value.ToString(CultureInfo.InvariantCulture);
}
