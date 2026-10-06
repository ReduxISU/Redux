# Redux Backend

**An interactive, dynamic knowledgebase of canonical Computer Science problems, solutions, and reductions**

[![Idaho State University](https://img.shields.io/badge/Idaho%20State%20University-Computer%20Science-orange)](https://www.isu.edu/cs/)

## Live Demo
- **Website**: [https://redux.portneuf.cose.isu.edu/](https://redux.portneuf.cose.isu.edu/)
- **API Documentation**: [https://api.redux.portneuf.cose.isu.edu/swagger/index.html](https://api.redux.portneuf.cose.isu.edu/swagger/index.html)

## Table of Contents
- [About Redux](#about-redux)
- [Quick Start](#quick-start)
- [Architecture](#architecture)
- [Contributing](#contributing)
- [Production Deployment](#production-deployment)
- [Contributors](#contributors)
- [Additional Resources](#additional-resources)
- [License](#license)
- [Contact & Support](#contact--support)

---

## About Redux

Redux is an extensible, interactive web-based platform designed for Computer Science pedagogy. It provides:

- **Interactive Problem Visualization**: Explore problems across complexity classes, from P to NP-Hard and beyond
- **Reduction Framework**: Understand how problems reduce to one another
- **Solver & Verifier Tools**: Execute and verify solutions to computational problems
- **Educational Resource**: Built on Karp's 21 NP-Complete problems and expanded across multiple complexity classes

The backend is designed to be adaptable and can work with different frontends. The default frontend can be found at [Redux_GUI](https://github.com/ReduxISU/Redux_GUI).

---

## Quick Start

Full instructions, including forking, Docker, and the dev container, are in the [setup guide](Documentation/guides/setup.md). The short version:

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download) (and [Node.js](https://nodejs.org/en/download) only if you also want to run the frontend).
2. Clone the repo and run the API from the repo root:

   ```bash
   git clone https://github.com/ReduxISU/Redux.git
   cd Redux
   dotnet run
   ```

   The API listens on `http://127.0.0.1:27000/`.
3. Open Swagger, the interactive API page, at `http://127.0.0.1:27000/swagger/index.html`.

For automatic reloading while you edit, use `dotnet watch --project API.csproj run`. To run the Docker image, use `docker build -t reduxapi .` and then `docker run -it --rm -p 27000:27000 --name reduxapi reduxapi`.

---

## Architecture

Redux has five kinds of building blocks. Each one is a C# class that implements an interface from the `Interfaces/` folder, and the API finds them automatically (no controllers to write):

1. **IProblem**: a problem, with its definition and default example
2. **ISolver**: solves a problem instance
3. **IVerifier**: checks whether an answer (a certificate) is correct
4. **IVisualization**: turns a problem into a picture the frontend can draw
5. **IReduction**: turns an instance of one problem into an instance of another

```
Redux/
├── Problems/
│   ├── NPComplete/          # NPC_<NAME>/ folders: the main problem set
│   ├── NPHard/              # NPH_<NAME>/
│   └── P/                   # P_<NAME>/
│       └── each problem has <NAME>_Class.cs, Solvers/, Verifiers/,
│           Visualizations/, and ReduceTo/NPC_<TO>/ (reductions)
├── Interfaces/              # The five interfaces, enums, and graph utilities
├── AdditionalControllers/   # ProblemProvider and Navigation/ (API endpoints)
├── ProblemTemplate/         # Starter-file templates
├── redux-tests/             # Tests
└── API.csproj               # Main project file
```

The Navigation controllers in `AdditionalControllers/Navigation/` are heavily used by the frontend, so change them carefully. The [how the code works](Documentation/guides/how-the-code-works.md) guide explains the folders, the interfaces, graph utilities, the SPADE instance parser, and the Navigation controllers.

---

## Contributing

We welcome contributions! Start with the [contributor guides](Documentation/guides/README.md), which cover setup, how the code works, building and testing, and step-by-step guides for adding a problem, solver, verifier, reduction, or visualization. [CONTRIBUTING.md](CONTRIBUTING.md) has the license terms and the pull request workflow.

In short: fork the repo, make a branch, open a pull request to the `CSharpAPI` branch, and wait for review before merging.

Join the community on Discord: [https://discord.gg/sEC3rTXn2Z](https://discord.gg/sEC3rTXn2Z)

---

## Production Deployment

How the live server is run, restarted, and updated is documented in [Documentation/production.md](Documentation/production.md).

---

## Contributors

This project is developed by students and faculty at Idaho State University's Computer Science Department.

For a complete list of contributors, visit our [About Us page](https://redux.portneuf.cose.isu.edu/aboutus).

---

## Additional Resources

### Documentation Links
- [GitHub Repository](https://github.com/ReduxISU/Redux)
- [Contributor guides](Documentation/guides/README.md)
- [Wikipedia: What is NP-Complete?](https://en.wikipedia.org/wiki/NP-completeness)
- [Karp's 21 NP-Complete Problems](https://en.wikipedia.org/wiki/Karp%27s_21_NP-complete_problems)
- [Redux GUI Documentation](https://github.com/ReduxISU/Redux_GUI)
- [SPADE Parser](https://github.com/Jetison333/SPADE)

### Related Repositories
- **Frontend**: [Redux_GUI](https://github.com/ReduxISU/Redux_GUI)
- **Quantum Solver**: [quantumsolver](https://github.com/ReduxISU/quantumsolver)

---

## License

This project is licensed under the BSD 3-Clause License. See [LICENSE](LICENSE) for details.

---

## Contact & Support

- **Issues**: Please use GitHub Issues for bug reports and feature requests
- **Discord**: [Join our community](https://discord.gg/sEC3rTXn2Z)
- **Email**: Contact the Redux team at [redux@isu.edu](mailto:redux@isu.edu)
