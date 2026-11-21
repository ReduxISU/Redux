# SAT ≤ₚ Akari: correctness and running time

This is the proof for the reduction in
[SATToAkariReduction.cs](../../Problems/NPComplete/NPC_SAT/ReduceTo/NPC_AKARI/SATToAkariReduction.cs)
(`reductionName` "Akari Wire Reduction"). It shows two things:

1. **Validity.** For every CNF formula φ, the grid G(φ) the reduction builds has an Akari solution if and only
   if φ is satisfiable.
2. **Polynomial time.** G(φ) has (4m + 5)(5L + 2n + 1) cells and is built in O(L(m + n)) time, where n, m and L
   are the number of variables, clauses and literal occurrences of φ.

Akari is in NP: the verifier checks a bulb placement in O(rows × columns) time. Together with (1) and (2), and
SAT being NP-complete, this gives another proof that Akari is NP-complete. McPhail [1] proved this first, using a
different, circuit-based set of gadgets. The construction here was written for Redux and reduces from CNF SAT
directly.

---

## 1. Akari, restated with segments

An instance is a grid of white cells, plain walls, and clue walls labelled 0–4. A **segment** is a maximal run of
white cells in one row (a row segment) or one column (a column segment), with walls or the grid edge at both ends.
Every white cell lies in exactly one row segment and one column segment. A bulb lights exactly the cells of its two
segments. So a set B of white cells is a solution exactly when:

- **(S1)** every segment contains at most one cell of B (otherwise two bulbs light each other);
- **(S2)** every white cell has a cell of B in its row segment or in its column segment;
- **(S3)** every clue wall labelled k has exactly k cells of B among its orthogonal neighbours.

For a cell p we write [p] = 1 if p ∈ B and [p] = 0 otherwise.

The soundness half of the proof (Section 5) uses only (S1) and (S3). The completeness half (Section 4) checks all
three.

## 2. The construction

Let φ = C₀ ∧ … ∧ C_{m−1} be a CNF formula over the variables x₁, …, xₙ, numbered in order of first appearance, with
L literal occurrences in total. Each clause has at least one literal. Literals may repeat inside a clause, and a
clause may contain both x and ¬x. Every cell not listed below is a plain wall `#`. Coordinates are (row, column),
starting at 0. Rows 0 and 4m + 4 and column 0 are all walls.

### 2.1 Wires

A **wire** is a straight run of at least two white cells, one cell wide, that forms one whole segment. Its two
end cells are different cells. The construction uses four kinds of wire:

| Wire | Direction | Where |
| --- | --- | --- |
| chain wire h_k (of a variable) | horizontal | row 1 |
| occurrence wire (one per literal occurrence) | vertical | its own column, from row 3 down to its clause's row |
| carry wire K_{c,j} (inside clause c) | horizontal | row R_c + 1 |
| slack wire S_{c,j} (inside clause c) | vertical | rows R_c + 2 and R_c + 3 |

Every clue in the grid is a 1 or a 2, and every clue's white neighbours are **end cells** of wires (Lemma 1(c)).
A vertical occurrence wire and a horizontal carry wire may share one cell, called a **crossing**.

### 2.2 Columns: variable chains and occurrence columns

Each variable x gets a contiguous block of columns, one block after another starting at column s = 1. List x's
occurrences o₁, …, o_d (d ≥ 1) with **all negative occurrences first**, then all positive ones. Let p_x be the
number of negative occurrences. Set

- junction columns J_i = s + 3 + 5(i − 1), for i = 1, …, d;
- occurrence column col(o_i) = J_i − 1 if o_i is positive, and J_i + 1 if o_i is negative;
- the chain occupies row 1 from column s to column e = J_d + 2. Cell (1, J_i) is a clue **1** (the *junction*).
  The white cells between consecutive junctions form the chain wires

  h₀ = (1, s … J₁ − 1), h_i = (1, J_i + 1 … J_{i+1} − 1) for 0 < i < d, h_d = (1, J_d + 1 … e).

  These wires have lengths 3, 4 and 2;
- the cell (2, col(o_i)) is a clue **1** (the *occurrence clue*);
- the next variable's block starts at s′ = e + 2, which leaves one wall column between chains.

The grid has W = 1 + Σ_x (5d_x + 2) = 5L + 2n + 1 columns.

**Claim A (spacing).** Two different occurrence columns are at least 3 apart.

*Proof.* Take consecutive occurrences o_i, o_{i+1} of the same variable. J_{i+1} − J_i = 5, and each column is
J ± 1. The only way to get a gap of 7 is o_i positive followed by o_{i+1} negative. That never happens, because the
negative occurrences come first. So the gap is 3 or 5. For two different variables, x's last occurrence column is at
most e − 1, and the next variable's first occurrence column is at least s′ + 2 = e + 4. ∎

