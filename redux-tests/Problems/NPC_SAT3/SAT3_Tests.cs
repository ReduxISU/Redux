using System.Linq;
using Xunit;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_SAT3.Solvers;
using API.Problems.NPComplete.NPC_SAT3.Verifiers;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_CLIQUE.Verifiers;
using API.Problems.NPComplete.NPC_CLIQUE.Inherited;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_GRAPHCOLORING;
using API.Problems.NPComplete.NPC_GRAPHCOLORING;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.Verifiers;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_DM3;
using API.Problems.NPComplete.NPC_DM3;
using API.Problems.NPComplete.NPC_DM3.Verifiers;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_INTPROGRAMMING01;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class SAT3_Tests {

    // -------------------------------------------------------------------------
    // Instantiation
    // -------------------------------------------------------------------------

    [Fact]
    public void SAT3_Default_Instantiation() {
        SAT3 sat3 = new SAT3();
        Assert.Equal("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)", sat3.defaultInstance);
        Assert.Equal(sat3.defaultInstance, sat3.instance);
    }

    [Fact]
    public void SAT3_Custom_Instance() {
        string instance = "(x1 | x2 | x3) & (!x1 | x2 | x3) & (x1 | !x2 | x3)";
        SAT3 sat3 = new SAT3(instance);
        Assert.Equal(instance, sat3.instance);
    }

    // -------------------------------------------------------------------------
    // Parsing
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | x2 | x3) & (!x1 | x2 | x3) & (x1 | !x2 | x3)", 3)]
    [InlineData("(x1 | x2 | x3) & (!x1 | !x2 | !x3)", 2)]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)", 3)]
    public void SAT3_Parses_Correct_Clause_Count(string instance, int expectedCount) {
        SAT3 sat3 = new SAT3(instance);
        Assert.Equal(expectedCount, sat3.clauses.Count);
    }

    [Theory]
    [InlineData("(x1 | x2 | x3) & (!x1 | x2 | x3) & (x1 | !x2 | x3)", 3)]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)", 3)]
    public void SAT3_Each_Clause_Has_Three_Literals(string instance, int expectedLiteralsPerClause) {
        SAT3 sat3 = new SAT3(instance);
        foreach (var clause in sat3.clauses) {
            Assert.Equal(expectedLiteralsPerClause, clause.Count);
        }
    }

    // -------------------------------------------------------------------------
    // Solver — satisfiable instances
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)")]  // default instance
    [InlineData("(x1 | x2 | x3) & (!x1 | x2 | x3) & (x1 | !x2 | x3)")]
    [InlineData("(!x1 | !x2 | !x3) & (x1 | x2 | x3) & (x1 | !x2 | x3)")]
    public void SAT3_Solver_Finds_Solution_For_Satisfiable_Instance(string instance) {
        SAT3 sat3 = new SAT3(instance);
        Sat3BacktrackingSolver solver = new Sat3BacktrackingSolver();
        string result = solver.solve(sat3);
        Assert.NotEqual("No Solution", result);
    }

    // -------------------------------------------------------------------------
    // Solver — unsatisfiable instance
    // -------------------------------------------------------------------------

    [Fact]
    public void SAT3_Solver_Returns_NoSolution_For_UNSAT_Instance() {
        // All 8 possible 3-literal clauses over x1, x2, x3 — negates every
        // possible truth assignment, so the formula is trivially UNSAT.
        string instance = "(x1 | x2 | x3) & (!x1 | !x2 | x3) & (x1 | !x2 | !x3) & (!x1 | x2 | !x3)" +
                          " & (!x1 | !x2 | !x3) & (x1 | x2 | !x3) & (!x1 | x2 | x3) & (x1 | !x2 | x3)";
        SAT3 sat3 = new SAT3(instance);
        Sat3BacktrackingSolver solver = new Sat3BacktrackingSolver();
        string result = solver.solve(sat3);
        Assert.Equal("No Solution", result);
    }

    // -------------------------------------------------------------------------
    // Verifier — valid certificates
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)", "(x1:True,x2:True,x3:False)")]
    [InlineData("(x1 | x2 | x3) & (!x1 | x2 | x3)", "(x1:False,x2:True,x3:False)")]
    [InlineData("(!x1 | !x2 | !x3) & (x1 | x2 | x3)", "(x1:True,x2:False,x3:False)")]
    public void SAT3_Verifier_Accepts_Valid_Certificate(string instance, string certificate) {
        SAT3 sat3 = new SAT3(instance);
        SAT3Verifier verifier = new SAT3Verifier();
        Assert.True(verifier.verify(sat3, certificate));
    }

    // -------------------------------------------------------------------------
    // Verifier — invalid certificates
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | x2 | x3) & (!x1 | !x2 | !x3)", "(x1:True,x2:True,x3:True)")]   // fails clause 2
    [InlineData("(x1 | x2 | x3) & (!x1 | !x2 | !x3)", "(x1:False,x2:False,x3:False)")] // fails clause 1
    public void SAT3_Verifier_Rejects_Invalid_Certificate(string instance, string certificate) {
        SAT3 sat3 = new SAT3(instance);
        SAT3Verifier verifier = new SAT3Verifier();
        Assert.False(verifier.verify(sat3, certificate));
    }

    // -------------------------------------------------------------------------
    // Solver + Verifier — round-trip
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)")]
    [InlineData("(!x1 | !x2 | !x3) & (x1 | x2 | x3) & (x1 | !x2 | x3)")]
    public void SAT3_Solver_Certificate_Passes_Verifier(string instance) {
        SAT3 sat3 = new SAT3(instance);
        Sat3BacktrackingSolver solver = new Sat3BacktrackingSolver();
        SAT3Verifier verifier = new SAT3Verifier();
        string certificate = solver.solve(sat3);
        Assert.True(verifier.verify(sat3, certificate));
    }

    // -------------------------------------------------------------------------
    // BUG: solver returns a partial certificate (unassigned variables omitted)
    //
    // When all clauses contain x1, setting x1=True satisfies the formula
    // immediately. The backtracking solver stops and returns varStates, which
    // only contains the variables it actually assigned — so x2 and x3 are
    // absent from the certificate string.
    //
    // Fix: in Sat3BacktrackingSolver.solve(), after findSolution() returns,
    // iterate over sat3.literals and add any variable not already in the
    // solution dictionary with a default value of false.
    // -------------------------------------------------------------------------

    [Fact]
    public void SAT3_Solver_Certificate_Contains_All_Variables() {
        // Every clause contains x1, so x1=True satisfies the formula without
        // the solver ever needing to assign x2 or x3. The returned certificate
        // should still include all three variables.
        string instance = "(x1 | x2 | x3) & (x1 | !x2 | x3) & (x1 | x2 | !x3)";
        SAT3 sat3 = new SAT3(instance);
        Sat3BacktrackingSolver solver = new Sat3BacktrackingSolver();
        string certificate = solver.solve(sat3);

        // Each variable name must appear in the certificate
        Assert.Contains("x1:", certificate);
        Assert.Contains("x2:", certificate);
        Assert.Contains("x3:", certificate);
    }

    // -------------------------------------------------------------------------
    // SAT3 → CLIQUE reduction (Sipser)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)", 3, 9)]
    [InlineData("(x1 | x2 | x3) & (!x1 | !x2 | !x3)", 2, 6)]
    [InlineData("(x1 | x2 | x3) & (!x1 | x2 | x3) & (x1 | !x2 | x3) & (x1 | x2 | !x3)", 4, 12)]
    public void SAT3_To_CLIQUE_Reduction_Structure(string sat3Instance, int expectedK, int expectedNodes) {
        // K must equal the number of SAT3 clauses; one node per literal per clause.
        SAT3 sat3 = new SAT3(sat3Instance);
        SipserReduceToCliqueStandard reduction = new SipserReduceToCliqueStandard(sat3);
        CLIQUE clique = reduction.reductionTo;
        Assert.Equal(expectedK, clique.K);
        Assert.Equal(expectedNodes, clique.nodes.Count);
    }

    [Theory]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)")]
    [InlineData("(x1 | x2 | x3) & (!x1 | !x2 | !x3) & (x1 | !x2 | x3)")]
    public void SAT3_To_CLIQUE_Reduction_SAT3_Solution_Maps_To_Valid_Clique_Certificate(string sat3Instance) {
        // A valid SAT3 assignment must map to a valid k-clique certificate.
        SAT3 sat3 = new SAT3(sat3Instance);
        SipserReduceToCliqueStandard reduction = new SipserReduceToCliqueStandard(sat3);
        CLIQUE clique = reduction.reductionTo;

        Sat3BacktrackingSolver solver = new Sat3BacktrackingSolver();
        string sat3Solution = solver.solve(sat3);
        Assert.NotEqual("No Solution", sat3Solution);

        string cliqueCertificate = reduction.mapSolutions(sat3Solution);

        CliqueVerifier cliqueVerifier = new CliqueVerifier();
        Assert.True(cliqueVerifier.verify(clique, cliqueCertificate));
    }

    [Fact]
    public void SAT3_To_CLIQUE_Reduction_MalformedCertificate_Throws_ReductionInputException() {
        // Regression: a CLIQUE-shaped certificate (items missing the ':' separator)
        // once threw IndexOutOfRangeException from Split(":"), surfacing as an
        // opaque HTTP 500. It must now throw the typed ReductionInputException so
        // the controller can return a 400 with a format hint.
        SAT3 sat3 = new SAT3("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)");
        SipserReduceToCliqueStandard reduction = new SipserReduceToCliqueStandard(sat3);
        Assert.Throws<API.Interfaces.ReductionInputException>(
            () => reduction.mapSolutions("{x1_0,x2_1,!x3_3}"));
    }

    // -------------------------------------------------------------------------
    // SAT3 → CLIQUE reduction (Sipser) — reduce2() / SipserClique shape
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)", 3)]
    [InlineData("(x1 | x2 | x3) & (!x1 | !x2 | !x3)", 2)]
    public void SAT3_To_CLIQUE_Reduce2_Structure(string sat3Instance, int expectedClauses) {
        // reduce2() is a second, richer reduction shape (unused by the constructor,
        // which calls reduce()) that returns a SipserClique carrying per-cluster
        // node metadata instead of a plain CLIQUE.
        SAT3 sat3 = new SAT3(sat3Instance);
        SipserReduceToCliqueStandard reduction = new SipserReduceToCliqueStandard(sat3);

        SipserClique clique2 = reduction.reduce2();

        Assert.Equal(expectedClauses, clique2.K);
        Assert.Equal(expectedClauses, clique2.numberOfClusters);
        // nodes is set directly to SAT3.literals (one entry per literal occurrence).
        Assert.Equal(sat3.literals.Count, clique2.nodes.Count);
        Assert.Equal(sat3.literals.Count, clique2.clusterNodes.Count);
        Assert.NotEmpty(clique2.edges);
        Assert.NotNull(clique2.graph);
        Assert.NotEmpty(clique2.instance);
        // reduce2() assigns its result to reductionTo, same as reduce() does.
        Assert.Same(clique2, reduction.reductionTo);
    }

    [Fact]
    public void SAT3_To_CLIQUE_SolutionMappedToClusterNodes_MarksMatchingNodesTrue() {
        SAT3 sat3 = new SAT3("(x1 | x2 | x3) & (!x1 | x2 | x3)");
        SipserReduceToCliqueStandard reduction = new SipserReduceToCliqueStandard(sat3);
        SipserClique clique2 = reduction.reduce2();
        List<string> allNodeNames = clique2.clusterNodes.Select(n => n.name).ToList();

        SipserClique marked = reduction.solutionMappedToClusterNodes(clique2, allNodeNames);

        Assert.All(marked.clusterNodes, n => Assert.Equal(true.ToString(), n.solutionState));
    }

    [Fact]
    public void SAT3_To_CLIQUE_SolutionMappedToClusterNodes_NoMatch_LeavesStateUnset() {
        SAT3 sat3 = new SAT3("(x1 | x2 | x3) & (!x1 | x2 | x3)");
        SipserReduceToCliqueStandard reduction = new SipserReduceToCliqueStandard(sat3);
        SipserClique clique2 = reduction.reduce2();

        SipserClique marked = reduction.solutionMappedToClusterNodes(clique2, new List<string> { "no_such_node" });

        Assert.All(marked.clusterNodes, n => Assert.Equal(string.Empty, n.solutionState));
    }

    // -------------------------------------------------------------------------
    // SAT3 → CLIQUE reduction (Sipser) — SAT3Gadget / CLIQUEGadget
    // -------------------------------------------------------------------------

    [Fact]
    public void SAT3Gadget_Construction_ExposesProperties() {
        SAT3Gadget gadget = new SAT3Gadget("SipserReduceToCliqueStandard", "x1", 3);

        Assert.Equal("SipserReduceToCliqueStandard", gadget.reductionType);
        Assert.Equal("SAT3", gadget.problemType);
        Assert.Equal("x1", gadget.gadgetString);
        Assert.Equal(3, gadget.uniqueId);
    }

    [Fact]
    public void SAT3Gadget_ToString_ReturnsGadgetString() {
        SAT3Gadget gadget = new SAT3Gadget("SipserReduceToCliqueStandard", "!x2", 1);
        Assert.Equal("!x2", gadget.ToString());
    }

    [Fact]
    public void SAT3Gadget_GetHashCode_IgnoresUniqueId() {
        // uniqueId is not part of the GetHashCode computation, only
        // reductionType/problemType/gadgetString are.
        SAT3Gadget a = new SAT3Gadget("R", "x1", 1);
        SAT3Gadget b = new SAT3Gadget("R", "x1", 2);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void SAT3Gadget_Equals_DifferentType_ReturnsFalse() {
        SAT3Gadget gadget = new SAT3Gadget("R", "x1", 1);
        Assert.False(gadget.Equals("x1"));
    }

    [Fact]
    public void SAT3Gadget_Equals_Null_ReturnsFalse() {
        SAT3Gadget gadget = new SAT3Gadget("R", "x1", 1);
        Assert.False(gadget.Equals(null));
    }

    [Fact]
    public void SAT3Gadget_Equals_SameType_DifferentFields_ReturnsFalse() {
        // Exercises the full same-type field-comparison branch (all three if-blocks run).
        // See the BUG test below for why Equals() always returns false here regardless of
        // whether the fields actually match.
        SAT3Gadget a = new SAT3Gadget("R1", "x1", 1);
        SAT3Gadget b = new SAT3Gadget("R2", "x2", 2);

        Assert.False(a.Equals(b));
    }

    [Fact]
    public void SAT3Gadget_Equals_IdenticalGadgets_IncorrectlyReturnsFalse() {
        SAT3Gadget a = new SAT3Gadget("R", "x1", 1);
        SAT3Gadget b = new SAT3Gadget("R", "x1", 1);

        Assert.True(a.Equals(b));
    }

    [Fact]
    public void CLIQUEGadget_Construction_ExposesProperties() {
        CLIQUEGadget gadget = new CLIQUEGadget("SipserReduceToCliqueStandard", "x1_0", 2);

        Assert.Equal("SipserReduceToCliqueStandard", gadget.reductionType);
        Assert.Equal("CLIQUE", gadget.problemType);
        Assert.Equal("x1_0", gadget.gadgetString);
        Assert.Equal(2, gadget.uniqueId);
    }

    [Fact]
    public void CLIQUEGadget_ToString_ReturnsGadgetString() {
        CLIQUEGadget gadget = new CLIQUEGadget("R", "x2_1", 5);
        Assert.Equal("x2_1", gadget.ToString());
    }

    // -------------------------------------------------------------------------
    // SAT3 → GRAPHCOLORING reduction (Karp)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | x2 | x3) & (!x1 | !x2 | !x3)")]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)")]
    public void SAT3_To_GRAPHCOLORING_Reduction_Structure(string sat3Instance) {
        SAT3 sat3 = new SAT3(sat3Instance);
        KarpReduceGRAPHCOLORING reduction = new KarpReduceGRAPHCOLORING(sat3);
        GRAPHCOLORING gc = reduction.reductionTo;

        // 3 palette nodes + one node per distinct literal token (each polarity of
        // each variable gets its own node) + 6 clause-gadget nodes per clause.
        int expectedNodes = 3 + sat3.literals.Distinct().Count() + 6 * sat3.clauses.Count;
        Assert.Equal(expectedNodes, gc.nodes.Count);
        Assert.Equal(3, gc.K);
        Assert.Contains("F", gc.nodes);
        Assert.Contains("T", gc.nodes);
        Assert.Contains("B", gc.nodes);
        Assert.Equal("0", gc.nodeColoring["F"]);
        Assert.Equal("1", gc.nodeColoring["T"]);
        Assert.Equal("2", gc.nodeColoring["B"]);
        // Every edge must have its reverse present (undirected graph stored as two KVPs).
        Assert.All(gc.edges, e => Assert.Contains(new KeyValuePair<string, string>(e.Value, e.Key), gc.edges));
    }

    [Fact]
    public void KarpReduceGRAPHCOLORING_AddEdge_SkipsDuplicateReverseEdge() {
        KarpReduceGRAPHCOLORING reduction = new KarpReduceGRAPHCOLORING(new SAT3("(x1 | x2 | x3)"));
        List<KeyValuePair<string, string>> edges = new();
        List<string> instanceEdges = new();

        reduction.addEdge("a", "b", edges, instanceEdges);
        reduction.addEdge("b", "a", edges, instanceEdges); // reverse of an already-added edge -- no-op

        Assert.Equal(2, edges.Count);
        Assert.Contains(new KeyValuePair<string, string>("a", "b"), edges);
        Assert.Contains(new KeyValuePair<string, string>("b", "a"), edges);
    }

    [Fact]
    public void SAT3_To_GRAPHCOLORING_MapSolutions_InvalidCertificate_ReturnsErrorString() {
        SAT3 sat3 = new SAT3("(x1 | x2 | x3) & (!x1 | !x2 | !x3)");
        KarpReduceGRAPHCOLORING reduction = new KarpReduceGRAPHCOLORING(sat3);

        // Fails clause 2 (all-True doesn't satisfy "!x1 | !x2 | !x3").
        string result = reduction.mapSolutions("(x1:True,x2:True,x3:True)");

        Assert.Equal("Solution is inccorect", result);
    }

    [Fact]
    public void SAT3_To_GRAPHCOLORING_Reduction_SAT3Solution_MapsToValidCertificate() {
        SAT3 sat3 = new SAT3("(x1 | x2 | x3) & (!x1 | x2 | x3)");
        KarpReduceGRAPHCOLORING reduction = new KarpReduceGRAPHCOLORING(sat3);

        Sat3BacktrackingSolver solver = new Sat3BacktrackingSolver();
        string sat3Solution = solver.solve(sat3);
        Assert.NotEqual("No Solution", sat3Solution);

        string gcCertificate = reduction.mapSolutions(sat3Solution);

        GraphColoringVerifier verifier = new GraphColoringVerifier();
        Assert.True(verifier.verify(reduction.reductionTo, gcCertificate));
    }

    // -------------------------------------------------------------------------
    // SAT3 → DM3 reduction (Garey & Johnson)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | x2 | x3) & (!x1 | x2 | !x3)")]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)")]
    public void SAT3_To_DM3_Reduction_Structure(string sat3Instance) {
        SAT3 sat3 = new SAT3(sat3Instance);
        GareyJohnson reduction = new GareyJohnson(sat3);
        DM3 dm3 = reduction.reductionTo;

        // Every candidate triple has exactly 3 elements.
        Assert.All(dm3.M, triple => Assert.Equal(3, triple.Count));
        // X and Y grow in lockstep: every "X.Add" in reduce() (variable/clause/garbage
        // gadgets) is paired with exactly one "Y.Add".
        Assert.Equal(dm3.X.Count, dm3.Y.Count);
        // The variable gadget emits exactly two Z entries (one per polarity) for every
        // literal occurrence in the original SAT3 instance.
        Assert.Equal(2 * sat3.literals.Count, dm3.Z.Count);
        // instance is built by wrapping each M-triple in its own "{...}", one brace
        // pair per triple.
        Assert.Equal(dm3.M.Count, dm3.instance.Count(c => c == '{'));
        Assert.Equal(dm3.M.Count, dm3.instance.Count(c => c == '}'));
    }

    [Fact]
    public void SAT3_To_DM3_Reduction_NoGarbageGadget_WhenLiteralsDoNotExceedClauses() {
        // 2 single-literal clauses: literals.Count (2) - clauses.Count (2) == 0, so the
        // garbage-collection loop's guard ("i < literals.Count - clauses.Count") is
        // never true and no garbage nodes are added.
        SAT3 sat3 = new SAT3("(x1) & (x2)");
        GareyJohnson reduction = new GareyJohnson(sat3);
        DM3 dm3 = reduction.reductionTo;

        Assert.DoesNotContain(dm3.X, x => x.StartsWith("x_garb_"));
    }

    [Fact]
    public void SAT3_To_DM3_MapSolutions_ProducesWellFormedCertificateString() {
        SAT3 sat3 = new SAT3("(x1 | x2 | x3) & (!x1 | x2 | !x3)");
        GareyJohnson reduction = new GareyJohnson(sat3);
        Sat3BacktrackingSolver solver = new Sat3BacktrackingSolver();
        string sat3Solution = solver.solve(sat3);
        Assert.NotEqual("No Solution", sat3Solution);

        string certificate = reduction.mapSolutions(sat3Solution);

        Assert.StartsWith("{", certificate);
        Assert.EndsWith("}", certificate);
        Assert.NotEmpty(certificate);
    }

    [Fact]
    public void SAT3_To_DM3_MapSolutions_SingleClauseInstance_ThrowsInsteadOfMapping() {
        SAT3 sat3 = new SAT3("(x1 | x2 | x3)");
        GareyJohnson reduction = new GareyJohnson(sat3);
        Sat3BacktrackingSolver solver = new Sat3BacktrackingSolver();
        string sat3Solution = solver.solve(sat3);

        string certificate = reduction.mapSolutions(sat3Solution);

        Assert.NotNull(certificate);
    }

    [Fact]
    public void SAT3_To_DM3_Reduction_SAT3Solution_MapsToValidCertificate() {
        SAT3 sat3 = new SAT3("(x1 | x2 | x3) & (!x1 | x2 | !x3)");
        GareyJohnson reduction = new GareyJohnson(sat3);
        DM3 dm3 = reduction.reductionTo;

        Sat3BacktrackingSolver solver = new Sat3BacktrackingSolver();
        string sat3Solution = solver.solve(sat3);
        Assert.NotEqual("No Solution", sat3Solution);

        string dm3Certificate = reduction.mapSolutions(sat3Solution);

        GenericVerifierDM3 verifier = new GenericVerifierDM3();
        Assert.True(verifier.verify(dm3, dm3Certificate));
    }

    // -------------------------------------------------------------------------
    // SAT3 → INTPROGRAMMING01 reduction (Karp)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(x1 | x2 | x3) & (!x1 | x2 | !x3)")]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)")]
    public void SAT3_To_INTPROGRAMMING01_Reduction_Structure(string sat3Instance) {
        SAT3 sat3 = new SAT3(sat3Instance);
        KarpIntProgStandard reduction = new KarpIntProgStandard(sat3);
        INTPROGRAMMING01 ip = reduction.reductionTo;

        List<string> variables = sat3.literals.Select(l => l.Replace("!", "")).Distinct().ToList();

        Assert.Equal(sat3.clauses.Count, ip.C.Count);
        Assert.Equal(sat3.clauses.Count, ip.d.Count);
        Assert.All(ip.C, row => Assert.Equal(variables.Count, row.Count));
        Assert.All(ip.C, row => Assert.All(row, coeff => Assert.InRange(coeff, -1, 1)));
    }

    [Theory]
    [InlineData("(x1 | x2 | x3) & (!x1 | x2 | !x3)", "(x1:True,x2:True,x3:False)")]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)", "(x1:True,x2:False,x3:False)")]
    public void SAT3_To_INTPROGRAMMING01_Reduction_SAT3Solution_MapsToValidCertificate(string sat3Instance, string sat3Solution) {
        // A hand-built (not solver-produced) satisfying assignment is used here -- see the
        // BUG test below for why routing this through Sat3BacktrackingSolver.solve() first
        // would break the mapping for reasons unrelated to KarpIntProgStandard itself.
        SAT3 sat3 = new SAT3(sat3Instance);
        Assert.True(new SAT3Verifier().verify(sat3, sat3Solution));
        KarpIntProgStandard reduction = new KarpIntProgStandard(sat3);

        string ipCertificate = reduction.mapSolutions(sat3Solution);

        GenericVerifier01INTP verifier = new GenericVerifier01INTP();
        Assert.True(verifier.verify(reduction.reductionTo, ipCertificate));
    }

    [Fact]
    public void SAT3_To_INTPROGRAMMING01_Reduction_SolverThenMapSolutions_DesyncsVariableOrder() {
        SAT3 sat3 = new SAT3("(x1 | x2 | x3) & (!x1 | x2 | !x3)");
        KarpIntProgStandard reduction = new KarpIntProgStandard(sat3);

        Sat3BacktrackingSolver solver = new Sat3BacktrackingSolver();
        string sat3Solution = solver.solve(sat3);
        Assert.NotEqual("No Solution", sat3Solution);

        string ipCertificate = reduction.mapSolutions(sat3Solution);

        GenericVerifier01INTP verifier = new GenericVerifier01INTP();
        Assert.True(verifier.verify(reduction.reductionTo, ipCertificate));
    }

    [Fact]
    public void SAT3_To_INTPROGRAMMING01_MapSolutions_InvalidCertificate_ReturnsErrorString() {
        SAT3 sat3 = new SAT3("(x1 | x2 | x3) & (!x1 | !x2 | !x3)");
        KarpIntProgStandard reduction = new KarpIntProgStandard(sat3);

        // Fails clause 2 (all-True doesn't satisfy "!x1 | !x2 | !x3").
        string result = reduction.mapSolutions("(x1:True,x2:True,x3:True)");

        Assert.Equal("Solution is inccorect", result);
    }

    [Fact]
    public void SAT3_To_INTPROGRAMMING01_Reduction_VariableAppearingBothSigns_RowCoefficientIsZero() {
        // x1 and !x1 both appear in the same clause -- neither the "positive only" nor
        // the "negative only" branch applies, so the row-building falls through to the
        // else (coefficient 0) for that variable.
        SAT3 sat3 = new SAT3("(x1 | !x1 | x2)");
        KarpIntProgStandard reduction = new KarpIntProgStandard(sat3);
        INTPROGRAMMING01 ip = reduction.reductionTo;

        List<string> variables = sat3.literals.Select(l => l.Replace("!", "")).Distinct().ToList();
        int x1Index = variables.IndexOf("x1");

        Assert.Equal(0, ip.C[0][x1Index]);
    }
}
