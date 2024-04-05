namespace API.Interfaces;

interface IReduction<T,U> {
    string reductionName{get;}
    string reductionDefinition{get;}
    string source {get;}
    string[] contributors { get; }
    Dictionary<Object,Object> gadgetMap {get;}
    T reductionFrom {get;}
    U reductionTo {get;}
    U reduce();
    string mapSolutions(T problemFrom, U problemTo, string problemFromSolution);
}
