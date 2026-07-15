# Redux Backend

**An interactive, dynamic knowledgebase of canonical Computer Science problems, solutions, and reductions**

[![Idaho State University](https://img.shields.io/badge/Idaho%20State%20University-Computer%20Science-orange)](https://www.isu.edu/cs/)

##  Live Demo
- **Website**: [https://redux.portneuf.cose.isu.edu/](https://redux.portneuf.cose.isu.edu/)
- **API Documentation**: [https://api.redux.portneuf.cose.isu.edu/swagger/index.html](https://api.redux.portneuf.cose.isu.edu/swagger/index.html)

##  Table of Contents
- [About Redux](#about-redux)
- [Features](#features)
- [Quick Start](#quick-start)
- [Documentation](#documentation)
- [Architecture](#architecture)
- [Contributing](#contributing)
- [Contributors](#contributors)
- [Additional Resources](#additional-resources)

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

The recommended path is the **dev container** (below): it needs only a container runtime and
gives everyone the same .NET 10 toolchain. Prefer to install toolchains on your host instead?
Skip to [Prerequisites](#prerequisites).

### Recommended: Dev Container

The dev container pins the .NET 10 SDK and a shared task runner ([mise](https://mise.jdx.dev/))
so VS Code, a plain terminal, and CI all build and test the same way. **No local .NET SDK or
Node.js is required for backend work** — the container provides them.

**Dependencies**

- [Docker](https://docs.docker.com/get-docker/) (or a compatible container runtime), and
- one of:
  - **VS Code** + the [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers) (or a [GitHub Codespace](https://github.com/features/codespaces)), or
  - the [`@devcontainers/cli`](https://github.com/devcontainers/cli) for terminal-based editors (neovim, etc.): `npm install -g @devcontainers/cli` (needs Node.js on your host).

**VS Code / Codespaces**

Open the repo and choose **Reopen in Container**. The first build restores packages
automatically; then use the tasks below from the integrated terminal.

**Terminal (devcontainer CLI)**

```bash
# create/start the container (first run pulls the image + restores packages)
devcontainer up --workspace-folder .

# run tasks inside it
devcontainer exec --workspace-folder . mise run test   # restore -> build -> test
devcontainer exec --workspace-folder . mise run run    # API on http://localhost:27000
```

**Tasks** — the same `mise run <task>` names work in VS Code, the terminal, and CI:

| Task | What it does |
|------|--------------|
| `mise run restore` | Restore NuGet packages |
| `mise run build`   | Build the solution (Release) |
| `mise run test`    | Restore → build → test (mirrors CI) |
| `mise run run`     | Run the API on http://localhost:27000 (Swagger at `/swagger/index.html`) |
| `mise run watch`   | Run the API with hot reload |

Port **27000** is published to your host, so a browser or the
[Redux_GUI](https://github.com/ReduxISU/Redux_GUI) frontend can reach the API at
`http://localhost:27000` while you develop.

> **Working on the GUI too (e.g. a reduction)?** Run this backend container, then start the
> frontend separately and point its `REDUX_BASE_URL` at `http://localhost:27000/`. The two are
> independent containers wired over HTTP — you do not need both in one container.

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download)
- [Node.js](https://nodejs.org/en/download) (for frontend)
- [Visual Studio](https://visualstudio.microsoft.com/) or your preferred IDE

### Installation

1. **Clone the repositories**

   ```bash
   # Backend
   git clone https://github.com/ReduxISU/Redux.git
   
   # Frontend (optional, for full local setup)
   git clone https://github.com/ReduxISU/Redux_GUI.git
   ```

2. **Run the Backend**

   Navigate to the Redux directory and run:

   ```bash
   dotnet run
   ```

   The API will be available at `http://127.0.0.1:27000/`

3. **Access Swagger API Documentation**

   Open your browser to: `http://127.0.0.1:27000/swagger/index.html`

### Development Mode

For automatic reloading during development:

```bash
dotnet watch --project API.csproj run -- --project API.csproj
```

### Docker Deployment

```bash
docker build -t reduxapi .
docker run -it --rm -p 27000:80 --name reduxapi reduxapi
```

---

## Documentation

### Core Concepts

#### Problems
All problems are located in `Problems/NPComplete/`. Each problem follows a standardized structure:

```
NPC_PROBLEMNAME/
├── PROBLEMNAME_class.cs      # Implements IProblem interface
├── Reductions/               # Reduction implementations
├── Solvers/                  # Solver implementations
├── Verifiers/                # Verifier implementations
└── Visualizations            # Visualization implementations
```

#### Interfaces

Redux uses five main interfaces that problems must implement:

1. **IProblem** - Main problem interface with solver, verifier, and visualization
2. **ISolver** - Solves problem instances
3. **IVerifier** - Verifies solution certificates
4. **IVisualization** - Creates visual representations
5. **IReduction** - Maps one problem to another

For detailed interface documentation, see the [Interfaces](#interfaces-detail) section below.

### SPADE Parser

SPADE is used for parsing instance strings into usable data structures and should be used in problem class constructors where supported. Note that SPADE may not support all input types — verify compatibility before use.

Documentation: [SPADE GitHub](https://github.com/Jetison333/SPADE/blob/main/Documentation/index.md)

Example usage can be found in the Clique problem class.

---

## Architecture

### Backend Structure

```
Redux/
├── Problems/
│   └── NPComplete/          # NP-Complete problem implementations
├── Interfaces/              # Core interfaces and graph utilities
├── AdditionalControllers/
│   └── Navigation/          # API controllers for problem retrieval
└── API.csproj              # Main project file
```

### Key Components

#### Graph Utilities
For graph-based problems, use `UtilCollectionGraph` from the Interfaces folder. It includes:
- Automatic handling of directed/undirected graphs
- Weight management
- `ToAPIGraph()` conversion for API responses

#### Navigation Controllers
Located in `AdditionalControllers/Navigation/`, these controllers handle:
- Retrieving available problems
- Listing algorithms
- Problem metadata

** Caution**: The frontend heavily relies on these controllers. Changes should be made carefully.

---

## Contributing

We welcome contributions! Join our community:

- **Discord**: [https://discord.gg/sEC3rTXn2Z](https://discord.gg/sEC3rTXn2Z)

### Branching Strategy

- **Production Branch**: `CSharpAPI`
- **Development Branch**: `develop`

**Workflow**:
1. Fork the Redux API repo and clone it
2. Create a feature branch on your forked repo
3. Implement changes
4. Create a pull request to CSharpAPI
5. Assign a reviewer
6. After code review, merge into CSharpAPI

** Important**: DO NOT complete pull requests before they are reviewed.

### Definition of Done

#### New Problems
-  Correctly implements all interfaces
-  Includes at least one solver
-  Includes at least one verifier
-  Tests created and passing
-  Filled out all metadata fields

#### New Reductions
-  Correctly implements all interfaces
-  Includes working solution mapping function
-  Includes working gadget mapping function
-  Filled out all metadata fields

### Adding New Problems

1. Create folder: `Problems/NPComplete/NPC_PROBLEMNAME/`

2. Implement required files:

**Folder Structure:**

![Problem Folder Structure](./images/ProblemFolder.png)

Each problem folder should include 4 files/folders:
   - `PROBLEMNAME_class.cs` (implements `IProblem`)
   - `Solvers/` folder with at least one solver
   - `Verifiers/` folder with at least one verifier
   - `Visualizations/` folder if applicable
   - `Reductions/` folder if applicable

3. Write tests
4. Submit pull request

### Testing

Testing uses **Xunit**. All new problems should include tests for:
- Verifier correctness
- Solver correctness
- Reduction algorithms if applicable

Run tests with:
```bash
dotnet test
```

---

## Interfaces Detail

For comprehensive interface details see [Problem Template README](https://github.com/ReduxISU/Redux/blob/CSharpAPI/ProblemTemplate/Templates/README.md).

---

## Production Deployment

### SystemD Service (Linux)

1. Install service file to `/etc/systemd/system/redux.service`
2. Configure paths for your environment
3. Enable and start:

```bash
systemctl daemon-reload
systemctl enable redux.service
systemctl start redux.service
```

### Updating Production

```bash
cd [working directory]
git pull origin
sudo systemctl restart redux.service
```

### Viewing Logs

```bash
journalctl -xeu redux
```

For complete production setup instructions, see the production documentation in the repository.

---
## Contributors

This project is developed by students and faculty at Idaho State University's Computer Science Department.

For a complete list of contributors, visit our [About Us page](https://redux.portneuf.cose.isu.edu/aboutus).

---

## Additional Resources

### Documentation Links
- [GitHub Repository](https://github.com/ReduxISU/Redux)
- [Wikipedia: What is NP-Complete?](https://en.wikipedia.org/wiki/NP-completeness)
- [Karp's 21 NP-Complete Problems](https://en.wikipedia.org/wiki/Karp%27s_21_NP-complete_problems)
- [Redux GUI Documentation](https://github.com/ReduxISU/Redux_GUI)
- [SPADE Parser](https://github.com/Jetison333/SPADE)

### Related Repositories
- **Frontend**: [Redux_GUI](https://github.com/ReduxISU/Redux_GUI)
- **Quantum Solver**: [quantumsolver](https://github.com/ReduxISU/quantumsolver)

---

## License

This project is licensed under the BSD 3-Clause License. See [LICENSE.md](LICENSE.md) for details.

---

## Contact & Support

- **Issues**: Please use GitHub Issues for bug reports and feature requests
- **Discord**: [Join our community](https://discord.gg/sEC3rTXn2Z)
- **Email**: Contact the Reudx email [redux@isu.edu](mailto:redux@isu.edu) 

---