### 2.3 Rows: occurrence wires and clauses

Clause C_c owns the four rows R_c, …, R_c + 3, where R_c = 4 + 4c. The grid has 4m + 5 rows.

- **Occurrence wire.** An occurrence o in clause c has the white cells (3 … R_c, col(o)). Its top end is
  t(o) = (3, col(o)), and its bottom end is b(o) = (R_c, col(o)). Since R_c ≥ 4, it has at least two cells.
- **Clause clues.** Let col₁ < … < col_k be clause c's occurrence columns, and ℓ₁, …, ℓ_k the literals in that
  order. The clause is a disjunction, so the order does not matter. The cell Q_j = (R_c + 1, col_j) is a clue:
  **1** if j = 1, and **2** if j ≥ 2.
- **Carry wire.** For 1 ≤ j < k, K_j has the white cells (R_c + 1, col_j + 1 … col_{j+1} − 1). By Claim A it has at
  least 2 cells. Its left end touches Q_j, and its right end touches Q_{j+1}.
- **Slack wire.** For 2 ≤ j ≤ k, S_j has the white cells (R_c + 2, col_j) and (R_c + 3, col_j). Its top end
  touches Q_j.

**Example.** Here is φ = (x1 ∨ ¬x2) ∧ (x1 ∨ x2). The left grid is G(φ). The right grid shows the solution that
Section 4 builds from x1 = True, x2 = False. Columns 3 and 17 are clause 0's occurrence wires, and columns 8 and 20
are clause 1's. Clause 0's carry wire (row 5) crosses clause 1's wire in column 8 at (5, 8).

```text
              111111111122222                          111111111122222
    0123456789012345678901234                0123456789012345678901234
 0  #########################             0  #########################
 1  #___1____1__#___1____1__#             1  #__*1___*1_*#*__1*___1*_#
 2  ###1####1########1##1####             2  ###1####1########1##1####
 3  ###_####_########_##_####             3  ###_####_########_##*####
 4  ###_####_########_##_####             4  ###*####_########*##_####
 5  ###1_____________2##_####             5  ###1____________*2##_####
 6  ########_########_##_####             6  ########_########_##_####
 7  ########_########_##_####             7  ########_########*##_####
 8  ########_###########_####             8  ########*###########_####
 9  ########1___________2####             9  ########1__________*2####
10  ####################_####            10  ####################*####
11  ####################_####            11  ####################_####
12  #########################            12  #########################
```

### 2.4 Well-formedness

**Lemma 1.** In G(φ):

- **(a)** Every white cell belongs to exactly one wire, except a crossing, which belongs to one occurrence wire
  and one carry wire.
- **(b)** Each wire is a whole segment. For every white cell that is not a crossing, the segment perpendicular to
  its wire is that cell alone.
- **(c)** The white neighbours of each clue are exactly the following:

  | Clue | Value | White neighbours |
  | --- | --- | --- |
  | junction (1, J_i) | 1 | right end of h_{i−1}, left end of h_i |
  | occurrence clue of o_i, positive | 1 | right end of h_{i−1} (above), t(o_i) (below) |
  | occurrence clue of o_i, negative | 1 | left end of h_i (above), t(o_i) (below) |
  | Q₁ of a clause with k = 1 | 1 | b(o₁) |
  | Q₁ of a clause with k ≥ 2 | 1 | b(o₁), left end of K₁ |
  | Q_j, 1 < j < k | 2 | right end of K_{j−1}, b(o_j), left end of K_j, top of S_j |
  | Q_k, k ≥ 2 | 2 | right end of K_{k−1}, b(o_k), top of S_k |

- **(d)** No wire end is a crossing.

*Proof.* Go through the rows.

- **Row 1** holds only chain cells and junctions. Row 0 is all walls. Row 2 holds only occurrence clues and walls.
  So the chain wires are whole row segments, and every chain cell's column segment is just that cell. A junction's
  vertical neighbours are walls: column J_i is not an occurrence column, because those are J ± 1 and junctions are
  5 apart.
- **Row 2** has no white cells.
- **Row 3 and below.** By construction, row r ≥ 3 is white in column q only in these cases:
  - q is an occurrence column of a clause c with r ≤ R_c (an occurrence wire);
  - r = R_c + 1 and q lies strictly inside one of clause c's carry intervals (a carry cell);
  - r ∈ {R_c + 2, R_c + 3} and q = col_j for some j ≥ 2 of clause c (a slack cell).
- **Vertical extents.** Column col(o) is white exactly on rows 3 … R_c. Above it is the occurrence clue in row 2,
  and below it is Q_j in row R_c + 1. So the occurrence wire is a whole column segment. A slack wire has Q_j above
  it, and the cell (R_c + 4, col_j) below it. That cell is either the border row or row R_{c+1}, which is white only
  in the occurrence columns of clauses after c. col_j belongs to clause c, so it is not one of them.
