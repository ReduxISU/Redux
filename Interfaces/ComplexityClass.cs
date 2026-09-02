using System.Text.Json.Serialization;

namespace API.Interfaces;

/// <summary>
/// The computational complexity class a problem is declared to belong to. Declared,
/// not derived: the <c>Problems/&lt;Folder&gt;/</c> on-disk layout is a filing
/// convention only, and <c>Problems/NPComplete/</c> misfiles at least twelve of its
/// entries (e.g. six actually-P problems, one NP-intermediate problem, and five
/// quantum-complexity problems that aren't classical-hierarchy citizens at all).
/// This enum is the source of truth instead. Consumers building membership-in-NP
/// filters must special-case <see cref="NPComplete"/> onto <see cref="NP"/> (see the
/// doc comment on <see cref="NP"/>) — no other pair of values here implies another.
/// <para>
/// The quantum members (<see cref="BQP"/>, <see cref="EQP"/>, <see cref="QMA"/>,
/// <see cref="QCMA"/>, <see cref="QIP"/>, <see cref="MIPStar"/>) are a second,
/// incomparable hierarchy in the same enum rather than a separate field: a problem
/// declares whichever single class it actually belongs to, classical or quantum, and
/// nothing here implies membership across the two families. Reduction-validity checks
/// against the classical P/NP hierarchy must skip any problem declaring one of these —
/// see <c>ReductionValidity_Tests.Rank</c> (redux-tests/Metadata), which omits them
/// for exactly this reason.
/// </para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ComplexityClass>))]
public enum ComplexityClass {
    /// <summary>No complexity class has been declared for this problem yet.</summary>
    Unclassified = 0,
    /// <summary>Solvable in polynomial time.</summary>
    P,
    /// <summary>In NP, and every problem in NP polynomial-time reduces to it.</summary>
    NPComplete,
    /// <summary>At least as hard as every problem in NP, but not (known to be) in NP itself.</summary>
    NPHard,
    /// <summary>
    /// In NP. Also implicitly includes every <see cref="NPComplete"/> problem — NP-Complete
    /// is a subset of NP by definition — so consumers filtering "is this in NP?" should treat
    /// NPComplete as a match too rather than testing this value for exact equality. Problems
    /// declaring this value directly are ones known to be in NP but not known to be
    /// NP-complete or in P (e.g. integer factorization).
    /// </summary>
    NP,
    /// <summary>
    /// Bounded-error Quantum Polynomial time: decidable by a quantum computer in
    /// polynomial time with error probability bounded away from 1/2 on every instance
    /// (typically amplified to at most 1/3 by repetition). The quantum analog of BPP.
    /// Simon's problem and Grover/UNSTRUCTUREDSEARCH belong here — both need repeated
    /// sampling or tuned iteration counts to drive their error probability down, so
    /// neither succeeds with certainty on a single run.
    /// </summary>
    BQP,
    /// <summary>
    /// Exact Quantum Polynomial time: decidable by a quantum computer in polynomial
    /// time with zero error — certainty, not merely bounded error, on every run. The
    /// quantum analog of P. Deutsch, Deutsch-Jozsa, and Bernstein-Vazirani belong here:
    /// each solves its promise problem with a single oracle query and no failure
    /// probability, the textbook examples of EQP separating from classical P in the
    /// query-complexity model.
    /// </summary>
    EQP,
    /// <summary>
    /// Quantum Merlin-Arthur: the quantum analog of NP (and of MA). A "yes" instance
    /// has a polynomial-size quantum witness that a polynomial-time quantum verifier
    /// accepts with high probability; a "no" instance has no witness the verifier
    /// accepts with more than small probability.
    /// </summary>
    QMA,
    /// <summary>
    /// Quantum Classical Merlin-Arthur: like <see cref="QMA"/>, but the witness Merlin
    /// sends is restricted to classical bits rather than a quantum state — only the
    /// verifier is quantum.
    /// </summary>
    QCMA,
    /// <summary>
    /// Quantum Interactive Polynomial time: the quantum analog of IP — decidable via a
    /// polynomial number of message rounds between a quantum verifier and a single
    /// computationally-unbounded quantum prover.
    /// </summary>
    QIP,
    /// <summary>
    /// MIP*: the quantum analog of MIP — a multi-prover interactive proof system whose
    /// provers may not communicate but may share prior quantum entanglement. Named
    /// with a trailing <c>Star</c> rather than <c>*</c> because C# enum members must be
    /// valid identifiers.
    /// </summary>
    MIPStar,
}
