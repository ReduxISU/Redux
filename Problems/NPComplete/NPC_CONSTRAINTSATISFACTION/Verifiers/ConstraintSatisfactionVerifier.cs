using System.Text.Json;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Verifiers;

class ConstraintSatisfactionVerifier : IVerifier<CONSTRAINTSATISFACTION> {
    public const string CertificateExample = """{"x":"0","y":"1"}""";
    public string verifierName { get; } = "Constraint Satisfaction Solution Verifier";
    public string verifierDefinition { get; } = "Checks that the certificate assigns every variable a domain value and that each constraint allows its assigned tuple.";
    public string source { get; } = "Definition of finite-domain constraint satisfaction.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://aima.cs.berkeley.edu/";
    public string[] contributors { get; } = Array.Empty<string>();
    public string certificate { get; private set; } = "";
    public string complexity { get; } = "Polynomial in the explicit instance and certificate sizes";

    public bool verify(CONSTRAINTSATISFACTION problem, string certificate) {
        this.certificate = certificate;
        if (string.IsNullOrWhiteSpace(certificate))
            throw new CertificateParseException(problem, certificate, "certificate is empty");

        var assignment = new Dictionary<string, string>(StringComparer.Ordinal);
        try {
            using var document = JsonDocument.Parse(certificate);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new FormatException("certificate must be a JSON object");
            foreach (var property in document.RootElement.EnumerateObject()) {
                if (property.Value.ValueKind != JsonValueKind.String)
                    throw new FormatException("assignment values must be strings");
                if (!assignment.TryAdd(property.Name, property.Value.GetString()!))
                    throw new FormatException($"duplicate assignment for {property.Name}");
            }
        } catch (Exception ex) when (ex is JsonException or FormatException) {
            throw new CertificateParseException(problem, certificate, ex.Message, ex);
        }

        return assignment.Count == problem.domains.Count
            && assignment.All(pair => problem.domains.TryGetValue(pair.Key, out var domain) && domain.Contains(pair.Value))
            && problem.SatisfiesConstraints(assignment);
    }
}
