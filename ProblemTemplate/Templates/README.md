## Problem Template
New problems should be added into the back end repository in the Problems/NPComplete folder. (Currently the back end is only set up to handle NP-Complete problems. If other problems are added in the future, where those are to be placed will need to be determined.) All files relating to a problem should be put in a folder titled NPC_PROBLEMNAME. This folder should include 4 files/folders.
* A file named PROBLEMNAME_Class.cs
* A folder named "Reductions" (if applicable)
* A folder named "Solvers"
* A folder named "Verifiers"
* A folder named "Visualizations"

### Problem Class
The PROBLEMNAME_Class.cs should implement the `IProblem` interface or one of its variants, found in ProblemInterface.cs. This includes
* `string problemName` : Human readable problem name, this is what the problem will appear as in the GUI
* `string problemLink` : Link to an information on the problem, not neccesarily a formal definition 
* `string formalDefinition` : Definition in the form of {[problem variables] | "definition" }
* `string problemDefinition` : A more easily readable form of the above definition.
* `string source` : A formal citation of the source material for the problem definition
* `string sourceLink` : A link to the formal citation
* `string wikiName` : This is deprecated, and should be removed from all problems
* `string defaultInstance` : A reasonably sized example of the problem, and the necessary format. *If the problem is of a similar form to an existing problem, such as a directed graph, the format should match the existing problems.*
* `string instanceFormat` : Short descriptive sentence with a concrete embedded example of the instance string the problem accepts. Consumed by `/ProblemProvider/info` and by parse-error responses, so this is the canonical place to tell an LLM, the GUI, or a human what shape your instances take. Prefer a sentence + example over a formal grammar.
* `string certificateFormat` : Same shape as `instanceFormat`, but describes what the verifier accepts as a certificate. The default verifier defines this; if your problem has multiple verifiers, declare the format the *default* one expects here.
* `string[] contributors` : A list of names of all developers who have worked on the problem
* `T defaultSolver` : An object of the default solver for the problem
* `U defaultVerifier` : An object of the default verifier for the problem
* `V defaultVisualization` : An objet of the default visualization for the problem

The PROBLEMNAME_Class.cs should also include any necessary problem variables, any functions necessary for parsing string instances into a variable, and two constructors. One constructor that takes a string instance, and one which uses the default instance.

The string-instance constructor should **throw `ProblemParseException`** (defined in `Interfaces/ParseExceptions.cs`) on malformed input rather than silently constructing a half-populated object. The controller layer catches this exception and returns HTTP 400 with `instanceFormat` quoted back to the caller, which is what lets every client — MCP wrappers, the GUI, an LLM agent — recover from a bad input by reading the structured error rather than parsing a stack trace.

### Reductions
The Reductions folder should contain all reduction files for that problem. Each of which implements the `IReduction` interface found in ReductionInterface.cs. This includes
* `string reductionName` : Human readable name of reduction algorithm, this is what with appear in the GUI
* `string reductionDefinition` : A brief description of the algorithm used to reduce the problem
* `string source` : Formal citation of the source of the reduction algorithm
* `string sourceLink` : A link to the formal citation
* `string[] contributors` : A list of names of all developers who have worked on the reduction
* 'List<Gadget> gadgets' : A list of gadgets used in the reduction to visually represent the reduction elements
* '{PROBLEM} _reductionFrom' : An instance of the problem we are reducing from
* '{PROBLEM} _reductionTo' :   An instance of the problem we are reducing to 

### Solvers
The Solvers folder should contain all solver files for that problem. Each of which implements the `ISolver` interface found in SolverInterface.cs. This includes
* `string solverName` : Human readable name of solving algorithm, this is what with appear in the GUI
* `string solverDefinition` : A brief description of the algorithm used to solve the problem
* `string source` : Formal citation of the source of the solving algorithm
* `string sourceLink` : A link to the formal citation
* `string[] contributors` : A list of names of all developers who have worked on the solver
* `bool timerHasExpired` : bool that says if the timer for a problem has expired

The file should also include a function which takes a problem object and returns a string of the solution, as well as any other necessary functions.\
*Because for now, most problems are NP-Complete, solution algorithms should return a complete solution.*

### Verifiers
The Verifiers folder should contain all verifier files for that problem. Each of which implements the `IVerifier` interface found in VerifierInterface.cs. This includes
* `string verifierName` : Human readable name of verifier, this is what with appear in the GUI
* `string verifierDefinition` : A brief description of the algorithm used to verify the problem
* `string source` : Formal citation of the source of the verifier algorithm
* `string sourceLink` : A link to the formal citation
* `string[] contributors` : A list of names of all developers who have worked on the verifier

The file should also include a function which takes a problem object and certificate, and returns a Boolean for if the certificate is a solution to the given problem. As well as any other necessary functions.

The verifier should **throw `CertificateParseException`** (defined in `Interfaces/ParseExceptions.cs`) on malformed certificate input rather than silently returning `false`. The controller catches it and returns HTTP 400 with `problem.certificateFormat` quoted back to the caller. Return `false` only when the certificate parses successfully but fails the verification predicate.

### Visualizations
The Visualizations folder should contain all visualizations files for that problem. Each of which implements the `IVisualization` interface found in VisualizationInterface.cs. This includes
* `string visualizationName` : Human readable name of visualization, this is what with appear in the GUI
* `string visualizationDefinition` : A brief description of the visualization used to visualize the problem
* `string source` : Formal citation of the source of the visualization algorithm
* `string sourceLink` : A link to the formal citation
* `string[] contributors` : A list of names of all developers who have worked on the visualization
* `ISolver solver` : The solver to use for this visualization. If `StepsVisualization` is not implemented then setting it to the default solver is fine

The file should also include two functions which take a problem object and solution string, and returns an API object of the visualization for the given problem. As well as any other necessary functions.

## DOD - Definition of Done
### New Problems
All added problems should fulfill the following requirements,
* Correctly implements all interfaces
* Includes at least one solver
* Includes at least one verifier
* Tests for all solvers and verifiers have been created and pass
