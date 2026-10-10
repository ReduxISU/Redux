using System.Text.Json;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// #763: reduce() must build the gadget list afresh. Several reductions appended to `gadgets` without clearing it,
// so every extra reduce() call on the same object added a second copy of every gadget.
public class ReductionGadgets_Tests {
    // Source instances for reductions that cannot take their source problem's default instance.
    private static readonly Dictionary<string, string> Instances = new() {
        // Needs Sipser-formatted node names (<literal>_<clause>).
        ["sipserreducetosat3"] = "(({x1_0,x2_1,x3_2},{{x1_0,x2_1},{x2_1,x3_2},{x1_0,x3_2}}),3)",
    };

    // Every reduction in the API (the test project's own fake reductions are left out).
    public static TheoryData<string> AllReductions() => new(ProblemProvider.Reductions
        .Where(r => r.Value.Assembly == typeof(ProblemProvider).Assembly)
        .Select(r => r.Key)
        .OrderBy(k => k));

    [Theory]
    [MemberData(nameof(AllReductions))]
    public void Reduce_CalledAgain_KeepsTheSameGadgets(string reduction) {
        Type type = ProblemProvider.Reductions[reduction];
        string instance = Instances.TryGetValue(reduction, out var given)
            ? given
            : ((IReduction)Activator.CreateInstance(type)!).reductionFrom.defaultInstance;
        // The constructor already ran reduce() once.
        var red = (IReduction)Activator.CreateInstance(type, instance)!;
        int count = red.gadgets.Count;
        string gadgets = JsonSerializer.Serialize(red.gadgets);

        red.reduce();
        red.reduce();

        Assert.Equal(count, red.gadgets.Count);
        Assert.Equal(gadgets, JsonSerializer.Serialize(red.gadgets));
    }
}
