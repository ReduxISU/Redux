using API.Interfaces;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Solvers;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Verifiers;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Visualizations;
using SPADE;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION;

class CONSTRAINTSATISFACTION 
    : IProblem<
        ConstraintSatisfactionSolver, 
        ConstraintSatisfactionVerifier, 
        ConstraintSatisfactionVisualization
    > {

    // --- Fields ---
    public string problemName {get;} = "Constraint Satisfaction Problem";
    public string problemLink {get;} = "https://en.wikipedia.org/wiki/Constraint_satisfaction_problem";
    public string formalDefinition {get;} = "CONSTRAINTSATISFACTION = {<X, D, C> | there exists a complete and consistent assignment of values to all variables in X}";
    public string problemDefinition {get;} = "The Constraint Satisfaction Problem is a problem in which, given a list of variables, a list of their respective nonempty domains of possible values, and a set of constraints, the goal is to determine whether there exists an assignment of values to all variables that satisfies every constraint.";
    public string inputDescription {get;} = "X, a list of distinct variables; D, a list of nonempty domains where the ith domain corresponds to the ith variable in X; and C, a set of constraint strings";
    public string outputDescription {get;} = "True or False, indicating whether there exists an assignment of values to all variables in X, such that each variable is assigned a value from its corresponding domain and all constraints are satisfied.";
    public string source {get;} = "S. Russell and P. Norvig, “Constraint Satisfaction Problems,” in Artificial Intelligence: A Modern Approach, 3rd ed. New Jersey: Pearson, 2010, pp. 202–233.";
    public string sourceLink {get;} = "https://aima.eecs.berkeley.edu/3rd-ed/";
    public string sourceFile { get; } = SourceFile.Path();
    private static readonly string _defaultInstance =
    "((x,y),({1,2},{1,2}),{\"x!=y\"})";
    public string defaultInstance {get;} = _defaultInstance;
    // Short descriptive sentence with a concrete example. Consumed by
    // /ProblemProvider/info and by parse-error responses; this is the
    // canonical place to tell an LLM (or a human, or the GUI) what
    // shape your instance/certificate strings take.
    public string instanceFormat { get; } =
        "Format: (X,D,C), where X is a list of variables, " +
        "D is a list of sets where the ith set is the nonempty domain for the ith variable in X, and " +
        "C is a set of constraints where each constraint is a string. " +
        $"Example: {_defaultInstance}";

    public string certificateFormat { get; } =
        "Format: a set of (variable,value) pairs representing a satisfying assignment. " +
        "Each variable must be assigned exactly one value " +
        "from its domain, and all constraints must be satisfied. " +
        "Example: {(x,1),(y,2)}";
    public string instance {get;set;} = string.Empty;
    public string wikiName {get;} = "";
    public ConstraintSatisfactionSolver defaultSolver {get;} = new ConstraintSatisfactionSolver();
    public ConstraintSatisfactionVerifier defaultVerifier {get;} = new ConstraintSatisfactionVerifier();
    public ConstraintSatisfactionVisualization defaultVisualization {get;} = new ConstraintSatisfactionVisualization();
    public string[] contributors {get;} = { "Reece Foulkrod", "Gabriel Ory", "Ziming Wang" };
    // Declared, not derived — do NOT infer this from which Problems/<Folder>/ this file
    // lives under. The folder is a filing convention only and is wrong for a dozen-plus
    // existing problems; declaring it explicitly here is the only source of truth the
    // API trusts. Leave as Unclassified (the default — this line can simply be deleted)
    // until someone has actually checked the literature/argued the class. Getting this
    // wrong is a public correctness claim the API will serve to callers.
    public ComplexityClass complexityClass {get;} = ComplexityClass.NPComplete;
    public ProblemType problemType {get;} = ProblemType.MathematicalProgramming;

    // --- Properties ---

    public const string InstanceGrammar = "{(X,D,C) | X is a list of variables, D is a list of sets, and C is a set of strings}";
    public UtilCollection X { get; }
    public UtilCollection D { get; }
    public UtilCollection C { get; }

    // --- Methods and Constructors ---
    public CONSTRAINTSATISFACTION() : this(_defaultInstance) {

    }

    public CONSTRAINTSATISFACTION(string stringInstance) {
        // On a malformed instance, throw `ProblemParseException` (see
        // Interfaces/ParseExceptions.cs). The controller layer catches it
        // and returns HTTP 400 with `instanceFormat` quoted back to the
        // caller. Example:
        //
        //   if (string.IsNullOrWhiteSpace(stringInstance)) {
        //       throw new ProblemParseException("constraintsatisfaction", stringInstance, "instance is empty");
        //   }

        if (string.IsNullOrWhiteSpace(stringInstance)) {
            throw new ProblemParseException(problemName, stringInstance, "instance is empty");
        }

        instance = stringInstance;
        StringParser parser = new(InstanceGrammar);
        parser.parse(instance);
        X = parser["X"];
        D = parser["D"];
        C = parser["C"];

        // TODO: implement parsing of string instance of constraintsatisfaction. SPADE is a class meant to help with this step, see https://github.com/Jetison333/SPADE/blob/main/Documentation/index.md for more information.
        //
        // StringParser parser = new("{(i, w, v) | i subset int cross int, w is int, v is int}");
        // parser.parse(instance);
        // items = parser["i"];
        // W = int.Parse(parser["w"].ToString());
        // V = int.Parse(parser["v"].ToString());
        //
        // Or a unidirected unweighted graph example using SPADE
        // 
        // StringParser parser = new("{(N,E) | N is set, E subset N unorderdcross N}");
        // parser.parse(instance);
        // edges = parser["E"];
        // nodes = parser["N"];
    }
}
