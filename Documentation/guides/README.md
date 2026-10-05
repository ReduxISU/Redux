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

## Other documentation

- [Documentation home](../index.md)
- [CONTRIBUTING.md](../../CONTRIBUTING.md): the license terms and how to open a pull request
- [production.md](../production.md): how the live server is run (for maintainers)
- [AGENTS.md](../../AGENTS.md): short instructions for AI coding assistants working in this repo
