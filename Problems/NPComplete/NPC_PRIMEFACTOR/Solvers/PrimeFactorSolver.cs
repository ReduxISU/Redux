using System.Numerics;
using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_PRIMEFACTOR.Solvers;

// The steps Trial Division records, for the Quantum Circuit picture (which this solver never runs) and the Formula picture.
// Each shows the factors found so far, the trial divisor and what is left to factor:
//   - a Reject for each trial divisor that does not divide what is left;
//   - an Accept for each division that does: the divisor is a prime factor;
//   - an Accept for a last prime factor, once no divisor up to its square root is left (no divisor, and 1 left);
//   - the Done, with the verifier's verdict.
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
    public MathematicalFunction timeComplexity { get; } = new("sqrt(n)", " in the value of n (exponential in n's bit-length)");
    // --- Methods Including Constructors ---
    public PrimeFactorSolver() { }

    // How the steps show the division: the trial divisor (null when no division happens) and what is left to factor.
    private static RunSteps<(BigInteger? Divisor, BigInteger Left), QuantumProgress> NewSteps(StepRecorder<QuantumProgress> rec,
        List<BigInteger> factors) => new(rec,
        // What every picture shows: the factors found so far, the divisor tried, and what is left.
        partial: s => new QuantumProgress(result: Certificate(factors)) { Divisor = s.Divisor?.ToString(), Remaining = s.Left.ToString() });

    public string solve(PRIMEFACTOR problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // Trial division: divide out each i = 2, 3, 4, ... as often as it divides what is left, while i * i is at most what
    // is left. Every i that divides is prime, since its own factors were divided out before it. Whatever is left above 1
    // at the end has no divisor up to its square root, so it is a prime factor too.
    public string Solve(PRIMEFACTOR problem, StepRecorder<QuantumProgress> rec) {
        var numberToFactor = BigInteger.Parse(problem.instance);
        var original = numberToFactor;

        var factors = new List<BigInteger>();
        var steps = NewSteps(rec, factors);

        var i = new BigInteger(2);
        while (i * i <= numberToFactor) {
            if (timerHasExpired) {
                return SolveRun.TimedOutAnswer;
            }

            if (numberToFactor % i != 0)
                steps.Reject((i, numberToFactor), $"{Words.Number(i)} does not divide {Words.Number(numberToFactor)}.");
            while (numberToFactor % i == 0) {
                var before = numberToFactor;
                factors.Add(i);
                numberToFactor /= i;
                steps.Accept((i, numberToFactor), $"{Words.Number(before)} ÷ {Words.Number(i)} = {Words.Number(numberToFactor)}, so {Words.Number(i)} is a prime factor.");
            }
            i++;
        }

        if (numberToFactor > 1) {
            factors.Add(numberToFactor);
            if (factors.Count == 1)
                steps.Accept((null, BigInteger.One), $"No number from 2 to √{Words.Number(numberToFactor)} divides {Words.Number(numberToFactor)}, so {Words.Number(numberToFactor)} is prime.");
            else
                steps.Accept((null, BigInteger.One), $"No number from 2 to √{Words.Number(numberToFactor)} divides {Words.Number(numberToFactor)}, so {Words.Number(numberToFactor)} is prime: the last factor.");
        }

        string answer = Certificate(factors);
        // The verdict is only for the steps: a plain solve never runs the verifier.
        if (QuantumSteps.Recording(rec)) {
            // What is left once every factor is found: 1 (or the number itself, for 0 and 1, which have no prime factors).
            var final = new QuantumProgress(result: answer) { Remaining = (numberToFactor > 1 ? BigInteger.One : numberToFactor).ToString() };
            bool ok = QuantumSteps.Verdict(() => problem.defaultVerifier.verify(problem, answer));
            if (ok && factors.Count == 0)
                steps.Done(final, true, $"{Words.Number(original)} is the empty product: it has no prime factors.");
            else if (ok && factors.Count == 1)
                steps.Done(final, true, $"{Words.Number(original)} is prime.");
            else if (ok)
                steps.Done(final, true, $"{Words.Number(original)} = {string.Join(" × ", factors.Select(f => Words.Number(f)))}.");
            else if (factors.Count == 0)
                steps.Done(final, false, $"No answer: {Words.Number(original)} has no prime factorization.");
            else
                steps.Done(final, false, $"The answer {answer} is not correct.");
        }
        return answer;
    }

    // The certificate: the prime factors found, in order, "(2,3,5)".
    private static string Certificate(List<BigInteger> factors) => "(" + string.Join(",", factors) + ")";
}
