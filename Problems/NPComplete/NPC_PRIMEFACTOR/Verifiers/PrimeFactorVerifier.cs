using API.Interfaces;
using System.Text.Json;
using SPADE;

namespace API.Problems.NPComplete.NPC_PRIMEFACTOR.Verifiers;

class PrimeFactorVerifier : IVerifier<PRIMEFACTOR> {

    // --- Fields ---
    public string verifierName {get;} = "Prime Factor Verifier";
    public string verifierDefinition {get;} = "Verifies that the proposed factors are all prime numbers and their product equals the original input number.";
    public string source {get;} = " ";
    public string[] contributors {get;} = { "Paul Gilbreath", "Alex Svancara", "Grant Gardner" };
    private readonly string _certificate =  "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public PrimeFactorVerifier() {

    }

    public bool verify(PRIMEFACTOR problem, string certificate){
        try {
            // Parse the original number from the problem instance using SPADE parser
            StringParser parser = new("{ i | i is int}");
            parser.parse(problem.instance);
            int originalNumber = int.Parse(parser["i"].ToString());

            // Safety check: Reject unreasonably large numbers to prevent DoS
            const int MAX_NUMBER = 1_000_000_000; // 1 billion
            if (originalNumber > MAX_NUMBER || originalNumber < 2) {
                return false;
            }

            // Parse the certificate (solution) which is in format "[3,5]"
            int[]? factors = JsonSerializer.Deserialize<int[]>(certificate);

            if (factors == null || factors.Length == 0) {
                return false;
            }

            // Safety check: Limit number of factors to prevent abuse
            // (2^64 > any int, so max 64 factors is mathematically reasonable)
            const int MAX_FACTORS = 64;
            if (factors.Length > MAX_FACTORS) {
                return false;
            }

            // Check 1: Verify all factors are prime and not too large
            foreach (int factor in factors) {
                // Reject factors that are too large to prevent slow prime checks
                if (factor > MAX_NUMBER || factor < 2) {
                    return false;
                }
                if (!IsPrime(factor)) {
                    return false;
                }
            }

            // Check 2: Verify the product of all factors equals the original number
            long product = 1;
            foreach (int factor in factors) {
                product *= factor;
                // Safety check: Prevent overflow
                if (product > MAX_NUMBER) {
                    return false;
                }
            }

            return product == originalNumber;
        }
        catch {
            // If parsing fails or any error occurs, verification fails
            return false;
        }
    }

    /// <summary>
    /// Helper method to check if a number is prime
    /// </summary>
    private static bool IsPrime(int n) {
        if (n <= 1) return false;
        if (n == 2) return true;
        if (n % 2 == 0) return false;

        // Check odd divisors up to sqrt(n)
        int limit = (int)Math.Sqrt(n);
        for (int i = 3; i <= limit; i += 2) {
            if (n % i == 0) {
                return false;
            }
        }
        return true;
    }
}
