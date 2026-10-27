using System.Runtime.ConstrainedExecution;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_PRIMEFACTOR.Verifiers;

class PrimeFactorVerifier : IVerifier<PRIMEFACTOR> {
    public const string CertificateGrammar = "(f1,...,fn) | prime factors whose product is the original number";
    public const string CertificateExample = "(3,5)";

    // --- Fields ---
    public string verifierName { get; } = "Prime Factorization Solution Verifier";
    public string verifierDefinition { get; } = "Verifies that every proposed factor is prime and that their product equals the original input number.";
    public string source { get; } = " ";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Jason L. Wright", "Paul Gilbreath", "Alex Svancara", "Grant Gardner" };
    private readonly string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public PrimeFactorVerifier() {

    }

    public bool verify(PRIMEFACTOR problem, string certificate) {
        // Parse the original number from the problem instance using SPADE parser
        SPADE.StringParser parser = new("{ i | i is int}");
        parser.parse(problem.instance);
        int originalNumber = int.Parse(parser["i"].ToString());

        // Parse the list of factors from the certificate using SPADE parser
        parser = new("{f | f is list}");
        parser.parse(certificate);
        SPADE.UtilCollection factorsList = parser["f"];
        // Every factor must be prime (#812), and the product is a BigInteger so it cannot wrap around.
        System.Numerics.BigInteger certProduct = 1;
        foreach (var factor in factorsList) {
            var factorStr = factor.ToString();
            if (factorStr is null) {
                return false;
            }
            var factorValue = System.Numerics.BigInteger.Parse(factorStr, System.Globalization.CultureInfo.InvariantCulture);
            if (factorValue > originalNumber || !IsPrime((long)System.Numerics.BigInteger.Max(factorValue, 0)))
                return false;
            certProduct *= factorValue;
        }

        return certProduct == originalNumber;
    }

    private static bool IsPrime(long n) {
        if (n < 2)
            return false;
        for (long d = 2; d * d <= n; d++)
            if (n % d == 0)
                return false;
        return true;
    }
}
