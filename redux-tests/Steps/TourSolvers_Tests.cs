using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_DIRECTEDHAMILTONIAN;
using API.Problems.NPComplete.NPC_DIRECTEDHAMILTONIAN.Solvers;
using API.Problems.NPComplete.NPC_HAMILTONIAN;
using API.Problems.NPComplete.NPC_HAMILTONIAN.Solvers;
using API.Problems.NPComplete.NPC_TSP;
using API.Problems.NPComplete.NPC_TSP.Solvers;
using API.Problems.NPHard.NPH_MINIMUMTSP;
using API.Problems.NPHard.NPH_MINIMUMTSP.Solvers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Hamiltonian Cycle, Directed Hamiltonian Cycle, Traveling Salesperson (brute force, greedy, branch and bound) and
// Minimum Traveling Salesperson (brute force) solvers record Tour steps. Recording must never change an answer. Endpoints/Snapshots/tour_solver_answers.tsv holds what
// each solver returned (solver, instance, answer; tab separated) BEFORE it recorded steps, on its default instance and on
// 12 seeded random graphs and 4 to 7 hand-made ones (including ones with no answer).
public class TourSolvers_Tests {
    private static string SnapshotPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file);

    public static TheoryData<string, string, string> RecordedAnswers {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var line in File.ReadAllLines(SnapshotPath("tour_solver_answers.tsv")).Where(l => l.Length > 0)) {
                var parts = line.Split('\t');
                data.Add(parts[0], parts[1], parts[2]);
            }
            return data;
        }
    }

    private static Type ProblemTypeOf(Type solver) =>
        solver.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>)).GetGenericArguments()[0];

    private static SolveRun Run(ISolver solver, string instance) => solver.Run(instance, withSteps: true);

    private static List<SolverStep<Tour>> StepsOf(SolveRun run) => run.Steps.Cast<SolverStep<Tour>>().ToList();

    [Fact]
    public void TheSnapshotCoversEveryTourSolver_WithAnswersAndWithout() {
        var rows = RecordedAnswers.Select(d => d.Data).GroupBy(d => d.Item1).ToDictionary(g => g.Key, g => g.ToList());
        Assert.Equal(["bruteforceminimumtsp", "directedhamiltonianbruteforce", "hamiltonianbruteforce", "tspbranchandbound", "tspbruteforce", "tspgreedy"], rows.Keys.Order());
        foreach (var (solver, list) in rows) {
            Assert.Contains(list, d => d.Item3 == "{}");
            Assert.Contains(list, d => d.Item3 != "{}");
        }
        Assert.All(rows.Values, l => Assert.True(l.Count >= 16));
    }

    [Theory]
    [MemberData(nameof(RecordedAnswers))]
    public void Solve_ReturnsWhatItReturnedBeforeRecordingSteps(string solverName, string instance, string answer) {
        var type = ProblemProvider.Solvers[solverName];
        var solver = (ISolver)Activator.CreateInstance(type)!;
        var on = solver.Run(instance, withSteps: true);
        var off = solver.Run(instance, withSteps: false);

        Assert.Equal(answer, on.Answer);
        Assert.Equal(answer, off.Answer);
        Assert.Equal(answer, solver.solve(instance));
        Assert.Empty(off.Steps);
        Assert.Equal(typeof(Tour), on.StepShape);

        // The last step is the only Done, every caption is a sentence, and Done.Ok is what the problem's verifier says.
        var steps = StepsOf(on);
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.Single(steps, s => s.Event == StepEvent.Done);
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));
        var problem = (IProblem)Activator.CreateInstance(ProblemTypeOf(type))!;
        bool verified;
        try { verified = problem.defaultVerifier.verify(instance, answer); } catch { verified = false; }
        Assert.Equal(verified, steps[^1].Ok);
        Assert.Equal(answer != "{}", steps[^1].Ok);   // these solvers return {} exactly when they found no answer
    }

    // The last step's partial answer says the same thing as the answer string: the tour, closing repeat included.
    [Theory]
    [MemberData(nameof(RecordedAnswers))]
    public void TheDoneStep_ShowsTheAnswerTheSolverGave(string solverName, string instance, string answer) {
        var run = Run((ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solverName])!, instance);
        Assert.Equal(GraphTourFrames.ParseTour(answer), ((SolverStep<Tour>)run.Steps[^1]).Partial);
    }

    // Every partial answer names only nodes of the graph, never names a node twice (apart from a closing repeat of the
    // first), and only the Done step and a few tries are closed.
    [Theory]
    [MemberData(nameof(RecordedAnswers))]
    public void EveryStep_NamesOnlyNodesOfTheGraph_EachAtMostOnce(string solverName, string instance, string answer) {
        _ = answer;
        var type = ProblemProvider.Solvers[solverName];
        var run = Run((ISolver)Activator.CreateInstance(type)!, instance);
        dynamic problem = Activator.CreateInstance(ProblemTypeOf(type), instance)!;
        HashSet<string> nodes = ((IEnumerable<string>)problem.nodes).ToHashSet();
        foreach (var step in StepsOf(run)) {
            var order = step.Partial.Order;
            Assert.All(order, n => Assert.Contains(n, nodes));
            var open = order.Length > 1 && order[0] == order[^1] ? order[..^1] : order;
            Assert.Equal(open.Length, open.Distinct().Count());
            Assert.All(step.Focus, f => Assert.True(nodes.Contains(f) || f.Contains('-') || f.Contains('>'), f));
        }
    }

    // ---- Hamiltonian Cycle ----

    [Fact]
    public void Hamiltonian_TriesPathsInTheOrderItAlwaysDid_AndSaysWhyEachDeadEndFails() {
        var steps = StepsOf(Run(new HamiltonianBruteForce(), new HAMILTONIAN().defaultInstance));
        Assert.Equal(StepEvent.Try, steps[0].Event);
        Assert.Equal("Start the tour at 1.", steps[0].Caption);
        Assert.Equal(["1"], steps[0].Partial.Order);
        Assert.Equal("Go 1 → 2: the path is 1 → 2.", steps[1].Caption);
        Assert.Equal(["2", "1-2"], steps[1].Focus);
        // 3 and 4 are not joined: the partial includes the node tried, so the picture draws the missing edge.
        var rejected = steps.First(s => s.Event == StepEvent.Reject);
        Assert.Equal("There is no edge between 3 and 4, so 4 can't come after 3.", rejected.Caption);
        Assert.Equal(["1", "2", "3", "4"], rejected.Partial.Order);
        Assert.Equal(["4"], rejected.Focus);
        // A complete order whose closing edge is missing is rejected too, naming the missing edge.
        var closing = steps.First(s => s.Event == StepEvent.Reject && s.Partial.Order.Length == 6);
        Assert.Equal("Try the tour 1 → 2 → 3 → 5 → 4 → 1: there is no edge between 4 and 1.", closing.Caption);
        Assert.Equal(["4", "1"], closing.Focus);
        Assert.Contains(steps, s => s.Event == StepEvent.Backtrack && s.Caption == "Every way on from 5 fails: back up to 3.");
        // The cycle is found by an Accept that closes it, then Done.
        var accept = steps[^2];
        Assert.Equal(StepEvent.Accept, accept.Event);
        Assert.StartsWith("Close the tour 3 → 1:", accept.Caption);
        Assert.Equal(["1", "2", "4", "5", "3", "1"], steps[^1].Partial.Order);
        Assert.True(steps[^1].Ok);
    }

    [Fact]
    public void Hamiltonian_LeavesTheProblemsNodeListAlone() {
        var problem = new HAMILTONIAN();
        var before = problem.nodes.ToList();
        new HamiltonianBruteForce().Solve(problem, new StepRecorder<Tour>());
        Assert.Equal(before, problem.nodes);
    }

    [Fact]
    public void Hamiltonian_WhenThereIsNoCycle_EndsWithADoneThatSaysSo() {
        var steps = StepsOf(Run(new HamiltonianBruteForce(), "({1,2,3,4},{{1,2},{2,3},{3,4}})"));
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.False(steps[^1].Ok);
        Assert.Empty(steps[^1].Partial.Order);
        Assert.StartsWith("No Hamiltonian cycle", steps[^1].Caption);
        Assert.DoesNotContain(steps, s => s.Event == StepEvent.Accept && s.Caption.StartsWith("Close"));
    }

    // ---- Directed Hamiltonian Cycle ----

    [Fact]
    public void DirectedHamiltonian_NamesTheDirection_AndFocusesDirectedEdgeIds() {
        var run = Run(new DirectedHamiltonianBruteForce(), new DIRECTEDHAMILTONIAN().defaultInstance);
        var steps = StepsOf(run);
        Assert.Equal("{1,3,5,4,2,1}", run.Answer);
        Assert.Equal("There is no edge from 1 to 2, so 2 can't come after 1.", steps[1].Caption);   // only 2>1 exists
        Assert.Equal(StepEvent.Reject, steps[1].Event);
        Assert.Equal(["1", "2"], steps[1].Partial.Order);
        Assert.Equal(["3", "1>3"], steps[2].Focus);
        Assert.Equal("Go 1 → 3: the path is 1 → 3.", steps[2].Caption);
        Assert.Equal(["1", "3", "5", "4", "2", "1"], steps[^1].Partial.Order);
        Assert.True(steps[^1].Ok);
    }

    // ---- Traveling Salesperson ----

    [Fact]
    public void TspBruteForce_OneStepPerPermutation_FirstWithinKWins_AndEachRejectSaysWhy() {
        // Square with a missing diagonal: 1-2:3, 2-3:5, 3-4:2, 4-1:4 (the best tour costs 14); 1-3 and 2-4 are absent.
        const string instance = "(({1,2,3,4},{({1,2},3),({2,3},5),({3,4},2),({4,1},4)}),13)";
        var run = Run(new TSPBruteForce(), instance);
        Assert.Equal("{}", run.Answer);
        var steps = StepsOf(run);
        Assert.Equal(24 + 1, steps.Count);   // 4! permutations, then Done
        Assert.All(steps.Take(24), s => Assert.Equal(StepEvent.Reject, s.Event));
        Assert.Equal("Try 1 → 2 → 3 → 4 → 1: total 14 is more than K = 13.", steps[0].Caption);
        Assert.Equal(["1-2", "1-4", "2-3", "3-4"], steps[0].Focus.Order());   // the whole tour failed
        Assert.Equal("Try 1 → 2 → 4 → 3 → 1: there is no edge between 2 and 4.", steps[1].Caption);
        Assert.Equal(["2", "4"], steps[1].Focus);
        Assert.Contains("Tried all 24 permutations", steps[^1].Caption);

        var withinK = StepsOf(Run(new TSPBruteForce(), instance.Replace(",13)", ",14)")));
        Assert.Equal(StepEvent.Accept, withinK[^2].Event);
        Assert.Equal("Try 1 → 2 → 3 → 4 → 1: total 14 is within K = 14, so this is a tour.", withinK[^2].Caption);
        Assert.Equal(2, withinK.Count);   // the very first permutation is within K
        Assert.True(withinK[^1].Ok);
    }

    [Fact]
    public void TspGreedy_FollowsTheNearestNeighbour_WithRunningCost_AndKeepsTheBestSoFar() {
        var run = Run(new TSPGreedy(), new TSP().defaultInstance);
        var steps = StepsOf(run);
        Assert.Equal("Start at New York.", steps[0].Caption);
        Assert.Equal(StepEvent.Try, steps[0].Event);
        Assert.Equal("Go New York → Chicago (weight 790, path cost 790): the nearest city not yet visited.", steps[1].Caption);
        Assert.Equal("Go Chicago → Denver (weight 1000, path cost 1790): the nearest city not yet visited.", steps[2].Caption);
        Assert.Equal(["Denver", "Chicago-Denver"], steps[2].Focus);
        Assert.Equal("Close the tour Miami → New York: total 6830, the best so far.", steps[5].Caption);
        Assert.Equal(StepEvent.Accept, steps[5].Event);
        // Starting from Chicago gives a tour that is worse; it is rejected with the running best named.
        var worse = steps.First(s => s.Caption.StartsWith("Close the tour Los Angeles → Chicago"));
        Assert.Equal(StepEvent.Reject, worse.Event);
        Assert.Equal("Close the tour Los Angeles → Chicago: total 7160 is no better than the best so far (6830).", worse.Caption);
        Assert.Equal(5, steps.Count(s => s.Event == StepEvent.Try));   // one start per city
        Assert.StartsWith("The best tour is New York → Chicago", steps[^1].Caption);
    }

    [Fact]
    public void TspGreedy_ADeadEnd_AndAMissingClosingEdge_AreRejects() {
        // 1 and 3 are only joined through 2, so from 3 nothing is left; from 1 the tour reaches 3 but cannot close.
        var steps = StepsOf(Run(new TSPGreedy(), "(({1,2,3},{({1,2},1),({2,3},1)}),9)"));
        var closing = steps.First(s => s.Caption.StartsWith("Close the tour 3 → 1"));
        Assert.Equal(StepEvent.Reject, closing.Event);
        Assert.Contains("there is no such edge", closing.Caption);
        Assert.Equal(["1", "2", "3", "1"], closing.Partial.Order);   // the phantom closing edge is drawn
        Assert.Equal(["3", "1"], closing.Focus);
        var steps2 = StepsOf(Run(new TSPGreedy(), "(({1,2,3,4},{({1,2},1),({1,3},1),({1,4},1)}),9)"));
        var dead = steps2.First(s => s.Caption.StartsWith("Dead end at 2"));
        Assert.Equal(StepEvent.Reject, dead.Event);
        Assert.Equal(["1", "2"], dead.Partial.Order);
    }

    [Fact]
    public void TspGreedy_ATourOverK_IsRejectedWithItsTotal_AndNothingIsTheBest() {
        var steps = StepsOf(Run(new TSPGreedy(), "(({1,2,3},{({1,2},5),({2,3},5),({1,3},5)}),10)"));
        Assert.All(steps.Where(s => s.Caption.StartsWith("Close")), s => {
            Assert.Equal(StepEvent.Reject, s.Event);
            Assert.EndsWith("total 15 is more than K = 10.", s.Caption);
        });
        Assert.False(steps[^1].Ok);
        Assert.Equal("No start gave a tour within K = 10.", steps[^1].Caption);
    }

    [Fact]
    public void TspBranchAndBound_StartsFromTheGreedyTour_ThenBranchesBestFirst_PruningByTheBound() {
        var run = Run(new TSPBranchAndBound(), new TSP().defaultInstance);
        var steps = StepsOf(run);
        Assert.Equal(StepEvent.Accept, steps[0].Event);
        Assert.StartsWith("Start from the greedy tour New York → Chicago → Denver → Los Angeles → Miami → New York: total 6830, the best so far.", steps[0].Caption);
        Assert.Equal("Expand the path New York: lower bound 5380.", steps[1].Caption);
        Assert.Equal(StepEvent.Try, steps[1].Event);
        Assert.Equal("Branch New York → Chicago (weight 790, path cost 790): lower bound 5470, queued.", steps[2].Caption);   // the real weight, not the reduced one
        Assert.Equal(StepEvent.Accept, steps[2].Event);
        var pruned = steps.First(s => s.Event == StepEvent.Reject);
        Assert.Equal("Skip Denver after New York: the lower bound 7450 is no better than the best tour so far (6830).", pruned.Caption);
        Assert.Equal(["New York", "Denver"], pruned.Partial.Order);
        // The greedy tour is already optimal on the default instance, so the search never improves on it.
        Assert.DoesNotContain(steps.Skip(1), s => s.Event == StepEvent.Accept && s.Caption.StartsWith("Close"));
        Assert.StartsWith("The cheapest tour is New York → Chicago → Denver → Los Angeles → Miami → New York, total 6830, within K = 8000.", steps[^1].Caption);
    }

    [Fact]
    public void TspBranchAndBound_AnImprovementOverTheGreedyTour_IsAnAcceptThatNamesWhatItBeats() {
        // Nearest neighbour gets 60 here (3 5 1 2 4); the search finds 59 (1 2 4 5 3).
        const string instance = "(({1,2,3,4,5},{({1,2},9),({1,3},21),({1,4},26),({1,5},6),({2,3},17),({2,4},6),({2,5},8),({3,4},28),({3,5},11),({4,5},12)}),500)";
        var run = Run(new TSPBranchAndBound(), instance);
        Assert.Equal("{1,2,4,5,3,1}", run.Answer);
        var steps = StepsOf(run);
        Assert.StartsWith("Start from the greedy tour 3 → 5 → 1 → 2 → 4 → 3: total 60, the best so far.", steps[0].Caption);
        var improvement = Assert.Single(steps, s => s.Event == StepEvent.Accept && s.Caption.StartsWith("Close the tour"));
        Assert.Equal("Close the tour 3 → 1: total 59, the best so far (it beats 60).", improvement.Caption);
        Assert.Equal(["1", "2", "4", "5", "3", "1"], improvement.Partial.Order);
        Assert.Equal(["3", "1"], improvement.Focus);
        // From then on, bounds are compared with the new best.
        Assert.Contains(steps, s => s.Event == StepEvent.Reject && s.Caption.EndsWith("is no better than the best tour so far (59)."));
        Assert.Equal("The cheapest tour is 1 → 2 → 4 → 5 → 3 → 1, total 59, within K = 500.", steps[^1].Caption);
    }

    [Fact]
    public void TspBranchAndBound_NoTourWithinK_SaysTheCheapestCost() {
        var steps = StepsOf(Run(new TSPBranchAndBound(), "(({a,b,c},{({a,b},1),({b,c},2),({a,c},3)}),5)"));
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.False(steps[^1].Ok);
        Assert.Empty(steps[^1].Partial.Order);
        // The only tour, a → b → c → a, costs 1 + 2 + 3 = 6.
        Assert.StartsWith("The cheapest tour costs 6, which is more than K = 5.", steps[^1].Caption);
    }

    [Fact]
    public void TspBranchAndBound_ARanOutOfTimeRun_EndsWithADoneStep() {
        ISolver solver = new TSPBranchAndBound { timerHasExpired = true };
        var run = solver.Run(new TSP().defaultInstance, withSteps: true);
        Assert.Equal("timeout", run.Answer);
        var done = Assert.Single(StepsOf(run));
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.False(done.Ok);
    }

    // ---- Minimum Traveling Salesperson ----

    [Fact]
    public void MinimumTspBruteForce_EveryNewBestTourIsAnAccept_EveryOtherOrderingARejectThatSaysWhy() {
        // The default K5 (see MINIMUMTSP_Tests): the orderings with a first cost 25, 23, 29, 27, 21, 21, 27, 19, ...; the
        // cheapest, 19, is the 8th of 24.
        var run = Run(new BruteForceMinimumTSP(), new MINIMUMTSP().defaultInstance);
        Assert.Equal("{a,c,b,e,d,a}", run.Answer);
        var steps = StepsOf(run);
        Assert.Equal(24 + 1, steps.Count);   // (5-1)! orderings, then Done
        Assert.Equal([0, 1, 4, 7], steps.Select((s, i) => (s, i)).Where(x => x.s.Event == StepEvent.Accept).Select(x => x.i));
        Assert.All(steps.Take(24).Where(s => s.Event != StepEvent.Accept), s => Assert.Equal(StepEvent.Reject, s.Event));
        Assert.DoesNotContain(steps, s => s.Event is StepEvent.Try or StepEvent.Backtrack);

        Assert.Equal("Try a → b → c → d → e → a: cost 25, the first tour found, so the best so far.", steps[0].Caption);
        Assert.Equal(["a", "b", "c", "d", "e", "a"], steps[0].Partial.Order);
        Assert.Empty(steps[0].Focus);
        Assert.Equal("Try a → b → c → e → d → a: cost 23, a new best tour (it beats 25).", steps[1].Caption);
        Assert.Equal("Try a → b → d → c → e → a: cost 29, not better than the best so far (23).", steps[2].Caption);
        Assert.Equal(new[] { "a-b", "b-d", "c-d", "c-e", "a-e" }.Order(), steps[2].Focus.Order());   // the whole tour failed
        // A tie is not better: the first of equally cheap tours stays the best.
        Assert.Equal("Try a → b → e → d → c → a: cost 21, not better than the best so far (21).", steps[5].Caption);
        Assert.Equal("Try a → c → b → e → d → a: cost 19, a new best tour (it beats 21).", steps[7].Caption);

        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.True(steps[^1].Ok);
        Assert.Equal(["a", "c", "b", "e", "d", "a"], steps[^1].Partial.Order);
        Assert.Equal("Tried all 24 orderings of the cities after a. The cheapest tour is a → c → b → e → d → a, cost 19.", steps[^1].Caption);
    }

    [Fact]
    public void MinimumTspBruteForce_AMissingHopIsARejectThatNamesIt() {
        // No edge c-d, so the first ordering is not a tour; the first tour is a,b,c,e,d,a (16), the cheapest a,d,b,c,e,a (14).
        const string instance = "({a,b,c,d,e},{({a,b},6),({b,c},1),({a,c},9),({a,e},5),({b,d},2),({b,e},8),({c,e},2),({d,e},3),({a,d},4)})";
        var steps = StepsOf(Run(new BruteForceMinimumTSP(), instance));
        Assert.Equal(StepEvent.Reject, steps[0].Event);
        Assert.Equal("Try a → b → c → d → e → a: there is no edge between c and d.", steps[0].Caption);
        Assert.Equal(["c", "d"], steps[0].Focus);
        Assert.Equal("Try a → b → c → e → d → a: cost 16, the first tour found, so the best so far.", steps[1].Caption);
        Assert.Equal(StepEvent.Accept, steps[1].Event);
        var last = steps.Last(s => s.Event == StepEvent.Accept);
        Assert.Equal("Try a → d → b → c → e → a: cost 14, a new best tour (it beats 16).", last.Caption);
        Assert.Equal("Tried all 24 orderings of the cities after a. The cheapest tour is a → d → b → c → e → a, cost 14.", steps[^1].Caption);
    }

    [Theory]
    [InlineData("({},{})", "There are no cities to tour.")]
    [InlineData("({a},{})", "A tour needs at least three cities, and there is one.")]
    [InlineData("({a,b},{({a,b},1)})", "A tour needs at least three cities, and there are two.")]
    [InlineData("({a,b,c,d},{({a,b},1),({b,c},1),({c,a},1),({c,d},1)})", "Tried all 6 orderings of the cities after a. None is a tour.")]
    public void MinimumTspBruteForce_WithNoTour_EndsWithADoneThatSaysWhy(string instance, string caption) {
        var run = Run(new BruteForceMinimumTSP(), instance);
        Assert.Equal("{}", run.Answer);
        var steps = StepsOf(run);
        Assert.Equal(caption, steps[^1].Caption);
        Assert.False(steps[^1].Ok);
        Assert.Empty(steps[^1].Partial.Order);
        Assert.DoesNotContain(steps, s => s.Event == StepEvent.Accept);
    }

    [Fact]
    public void MinimumTspBruteForce_PastTheCap_KeepsTheLastAccept_TheCheapestTour() {
        // K7 with every edge 10 except the cycle a-f-b-d-c-e-g-a, whose edges cost 1. That cycle (cost 7) is the only
        // cheapest tour; both its directions start a → f or a → g, the last two blocks of 120 orderings, so it is found
        // long after the first 150 steps.
        string[] cities = ["a", "b", "c", "d", "e", "f", "g"];
        var cheap = new HashSet<string> { "a-f", "b-f", "b-d", "c-d", "c-e", "e-g", "a-g" };
        var edges = new List<string>();
        for (int i = 0; i < 7; i++)
            for (int j = i + 1; j < 7; j++)
                edges.Add($"({{{cities[i]},{cities[j]}}},{(cheap.Contains($"{cities[i]}-{cities[j]}") ? 1 : 10)})");
        string instance = $"({{{string.Join(",", cities)}}},{{{string.Join(",", edges)}}})";

        var run = Run(new BruteForceMinimumTSP(), instance);
        Assert.Equal("{a,f,b,d,c,e,g,a}", run.Answer);
        var steps = StepsOf(run);
        Assert.Equal(StepRecorder<Tour>.DefaultCap + 2, steps.Count);   // the first 150, the last Accept, Done
        var lastAccept = steps[^2];
        Assert.Equal(StepEvent.Accept, lastAccept.Event);
        Assert.StartsWith("Try a → f → b → d → c → e → g → a: cost 7, a new best tour (it beats ", lastAccept.Caption);
        Assert.Equal(["a", "f", "b", "d", "c", "e", "g", "a"], lastAccept.Partial.Order);
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        // 720 orderings: 150 kept, the last Accept kept, 569 not shown.
        Assert.Equal("Tried all 720 orderings of the cities after a. The cheapest tour is a → f → b → d → c → e → g → a, cost 7. (569 not shown)", steps[^1].Caption);
    }

    // ---- the recorder cap ----

    [Fact]
    public void TheRecorderCapKeepsTheFirst150_AndDoneSaysHowManyAreHidden() {
        // K7 with every edge weight 10 and K = 69: all 5040 permutations are tours of 70, one over K.
        var nodes = Enumerable.Range(1, 7).Select(i => i.ToString()).ToList();
        var edges = new List<string>();
        for (int a = 0; a < 7; a++) for (int b = a + 1; b < 7; b++) edges.Add($"({{{nodes[a]},{nodes[b]}}},10)");
        string instance = $"(({{{string.Join(",", nodes)}}},{{{string.Join(",", edges)}}}),69)";
        var run = Run(new TSPBruteForce(), instance);
        Assert.Equal("{}", run.Answer);
        Assert.Equal(151, run.Steps.Count);
        Assert.Contains("not shown", ((SolverStep<Tour>)run.Steps[^1]).Caption);
    }

    [Fact]
    public void TspBruteForce_WithNoCities_SaysSo_AndDoneOkIsWhatTheVerifierSays() {
        const string instance = "(({},{}),5)";
        var run = Run(new TSPBruteForce(), instance);
        Assert.Equal("{}", run.Answer);
        var steps = StepsOf(run);
        var done = Assert.Single(steps);   // no "Try : total 0 is more than K" step for the one empty permutation
        Assert.Equal("There are no cities to tour.", done.Caption);
        Assert.Equal(((IProblem)new TSP()).defaultVerifier.verify(instance, run.Answer), done.Ok);
    }
}
