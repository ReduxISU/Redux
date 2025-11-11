using API.Interfaces.JSON_Objects;

namespace API.Interfaces;

interface IReduction
{
    string reductionName { get; }
    string reductionDefinition { get; }
    string source { get; }
    string[] contributors { get; }
    IVisualization visualization { get; }
    List<Gadget> gadgets { get; }
    IProblem reductionFrom { get; }
    IProblem reductionTo { get; }
    IProblem reduce();
    string mapSolutions(string problemFromSolution);
}

interface IReduction<T, U> : IReduction where T : IProblem where U : IProblem
{
    IVisualization IReduction.visualization
    {
        get
        {
            return reductionTo.defaultVisualization;
        }
    }

    IProblem IReduction.reductionFrom
    {
        get
        {
            return reductionFrom;
        }
    }
    new T reductionFrom { get; }
    IProblem IReduction.reductionTo
    {
        get
        {
            return reductionTo;
        }
    }
    new U reductionTo { get; }

    IProblem IReduction.reduce()
    {
        return reduce();
    }
    new U reduce();

    List<Gadget> IReduction.gadgets
    {
        get
        {
            return new List<Gadget>();
        }
    }
}
