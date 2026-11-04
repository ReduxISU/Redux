# Contributor guides: start here

Welcome. These guides explain, step by step, how to contribute to Redux. They are written for people new to the project, so do not worry if you have never done this before.

## Suggested reading order for newcomers

1. [Setup](setup.md): install what you need, fork and clone, and run Redux for the first time.
2. [How the code works](how-the-code-works.md): a plain-language map of the folders and the five interfaces.
3. [Building and testing](building-and-testing.md): how to check your work before you push, and what to do when the automatic checks on your pull request (PR) turn red.
4. Then the task guide for what you want to do (next section).

When something breaks, go to [Troubleshooting](troubleshooting.md).

## Which guide do I need?

| I want to... | Read |
| --- | --- |
| Install the tools and run Redux on my computer | [setup.md](setup.md) |
| Understand what the folders and interfaces are | [how-the-code-works.md](how-the-code-works.md) |
| Check my work, or fix a red check on my PR | [building-and-testing.md](building-and-testing.md) |
| Fix an error message | [troubleshooting.md](troubleshooting.md) |
| Add a new problem | [adding-a-problem.md](adding-a-problem.md) |
| Add a way to solve a problem (a solver) | [adding-a-solver.md](adding-a-solver.md) |
| Add a way to check an answer (a verifier) | [adding-a-verifier.md](adding-a-verifier.md) |
| Add a reduction from one problem to another | [adding-a-reduction.md](adding-a-reduction.md) |
| Add a visualization (a picture of a problem) | [adding-a-visualization.md](adding-a-visualization.md) |

A brand-new problem usually needs all of these in order: the problem, then a verifier, a solver, and a visualization, then reductions that connect it to other problems.

## Working on a CS 4473/5573 assignment?

Each programming assignment can be its own PR. Leave out the parts you have not reached yet: a problem with no solver, verifier or visualization uses a placeholder, and its PR can still pass every check.

| Assignment | What your PR adds | Guide | What CI checks | Also required (the reviewer checks) |
| --- | --- | --- | --- | --- |
| Problem Implementation | `Problems/NPComplete/NPC_<NAME>/<NAME>_Class.cs`. If you used the template, delete the generated solver, verifier and visualization files. | [adding-a-problem.md](adding-a-problem.md) | No field still says `TODO`. `complexityClass`, `problemType` and `problemVariant` are declared. The class builds from its default instance, and its constructor rejects garbage input (any exception counts). | A malformed instance throws `ProblemParseException` specifically, with a message that says what is wrong. |
| Problem Instances | A better default instance (`_defaultInstance` in the class file) | [adding-a-problem.md](adding-a-problem.md), Step 2 | The class still builds from the new default instance. If there is a `CertificateExample`, it must still pass its verifier on the new instance. | The instance is small enough to read but interesting enough to teach something. |
| Verifier Implementation | `Verifiers/<Name>Verifier.cs`, plus tests | [adding-a-verifier.md](adding-a-verifier.md) | `verifierName` is exactly `<problemName> Solution Verifier`, or `<problemName> Optimality Verifier` for one that checks the answer is the best. If you add a `CertificateExample`, it must pass on the default instance. | A `CertificateExample`. CI does not require one, but both front ends use it to fill in the answer box. |
| Solver Implementation | `Solvers/<Name>BruteForce.cs`, `Solvers/<Name>Backtracking.cs`, or a file named for the algorithm, plus tests | [adding-a-solver.md](adding-a-solver.md) | `solverType`, `complexityBucket` and `timeComplexity` are declared. Started with the time limit already passed, the solver stops and returns `SolveRun.TimedOutAnswer`. Its display name looks like `Clique Brute Force` (`<Problem Name> <Approach>`) or `Dijkstra's Algorithm` (a person's name). | The time-limit check sits inside the expensive loops, so the solver also stops partway through a long search. CI tests that on the real clock only for the slow solvers already listed in `SolverTimeLimit_Tests.cs`. |
| Reduction Proof | Nothing in Redux. This one is written, not coded. | | | |
| Reduction Implementation | `Problems/NPComplete/NPC_<FROM>/ReduceTo/NPC_<YOURS>/<ClassName>.cs`, plus tests. The file goes in the folder of the problem the reduction starts **from**, in a subfolder named for your problem. | [adding-a-reduction.md](adding-a-reduction.md) | Its display name looks like `Partition Reduction` or `Karp's 3SAT Reduction`. | |
| Creative Leap (visualization) | A visualization for the [Redux_GUI](https://github.com/ReduxISU/Redux_GUI) website | [adding-a-visualization.md](adding-a-visualization.md) | `visualizationType` is a real type, not `Unimplemented`. | Choosing a type Redux_GUI already draws means you change only one file. |
| Midterm demo | Nothing. Run Redux and Redux_GUI on your own computer. | [setup.md](setup.md), sections 3 and 7 | | |

If a check is added or changed while your PR is open, a maintainer comments on your PR to say what to change.

## Other documentation

- [Documentation home](../index.md)
- [CONTRIBUTING.md](../../CONTRIBUTING.md): the license terms and how to open a pull request
- [production.md](../production.md): how the live server is run (for maintainers)
