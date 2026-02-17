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

Redux is a web-based platform that makes computational complexity theory accessible and interactive. It provides:

- **Interactive Problem Visualization**: See canonical NP-Complete problems in action
- **Reduction Framework**: Understand how problems reduce to one another
- **Solver & Verifier Tools**: Execute and verify solutions to computational problems
- **Educational Resource**: Built on Karp's 21 NP-Complete problems and beyond

The backend is designed to be adaptable and can work with different frontends. The default frontend can be found at [Redux_GUI](https://github.com/ReduxISU/Redux_GUI).

---

## Features

 **NP-Complete Problem Library**: Comprehensive collection of canonical CS problems  
 **RESTful API**: Full API access with Swagger documentation  
 **Solver Algorithms**: Multiple solving strategies for each problem  
 **Certificate Verification**: Verify solutions efficiently  
 **Reduction Mappings**: Visualize and understand problem reductions  
 **Graph & SAT Visualizations**: Interactive problem representations  

---

## Quick Start

### Prerequisites

- [.NET 6.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
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
├── Solvers/                  # Solver implementations
├── Verifiers/                # Verifier implementations
└── PROBLEMNAME_controller.cs # API endpoints
```

#### Interfaces

Redux uses five main interfaces that problems must implement:

1. **IProblem** - Main problem interface with solver, verifier, and visualization
2. **ISolver** - Solves problem instances
3. **IVerifier** - Verifies solution certificates
4. **IVisualization** - Creates visual representations
5. **IReduction** - Maps one problem to another

For detailed interface documentation, see the [Interfaces](#interfaces-detail) section below.

### API Usage

The Redux API is documented using SwaggerUI. All endpoints can be tested directly from the Swagger interface at:
- Production: `https://api.redux.portneuf.cose.isu.edu/swagger/index.html`
- Local: `http://127.0.0.1:27000/swagger/index.html`

#### Adding API Endpoints

When adding an API endpoint, the current practice is to add a controller into the problem reduction controller class, which corresponds to a specific class for that problem reduction. Each reduction has its own controller. The current naming convention is `NameOfRelatedClassController`. 

**Important:** If not named correctly, the GUI will not function properly.

**Controller Attributes:**

![API Attributes](./images/APIAttributes.png)

Each controller should include:
- `[Route("controller")]`
- `[Tag("Problem Name")]`

#### XML Documentation Comments

API calls must include proper XML comments to appear in SwaggerUI documentation. Add these comments above each HTTP call:

- `<summary>` - Brief description of API call
- `<param name="parameterName" example="example value">` - Description of parameter
- `<response code="200">` - What call returns

**Example:**
```csharp
/// <summary>Brief description of API call</summary>
/// <param name="parameterName" example="example value">Description of parameter</param>
/// <response code="200">What call returns</response>
```

### SPADE Parser

SPADE is used for parsing instance strings into usable data structures. It should be used in problem class constructors. 

Documentation: [SPADE GitHub](https://github.com/Jetison333/SPADE/blob/main/Documentation/index.md)

Example usage can be found in the Knapsack problem class.

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
- **Weekly Meetings**: Thursdays at 11:30 AM MT via [Zoom](https://isu.zoom.us/j/85203480771?pwd=oEMlnn5EItmPFy3OKHnLqENQF52OIK.1&jst=3)

### Branching Strategy

- **Production Branch**: `CSharpAPI`
- **Development Branch**: `develop`

**Workflow**:
1. Make changes on the `develop` branch (or create a feature branch)
2. Create a pull request to `develop`
3. Assign a reviewer
4. After code review, merge into `develop`
5. Periodically merge `develop` into `CSharpAPI` for production

** Important**: DO NOT complete pull requests before they are reviewed.

### Definition of Done

#### New Problems
-  Correctly implements all interfaces
-  Includes at least one solver
-  Includes at least one verifier
-  Tests created and passing

#### New Reductions
-  Correctly implements all interfaces
-  Includes working solution mapping function
-  Located in correct folder
-  Has API endpoint for reduction info, reduced string, and mapped solution

#### API Additions
-  Controller named properly
-  Controller in proper controller class
-  Proper XML comments for all HTTP calls

### Adding New Problems

1. Create folder: `Problems/NPComplete/NPC_PROBLEMNAME/`

2. Implement required files:

**Folder Structure:**

![Problem Folder Structure](./images/ProblemFolder.png)

Each problem folder should include 4 files/folders:
   - `PROBLEMNAME_class.cs` (implements `IProblem`)
   - `Solvers/` folder with at least one solver
   - `Verifiers/` folder with at least one verifier
   - `PROBLEMNAME_controller.cs` with API endpoints

3. Write tests
4. Submit pull request

### Testing

Testing uses **Xunit**. All new problems should include tests for:
- Verifier correctness
- Solver correctness
- Reduction algorithms

Run tests with:
```bash
dotnet test
```

---

## Interfaces Detail

### IProblem
Main interface with generic types for `ISolver`, `IVerifier`, and `IVisualization`.

**Required Fields**:
- `problemName` - Human-readable name
- `formalDefinition` - Mathematical definition
- `problemDefinition` - Readable description
- `source` - Citation
- `defaultInstance` - Example instance
- `contributors` - Developer names
- `defaultSolver` - Default solver object
- `defaultVerifier` - Default verifier object

**Required Constructors**:
- Constructor taking a string instance
- Constructor using default instance

### ISolver
Implements solving algorithms.

**Required Methods**:
- `Solve(problem)` → Returns solution string
- `GetSteps()` → (Optional) Returns first 99 solution steps

### IVerifier
Verifies solution certificates.

**Required Methods**:
- `Verify(problem, certificate)` → Returns boolean

### IVisualization
Creates visual representations.

**Visualization Types**:
- `Boolean Satisfiability` - Uses `API_SAT`
- `Graph D3` - Uses `API_graph`

**Required Methods**:
- `Visualize(problem)` → Returns `API_JSON`
- `SolvedVisualization(problem, solution)` → (Optional) Highlights solution
- `StepsVisualization(steps)` → (Optional) Visualizes steps

### IReduction
Maps one problem to another.

**Required Fields**:
- `reductionFrom` - Starting problem
- `reductionTo` - Resulting problem
- `gadgets` - UI element relationships for highlighting

**Required Methods**:
- `Reduce()` - Performs the reduction
- `MapSolutions(certificate)` - Maps solution from one problem to another

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

This project is developed at Idaho State University's Computer Science Department.

---

## Contact & Support

- **Issues**: Please use GitHub Issues for bug reports and feature requests
- **Discord**: [Join our community](https://discord.gg/sEC3rTXn2Z)
- **Email**: Contact the CS department at Idaho State University

---

