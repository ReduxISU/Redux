using System.Text.Json;
using API.DummyClasses;
using API.Interfaces;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Solvers;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Verifiers;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION;

class CONSTRAINTSATISFACTION : IProblem<ConstraintSatisfactionBruteForce, ConstraintSatisfactionVerifier, DummyVisualization> {
    public string problemName { get; } = "Constraint Satisfaction";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Constraint_satisfaction_problem";
    public string formalDefinition { get; } = "CSP = {<X,D,C> | there exists an assignment f with f(x) in D(x) for every x in X and (f(x1),...,f(xk)) in R for every constraint ((x1,...,xk),R) in C}.";
    public string problemDefinition { get; } = "Given finitely many variables, a finite domain for each variable, and constraints represented by explicit tables of allowed tuples, decide whether an assignment satisfies every constraint. General finite-domain constraint satisfaction is NP-complete; individual restrictions can be tractable.";
    public string inputDescription { get; } = "Variables with finite domains and constraints listing allowed tuples";
    public string outputDescription { get; } = "A satisfying assignment, or No solution exists";
    public string source { get; } = "Russell, Stuart J., and Peter Norvig. Artificial Intelligence: A Modern Approach. 4th edition, Chapter 6: Constraint Satisfaction Problems. Pearson, 2021.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://aima.cs.berkeley.edu/";
    public string[] contributors { get; } = Array.Empty<string>();
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;
    public ProblemType problemType { get; } = ProblemType.Logic;
    public string wikiName { get; } = "";

    private const string DefaultInstance = """
        {"domains":{"x":["0","1"],"y":["0","1"]},"constraints":[{"scope":["x","y"],"tuples":[["0","1"],["1","0"]]}]}
        """;
    public string defaultInstance { get; } = DefaultInstance;
    public string instanceFormat { get; } = "JSON with 'domains' mapping variable names to arrays of distinct string values, and 'constraints' containing objects with 'scope' (distinct variable names in tuple order) and 'tuples' (allowed value arrays). Names and values are case-sensitive. Empty domains/tables are allowed. Example: " + DefaultInstance;
    public string certificateFormat { get; } = "JSON object assigning exactly one string value to each variable. Example: " + ConstraintSatisfactionVerifier.CertificateExample;
    public string instance { get; set; }
    public ConstraintSatisfactionBruteForce defaultSolver { get; } = new();
    public ConstraintSatisfactionVerifier defaultVerifier { get; } = new();
    public DummyVisualization defaultVisualization { get; } = new();

    public IReadOnlyDictionary<string, IReadOnlyList<string>> domains { get; }
    public IReadOnlyList<Constraint> constraints { get; }

    public sealed record Constraint(IReadOnlyList<string> scope, IReadOnlyList<IReadOnlyList<string>> tuples);

    public CONSTRAINTSATISFACTION() : this(DefaultInstance) { }

    public CONSTRAINTSATISFACTION(string stringInstance) {
        instance = stringInstance;
        if (string.IsNullOrWhiteSpace(stringInstance))
            throw new ProblemParseException(problemName, stringInstance, "instance is empty");

        try {
            using var document = JsonDocument.Parse(stringInstance);
            var root = ReadObject(document.RootElement, "domains", "constraints");
            if (root["domains"].ValueKind != JsonValueKind.Object)
                throw new FormatException("domains must be an object");
            var parsedDomains = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var variable in root["domains"].EnumerateObject()) {
                if (string.IsNullOrWhiteSpace(variable.Name))
                    throw new FormatException("variable names must not be blank");
                var values = ReadStrings(variable.Value);
                if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                    throw new FormatException($"domain of {variable.Name} contains duplicate values");
                if (!parsedDomains.TryAdd(variable.Name, values))
                    throw new FormatException($"duplicate variable {variable.Name}");
            }

            if (root["constraints"].ValueKind != JsonValueKind.Array)
                throw new FormatException("constraints must be an array");
            var parsedConstraints = new List<Constraint>();
            foreach (var element in root["constraints"].EnumerateArray()) {
                var constraint = ReadObject(element, "scope", "tuples");
                var scope = ReadStrings(constraint["scope"]);
                if (scope.Distinct(StringComparer.Ordinal).Count() != scope.Length || scope.Any(v => !parsedDomains.ContainsKey(v)))
                    throw new FormatException("constraint scope must contain distinct declared variables");
                if (constraint["tuples"].ValueKind != JsonValueKind.Array)
                    throw new FormatException("tuples must be an array");
                var tuples = new List<IReadOnlyList<string>>();
                foreach (var elementTuple in constraint["tuples"].EnumerateArray()) {
                    var tuple = ReadStrings(elementTuple);
                    if (tuple.Length != scope.Length)
                        throw new FormatException("tuple length must match scope length");
                    for (int i = 0; i < tuple.Length; i++) {
                        if (!parsedDomains[scope[i]].Contains(tuple[i]))
                            throw new FormatException($"tuple value is outside the domain of {scope[i]}");
                    }
                    tuples.Add(tuple);
                }
                parsedConstraints.Add(new Constraint(scope, tuples));
            }
            domains = parsedDomains;
            constraints = parsedConstraints;
        } catch (Exception ex) when (ex is JsonException or FormatException) {
            throw new ProblemParseException(problemName, stringInstance, ex.Message, ex);
        }
    }

    // Enforce the schema explicitly, including duplicate JSON property names.
    private static Dictionary<string, JsonElement> ReadObject(JsonElement element, params string[] names) {
        if (element.ValueKind != JsonValueKind.Object)
            throw new FormatException("expected a JSON object");
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject()) {
            if (!names.Contains(property.Name) || !properties.TryAdd(property.Name, property.Value))
                throw new FormatException($"unexpected or duplicate property {property.Name}");
        }
        if (properties.Count != names.Length)
            throw new FormatException("missing required property: " + string.Join(", ", names.Where(n => !properties.ContainsKey(n))));
        return properties;
    }

    private static string[] ReadStrings(JsonElement element) {
        if (element.ValueKind != JsonValueKind.Array)
            throw new FormatException("expected an array of strings");
        return element.EnumerateArray().Select(value => value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new FormatException("domain values, scope variables, and tuple values must be strings")).ToArray();
    }

    // Callers first check that the assignment is complete and uses domain values.
    internal bool SatisfiesConstraints(IReadOnlyDictionary<string, string> assignment) =>
        constraints.All(c => c.tuples.Any(tuple => c.scope.Select((variable, i) => assignment[variable] == tuple[i]).All(equal => equal)));
}
