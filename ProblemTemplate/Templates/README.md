## Problem Template
New problems should be added into the back end repository in the Problems/NPComplete folder. (Currently the back end is only set up to handle NP-Complete problems. If other problems are added in the future, where those are to be placed will need to be determined.) All files relating to a problem should be put in a folder titled NPC_PROBLEMNAME. This folder should include 4 files/folders.
* A file named PROBLEMNAME_Class.cs
* A folder named "Solvers"
* A folder named "Verifiers"
* A file named PROBLEMNAME_Controller.cs

*Any NPHFolders contain code for a NP Hard version of that problem, and include deprecated code, that may be useful as the code base is expanded.*

### Problem Class
The PROBLEMNAME_Class.cs should implement the `ProblemI` interface found in ProblemInterface.cs. This includes
* `string problemName` : Human readable problem name, this is what the problem will appear as in the GUI
* `string formalDefinition` : Definition in the form of {[problem variables] | "definition" }
* `string problemDefinition` : A more easily readable form of the above definition.
* `string source` : A formal citation of the source material for the problem definition
* `string wikiName` : This is deprecated, and should be removed from all problems
* `string defaultInstance` : A reasonably sized example of the problem, and the necessary format. *If the problem is of a similar form to an existing problem, such as a directed graph, the format should match the existing problems.*
* `string[] contributors` : A list of names of all developers who have worked on the problem
* `T defaultSolver` : An object of the default solver for the problem
* `U defaultVerifier` : An object of the default verifier for the problem

The PROBLEMNAME_Class.cs should also include any necessary problem variables, any functions necessary for parsing string instances into a variable, and two constructors. One constructor that takes a string instance, and one which uses the default instance. For any graph problems, use the objects and parsers found in Interfaces/graphs.

### Solvers
The Solvers folder should contain all solver files for that problem. Each of which implements the `SolverI` interface found in SolverInterface.cs. This includes
* `string solverName` : Human readable name of solving algorithm, this is what with appear in the GUI
* `string solverDefinition` : A brief description of the algorithm used to solve the problem
* `string source` : Formal citation of the source of the solving algorithm
* `string[] contributors` : A list of names of all developers who have worked on the solver

The file should also include a function which takes a problem object and returns a string of the solution, as well as any other necessary functions.\
*Because for now, all problems are NP-Complete, solution algorithms should return a complete solution.*

### Verifiers
The Verifiers folder should contain all verifier files for that problem. Each of which implements the `VerifierI` interface found in VerifierInterface.cs. This includes
* `string verifierName` : Human readable name of verifier, this is what with appear in the GUI
* `string verifierDefinition` : A brief description of the algorithm used to verify the problem
* `string source` : Formal citation of the source of the verifier algorithm
* `string[] contributors` : A list of names of all developers who have worked on the verifier

The file should also include a function which takes a problem object and certificate, and returns a Boolean for if the certificate is a solution to the given problem. As well as any other necessary functions.

### Problem Controller
PROBLEMNAME_Controller.cs should contain all necessary API endpoints for the problem. This includes
* API endpoint for general problem named PROBLEMNAMEGenericController, with the following calls,
    * `HttpGet` : Returns default problem object
    * `HttpGet("instance")` : Returns problem object for given parameter `problemInstance`
* API endpoint for each verifier class named VerifierNameController, with the following calls,
    * `HttpGet("info")` : Returns verifier object
    * `HttpGet("verify")` : Returns results of the verify function given parameters `certificate`, and `problemInstance`
* API endpoint for each solution class named SolverNameController, with the following calls,
    * `HttpGet("info")` : Returns solver object.
    * `HttpGet("solve")` : Returns solution string for given parameter, `problemInstance`

## DOD - Definition of Done
### New Problems
All added problems should fulfill the following requirements,
* Correctly implements all interfaces
* Includes at least one solver
* Includes at least one verifier
* Includes API endpoints for problem, all verifiers, and all solvers
* Tests for all solvers and verifiers have been created and pass

### API Additions
All API additions should fulfill the following requirements,
* The controller is named properly
* Controller is in the proper controller class for related problem
* Proper XML comments for all Http calls
