namespace API.Interfaces;

interface IVerifier {
    string verifierName{get;}
    string verifierDefinition{get;}
    string source {get;}
    string certificate{get;}
    string[] contributors{ get; }

    bool verify(string problem, string certificate);
}

interface IVerifier<T> : IVerifier where T : IProblem {
    bool IVerifier.verify(string problem, string certificate) {
        // Should there be some sort of contraint that assures there is a constructor
        // that matches the signature of a single `string` argument?
        // Perhaps a static `FromInstance(string instance)` method for `IProblem` will work.
        T problemInstance;
        try {
            problemInstance = (T)Activator.CreateInstance(typeof(T), problem)!;
        } catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException != null) {
            // Unwrap so ProblemParseException (or any other constructor exception)
            // surfaces with its real type to the controller layer.
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
            throw; // unreachable
        }
        return verify(problemInstance, certificate);
    }
    bool verify(T problem, string certificate);
}
