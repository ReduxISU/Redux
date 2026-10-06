# Contributing

Thanks for your interest in contributing! We welcome contributions of all kinds, including bug fixes, new features, documentation improvements, and suggestions.

---

## License

This project is licensed under the BSD 3-Clause License.

By contributing to this project, you agree that your contributions will be licensed under the same terms as the project (BSD 3-Clause License).

This is commonly referred to as an **"inbound = outbound"** licensing model.

---

## Contribution Requirements

By submitting a contribution (pull request, patch, or code submission), you confirm that:

1. You have the legal right to submit the contribution.
2. The contribution is your original work or properly attributed.
3. You grant the project a perpetual, worldwide, non-exclusive license to use, modify, and distribute your contribution.
4. Your contribution will be licensed under the same BSD 3-Clause License as the project.

---

## How to Contribute

New here? The [contributor guides](Documentation/guides/README.md) walk through every step below in detail, starting with the [setup guide](Documentation/guides/setup.md).

### 1. Fork the repository
Create your own fork of the project (<https://github.com/ReduxISU/Redux>), then clone your fork. The [setup guide](Documentation/guides/setup.md) shows how, including adding the main repo as a second remote named `upstream`.

### 2. Create a branch
Start from the newest `CSharpAPI` branch (the main branch; there is no `develop` branch) and use a descriptive branch name, for example `add-reduction-subsetsum-to-partition` or `fix-clique-verifier-parsing`.

### 3. Make your changes
- Keep changes focused and minimal
- Follow existing code style and conventions (formatting is enforced automatically, see below)
- Add comments where helpful
- Step-by-step guides: [adding a problem](Documentation/guides/adding-a-problem.md), [solver](Documentation/guides/adding-a-solver.md), [verifier](Documentation/guides/adding-a-verifier.md), [reduction](Documentation/guides/adding-a-reduction.md), and [visualization](Documentation/guides/adding-a-visualization.md)

### 4. Test your changes
Run these from the repo root (always name `Redux.slnx`; a bare `dotnet build` fails with `MSB1011`):

```bash
dotnet format Redux.slnx
dotnet build Redux.slnx -c Release
dotnet test Redux.slnx -c Release --filter "Category!=Performance"
dotnet test Redux.slnx -c Release --filter "Category=Performance"
```

- The Release build checks formatting and treats compiler warnings as errors, so run `dotnet format Redux.slnx` first.
- Add tests for what you add. Details and what to do when a check fails: [building-and-testing guide](Documentation/guides/building-and-testing.md) and [troubleshooting](Documentation/guides/troubleshooting.md).

### 5. Submit a Pull Request (PR)
Open the PR against the `CSharpAPI` branch of `ReduxISU/Redux`. Include:
- A clear description of what you changed
- Why the change is needed
- Any relevant screenshots or logs (if applicable)

Then wait for a review. Do not merge your own PR before it has been reviewed. After you push, read the rbs report on your PR: the rbs check always shows green, even when gates inside it fail (see the [building-and-testing guide](Documentation/guides/building-and-testing.md)).

---

## Code Style Guidelines

- Formatting (indentation, spacing) is set in [.editorconfig](.editorconfig) and checked by the Release build. Run `dotnet format Redux.slnx` to fix it.
- Compiler warnings fail the build, so fix them instead of ignoring them
- Write clear, readable code
- Use meaningful variable and function names
- Keep functions small and focused
- Follow existing project patterns

---

## Reporting Issues

When reporting bugs, please include:
- Steps to reproduce
- Expected behavior
- Actual behavior
- Environment details (OS, version, etc.)

---

## Feature Requests

We welcome ideas! Please:
- Clearly describe the feature
- Explain the use case
- Keep scope reasonable

---

## Code of Conduct

Be respectful and constructive in all interactions. Harassment or abusive behavior will not be tolerated.

---

## Questions

If you have questions, feel free to open an issue for discussion before submitting major changes.

---

Thanks for contributing!