- **Horizontal neighbours in rows R_c, R_c + 2 and R_c + 3.** The white cells in these rows sit in distinct
  occurrence columns. By Claim A, no two are adjacent, so each one's row segment is that cell alone.
- **Row R_c + 1.** Its white cells are clause c's carry cells and the cells of later clauses' occurrence wires
  passing through. Take a passing wire in column q:
  - if q lies inside a carry interval of clause c, the cell (R_c + 1, q) is a crossing;
  - otherwise, its horizontal neighbours q ± 1 are neither clue columns (Claim A) nor carry cells, so they are
    walls.

  Take a carry cell (R_c + 1, q) that is not a crossing. Then q is not an occurrence column of any clause c′ ≥ c.
  If q is an occurrence column of an earlier clause c′ < c, the cells of that column end at row R_{c′} + 3 ≤ R_c − 1.
  Either way, (R_c, q) and (R_c + 2, q) are walls.
- **Carry ends.** The carry ends are in columns col_j + 1 and col_{j+1} − 1. By Claim A these are not occurrence
  columns, so no carry end is a crossing. No other wire end is in a carry row. This proves (d).

Part (c) can be read off the same inventory. Each clue's orthogonal neighbours are the listed cells or walls. In
particular, Q₁ has a wall to its left and no slack wire below it, and Q_k has a wall to its right. ∎

**Corollary 2.** Let w be a wire with end cells α ≠ β. In every solution, [α] + [β] ≤ 1.

*Proof.* α and β lie in the same segment w, so this follows from (S1). ∎

## 3. Notation for the two directions

For a variable x, chain wire h_k, write X_k = [right end of h_k]. For a truth assignment τ and a literal ℓ, write
τ(ℓ) ∈ {0, 1}. For clause c with literals ℓ₁, …, ℓ_k in column order, let N_j = τ(ℓ₁) ∨ … ∨ τ(ℓ_j).

## 4. Completeness: φ satisfiable ⇒ G(φ) solvable

Let τ satisfy φ. Put exactly one bulb in each wire, at one of its end cells:

| Wire | Bulb at |
| --- | --- |
| every chain wire h_k of x | right end if τ(x) = 1, left end otherwise |
| occurrence wire of o with literal ℓ | b(o) if τ(ℓ) = 1, t(o) otherwise |
| carry wire K_j of clause c | right end if N_j = 1, left end otherwise |
| slack wire S_j of clause c | top if σ_j = 1, bottom if σ_j = 0 |

The slack values are σ_j = 1 + N_j − N_{j−1} − τ(ℓ_j) for 1 < j < k, and σ_k = 2 − N_{k−1} − τ(ℓ_k).

**σ is always 0 or 1.** For 1 < j < k, N_j = N_{j−1} ∨ τ(ℓ_j). The four cases give:

| N_{j−1} | τ(ℓ_j) | N_j | σ_j |
| --- | --- | --- | --- |
| 0 | 0 | 0 | 1 |
| 1 | 0 | 1 | 1 |
| 0 | 1 | 1 | 1 |
| 1 | 1 | 1 | 0 |

For j = k, the clause is satisfied, so N_{k−1} + τ(ℓ_k) ∈ {1, 2}, and σ_k ∈ {1, 0}.

**(S1).** Every segment is either a wire or a single cell (Lemma 1(a), (b)). A wire holds exactly one bulb. A
single-cell segment {p} holds at most one.

**(S2).** Every white cell lies in a wire (Lemma 1(a)), and that wire holds a bulb.

**(S3).** Use the neighbour table of Lemma 1(c). No bulb sits on a crossing (Lemma 1(d)), so each neighbour counts
once.

- **Junction:** τ(x) + (1 − τ(x)) = 1.
- **Occurrence clue, positive o_i:** τ(x) + (1 − τ(x)) = 1, since t(o_i) holds the bulb exactly when τ(x) = 0.
- **Occurrence clue, negative o_i:** the left end of h_i gives 1 − τ(x), and t(o_i) gives 1 − τ(¬x) = τ(x). The sum
  is 1.
- **Q₁ with k = 1:** [b(o₁)] = τ(ℓ₁) = 1, because the one-literal clause is satisfied.
- **Q₁ with k ≥ 2:** τ(ℓ₁) + (1 − N₁) = 1, since N₁ = τ(ℓ₁).
- **Q_j with 1 < j < k:** N_{j−1} + τ(ℓ_j) + σ_j + (1 − N_j) = 2, by the definition of σ_j.
- **Q_k with k ≥ 2:** N_{k−1} + τ(ℓ_k) + σ_k = 2, by the definition of σ_k.

So B is a solution. This is exactly the placement `mapSolutions` builds. ∎

## 5. Soundness: G(φ) solvable ⇒ φ satisfiable

