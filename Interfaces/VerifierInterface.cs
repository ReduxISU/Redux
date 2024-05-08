namespace API.Interfaces;

interface IVerifier {
    string verifierName{get;}
    string verifierDefinition{get;}
    string source {get;}
    string certificate{get;}
    string[] contributors{ get; }
}