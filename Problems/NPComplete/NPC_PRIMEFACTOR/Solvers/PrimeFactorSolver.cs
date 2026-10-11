using System.Numerics;
using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_PRIMEFACTOR.Solvers;

// Incremental: each factor found is kept, and the number left only shrinks.
class PrimeFactorSolver : ISolver<PRIMEFACTOR, QuantumProgress>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "Prime Factorization Trial Division";
    public string solverDefinition { get; } = "A brute-force trial division algorithm for prime factorization. " +
        "This solver iteratively tests divisibility starting from the smallest prime (2) " +
        "and continues dividing the number by each prime factor found until the number is fully factored. " +
        "The algorithm runs in O(√n) time complexity in the worst case, making it suitable for small to moderate-sized integers.";
    public string source { get; } = "Pisano, Leonardo (1202), Incipit liber Abbaci compositus to Lionardo filio Bonaccii Pisano in year Mccij, Museo Galileo.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Jason L. Wright", "Paul Gilbreath", "Alex Svancara" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive trial division. Classic trap: looks polynomial-ish
    // (a loop to sqrt(n)) in the *value* of n, but is exponential in n's *bit-length* -- the actual
    // input size for a factoring problem -- which is why Shor's quantum algorithm (ShorsQuantumSolver)
    // is a genuine complexity-class improvement, not just a constant-factor speedup.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // solve()'s trial-division loop runs while i * i <= numberToFactor, i.e. O(sqrt(n)) iterations
    // in the *value* of n -- but n's bit-length b is the real input size, and n = 2^b, so this is
    // O(2^(b/2)): exponential in input size, matching the Exponential bucket above.
    public string complexity { get; } = "O(sqrt(n)) in the value of n (exponential in n's bit-length)";
    // --- Methods Including Constructors ---
    public PrimeFactorSolver() { }

    public string solve(PRIMEFACTOR problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // Steps (the Quantum Circuit picture, which this solver never runs): a Reject for each trial divisor that does not
    // divide what is left, an Accept for each division that does (the factors so far are the result), an Accept for a last
    // prime factor, and Done with the verifier's verdict.
    public string Solve(PRIMEFACTOR problem, StepRecorder<QuantumProgress> rec) {
        var numberToFactor = BigInteger.Parse(problem.instance);
        var original = numberToFactor;

        var factors = new List<BigInteger>();
        string Factors() => "(" + string.Join(",", factors) + ")";

        var i = new BigInteger(2);
        while (i * i <= numberToFactor) {
            if (timerHasExpired) {
                return "TIMEOUT";
            }

            if (numberToFactor % i != 0) {
                var divisor = i;
                var left = numberToFactor;
                rec.Reject(() => new QuantumProgress(result: Factors()), () => $"{N(divisor)} does not divide {N(left)}.");
            }
            while (numberToFactor % i == 0) {
                var before = numberToFactor;
                factors.Add(i);
                numberToFactor /= i;
                var divisor = i;
                var after = numberToFactor;
                rec.Accept(() => new QuantumProgress(result: Factors()), () => $"{N(before)} ÷ {N(divisor)} = {N(after)}, so {N(divisor)} is a prime factor.");
            }
            i++;
        }

        if (numberToFactor > 1) {
            factors.Add(numberToFactor);
            var last = numberToFactor;
            rec.Accept(() => new QuantumProgress(result: Factors()), () => factors.Count == 1
                ? $"No number from 2 to √{N(last)} divides {N(last)}, so {N(last)} is prime."
                : $"No number from 2 to √{N(last)} divides {N(last)}, so {N(last)} is prime: the last factor.");
        }

        string answer = Factors();
        QuantumSteps.Done(rec, () => problem.defaultVerifier.verify(problem, answer), new QuantumProgress(result: answer),
            ok => ok ? (factors.Count == 0 ? $"{N(original)} is the empty product: it has no prime factors."
                    : factors.Count == 1 ? $"{N(original)} is prime." : $"{N(original)} = {string.Join(" × ", factors.Select(N))}.")
                : factors.Count == 0 ? $"No answer: {N(original)} has no prime factorization." : $"The answer {answer} is not correct.");
        return answer;
    }

    private static string N(BigInteger n) => n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
}