Let B be **any** solution. B may put a bulb in the middle of a wire or on a crossing. Then neither end of that wire
holds a bulb, and in a crossing's case neither end of the other wire does either. The lemmas below allow for this:
they use only (S1), through Corollary 2, and (S3).

**Lemma 3 (chains only lose truth).** For each variable x: X₀ ≥ X₁ ≥ … ≥ X_d.

*Proof.* Junction J_i gives X_{i−1} + [left end of h_i] = 1. Corollary 2 on h_i gives
X_i ≤ 1 − [left end of h_i] = X_{i−1}. ∎

Define the assignment τ(x) = X_{p_x}: the bulb of the first chain wire after x's negative occurrences is at its right
end. This is what `MapBack` reads.

**Lemma 4 (a bulb at a wire's bottom means a true literal).** If [b(o)] = 1 for an occurrence o of x, then the
literal of o is true under τ.

*Proof.* Corollary 2 on o's wire gives [t(o)] = 0. Let o = o_i.

- **o positive.** Then i > p_x, because positives come after negatives. The occurrence clue gives
  X_{i−1} + [t(o)] = 1, so X_{i−1} = 1. Since i − 1 ≥ p_x, Lemma 3 gives τ(x) = X_{p_x} ≥ X_{i−1} = 1.
- **o negative.** Then i ≤ p_x. The occurrence clue gives [left end of h_i] = 1. By Corollary 2, X_i = 0. Lemma 3
  gives τ(x) = X_{p_x} ≤ X_i = 0, so ¬x is true. ∎

The order of occurrences matters here. Because truth can only be lost along a chain (Lemma 3), reading τ(x) at the
boundary between the negative and positive occurrences is consistent with both sides.

**Lemma 5 (every clause has a wire with its bulb at the bottom).** For each clause c, some occurrence o of c has
[b(o)] = 1.

*Proof.* If k = 1, Q₁ is a 1 whose only white neighbour is b(o₁), so [b(o₁)] = 1.

If k ≥ 2, let κ_j = [right end of K_j]. We show by induction on j that κ_j = 1 implies [b(o_i)] = 1 for some i ≤ j.

- **j = 1.** Corollary 2 gives [left end of K₁] = 0, so Q₁ forces [b(o₁)] = 1.
- **1 < j < k.** Corollary 2 gives [left end of K_j] = 0. Q_j then gives κ_{j−1} + [b(o_j)] + [top of S_j] = 2.
  Since [top of S_j] ≤ 1, either κ_{j−1} = 1 (apply the induction hypothesis) or [b(o_j)] = 1.

Finally, Q_k gives κ_{k−1} + [b(o_k)] + [top of S_k] = 2. So κ_{k−1} = 1 or [b(o_k)] = 1. In both cases some bulb
sits at a wire bottom. ∎

By Lemmas 4 and 5, every clause contains a literal that τ makes true, so τ satisfies φ. ∎

Sections 4 and 5 together prove: **G(φ) has an Akari solution ⇔ φ is satisfiable.**

## 6. Running time

- **Size.** G(φ) has H × W = (4m + 5)(5L + 2n + 1) cells. Since n ≤ L, this is O(mL) ⊆ O(|φ|²), and the
  instance string has O(1) characters per cell.
- **Time.** `BuildLayout` scans the L occurrences once per variable (O(nL)) and sorts each clause's occurrences
  (O(L log L) in total). `BuildGrid` writes each cell a constant number of times (O(mL)). The `AKARI` constructor
  parses the grid and builds its segments in time linear in the grid. Total: O(L(m + n)), which is polynomial in
  |φ|. This is the declared `complexity`.
- **Solution maps.** `mapSolutions` (SAT → Akari) places one bulb per wire. `MapBack` (Akari → SAT) parses the
  certificate grid and reads one cell per variable. Both are linear in the grid size.

## 7. How the code is checked

[SATToAkari_Tests.cs](../../redux-tests/Problems/NPC_SAT/SATToAkari_Tests.cs) checks the proof's claims on concrete
formulas:

- the hand-built grid for (x1);
- the grid size formula;
- that all clues are 1 or 2;
- that `mapSolutions` returns a valid Akari solution for exactly the satisfying assignments (Section 4);
- that the Akari constraint-propagation solver finds a solution exactly when brute force finds φ satisfiable
  (Sections 4–5);
- that `mapSolutionBack` turns whatever solution the solver finds into a satisfying assignment (Section 5).

During development, a Python model of the same construction was also checked against brute force on 440 random
formulas with 1–3 variables and up to 4 clauses.

## References

1. Brandon McPhail. *Light Up is NP-complete.* 2005.
2. Richard M. Karp. Reducibility among combinatorial problems. In *Complexity of Computer Computations*, 85–103.
   Springer, 1972.
