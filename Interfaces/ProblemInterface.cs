namespace API.Interfaces;

interface IProblem<T,U> where T : ISolver where U : IVerifier{
    string problemName{get;}

    string formalDefinition{get;}
    string problemDefinition{get;}
    string source {get;}
    string wikiName {get;}
    string defaultInstance{get;}

    string[] contributors{ get; }
    T defaultSolver{get;}
    U defaultVerifier{get;}

    
}
