using API.Interfaces;
using API.Interfaces.Logic;

namespace API.Problems.NPComplete.NPC_CIRCUITSAT.Verifiers;

class CircuitSatVerifier : IVerifier<CIRCUITSAT> {

    // --- Fields ---
    public string verifierName { get; } = "Default CircuitSAT Verifier";
    public string verifierDefinition { get; } = "This is a verifier for CircuitSAT. It reads the certificate as an assignment to the circuit's input wires, evaluates the circuit gate by gate, and accepts exactly when the output wire is True.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };

    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public CircuitSatVerifier() {

    }

    // Certificate format: (x1:True,x2:False,...) — ':' or '=' separator, True/False/T/F
    // values, the same accepted spellings as the SAT and 3SAT verifiers. Inputs the
    // certificate leaves out are read as False (matching the SAT-family convention that
    // an unassigned variable contributes no true literal); names that are not wires of
    // this circuit are ignored, again as in SAT.
    public bool verify(CIRCUITSAT problem, string certificate) {
        Dictionary<string, bool> assignment;
        try {
            assignment = TruthAssignment.Parse(certificate);
        } catch (FormatException e) {
            throw new CertificateParseException(problem, certificate, e.Message, e);
        }

        var inputs = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (string wire in problem.circuit.Inputs) {
            inputs[wire] = assignment.TryGetValue(wire, out bool value) && value;
        }
        return problem.circuit.Evaluate(inputs);
    }
}
