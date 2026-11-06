using System.Text.Json.Serialization;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_KERNEL.Verifiers;

class KernelVerifier : IVerifier<KERNEL> {

    // --- Fields ---
    public const string CertificateGrammar = "K = {v_1, v_2, ..., v_k} ⊆ V | (∀u,v ∈ K, (u,v) ∉ E) ∧ (∀u ∈ V\\K, ∃v ∈ K : (u,v) ∈ E)";
    public const string CertificateExample = "{1, 2, 3}";
    public string verifierName { get; } = "Kernel Solution Verifier";
    public string verifierDefinition { get; } = "Verifies that a candidate kernel satisfies two conditions: independence (no edge connects two vertices in the kernel) and absorption (every vertex outside the kernel has an outgoing edge to a vertex in the kernel).";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Bektur Akkabakov", "Prashant Pant" };
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MathematicalFunction timeComplexity { get; set; } = new("n + m", ", n = |V|, m = |E|");
    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public KernelVerifier() {

    }
    private HashSet<int> parseCertificate(KERNEL problem, string certificate) {
        if (string.IsNullOrWhiteSpace(certificate))
            throw new CertificateParseException(problem, certificate, "Certificate is empty");

        string input = certificate.Trim();

        if (!input.StartsWith("{") || !input.EndsWith("}"))
            throw new CertificateParseException(problem, certificate, "Certificate must be enclosed in braces");

        string content = input.Substring(1, input.Length - 2).Trim();
        HashSet<int> kernel = new HashSet<int>();

        if (content.Length > 0) {
            foreach (string vertex in content.Split(',')) {
                if (!int.TryParse(vertex.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int v))
                    throw new CertificateParseException(problem, certificate, "Invalid vertex identifier");
                if (!kernel.Add(v))
                    throw new CertificateParseException(problem, certificate, "Duplicate vertex in certificate");
            }
        }

        return kernel;
    }
    public bool verify(KERNEL problem, string certificate) {
        this._certificate = "";
        HashSet<int> kernel = parseCertificate(problem, certificate);
        _certificate = certificate;

        // Verify that K is a subset of V
        if (!kernel.IsSubsetOf(problem.vertices))
            return false;

        // Independence: no edge connects two vertices in K
        foreach (var (u, v) in problem.edges) {
            if (kernel.Contains(u) && kernel.Contains(v))
                return false;
        }

        // Absorption: every vertex outside K must point to K
        HashSet<int> absorbed = new HashSet<int>();

        foreach (var (u, v) in problem.edges) {
            if (!kernel.Contains(u) && kernel.Contains(v))
                absorbed.Add(u);
        }

        foreach (int u in problem.vertices) {
            if (!kernel.Contains(u) && !absorbed.Contains(u))
                return false;
        }

        return true;
    }
}