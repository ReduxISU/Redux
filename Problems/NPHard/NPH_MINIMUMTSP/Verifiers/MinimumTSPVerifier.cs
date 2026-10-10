using API.Interfaces;

namespace API.Problems.NPHard.NPH_MINIMUMTSP.Verifiers;

// Validity only, as for every NP-Hard verifier (see #603 for the contract): checking that a tour is the cheapest is as
// hard as finding one, so this checks that the answer is a tour and nothing more.
class MinimumTSPVerifier : IVerifier<MINIMUMTSP> {
    public const string CertificateGrammar = "{q0,...,qn} | cycle visiting every node exactly once; the closing repeat of q0 is optional";
    public const string CertificateExample = "{a,c,b,e,d,a}";

    // --- Fields ---
    public string verifierName { get; } = "Default Minimum Traveling Salesperson Verifier";
    public string verifierDefinition { get; } = "Checks that the certificate is a valid tour: it lists every city of the graph exactly once (at least three cities), optionally repeating the first city at the end, and every hop, including the one from the last city back to the first, is an edge of the graph. It does not check that the tour is the cheapest one; checking optimality is as hard as solving the problem.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };

    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public MinimumTSPVerifier() {

    }

    /// <summary>
    /// True when <paramref name="certificate"/> is a tour of the graph. <c>{}</c>, the solvers' answer when there is no
    /// tour, is never one. A blank certificate, or a city name left empty, is malformed and throws
    /// <see cref="CertificateParseException"/>.
    /// </summary>
    public bool verify(MINIMUMTSP problem, string certificate) {
        return TourCost(problem, ReadCities(problem, certificate)) != null;
    }

    /// <summary>
    /// The cities a certificate lists, in order, without the closing repeat of the first city. <c>{}</c> lists none.
    /// </summary>
    public static List<string> ReadCities(MINIMUMTSP problem, string certificate) {
        if (string.IsNullOrWhiteSpace(certificate)) {
            throw new CertificateParseException(problem, certificate, "certificate is empty");
        }
        string inner = certificate.Trim();
        if (inner.StartsWith('{') && inner.EndsWith('}')) inner = inner[1..^1];
        if (string.IsNullOrWhiteSpace(inner)) return new List<string>();

        List<string> cities = inner.Split(',').Select(city => city.Trim()).ToList();
        if (cities.Any(city => city.Length == 0)) {
            throw new CertificateParseException(problem, certificate, "a city name in the certificate is empty");
        }
        if (cities.Count > 1 && cities[0] == cities[^1]) cities.RemoveAt(cities.Count - 1);
        return cities;
    }

    /// <summary>
    /// The total weight of the tour through <paramref name="cities"/> (in order, closing back to the first), or null when
    /// it is not a tour: a city is missing, repeated or not in the graph, there are fewer than three cities, or a hop is
    /// not an edge.
    /// </summary>
    public static long? TourCost(MINIMUMTSP problem, IReadOnlyList<string> cities) {
        if (cities.Count < 3
            || cities.Count != problem.nodes.Count
            || cities.Distinct().Count() != cities.Count
            || cities.Any(city => !problem.nodes.Contains(city))) {
            return null;
        }
        long total = 0;
        for (int i = 0; i < cities.Count; i++) {
            int? weight = problem.Weight(cities[i], cities[(i + 1) % cities.Count]);
            if (weight == null) return null;
            total += weight.Value;
        }
        return total;
    }
}
