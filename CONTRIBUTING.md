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

### Development environment

The easiest way to develop is the **dev container**: it provides the correct .NET 10 toolchain
and a shared `mise run <task>` command set, so you install nothing on your host and your
environment matches CI. Open the repo in VS Code (**Reopen in Container**) or run
`devcontainer up` from a terminal. See **Quick Start → Recommended: Dev Container** in the
[README](./Readme.md) for full setup.

Inside the container: `mise run run` starts the API on `http://localhost:27000`, and
`mise run test` runs the full test suite.

### 1. Fork the repository
Create your own fork of the project.

### 2. Create a branch
Use a descriptive branch name:

### 3. Make your changes
- Keep changes focused and minimal
- Follow existing code style and conventions
- Add comments where helpful

### 4. Test your changes
- Ensure the project builds successfully
- Run any existing tests (`mise run test` in the dev container)
- Add tests if applicable

### 5. Submit a Pull Request (PR)
Include:
- A clear description of what you changed
- Why the change is needed
- Any relevant screenshots or logs (if applicable)

---

## Code Style Guidelines

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