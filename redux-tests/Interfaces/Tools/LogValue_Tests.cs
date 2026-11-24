using Xunit;
using API.Interfaces.Tools;

namespace redux_tests;
#pragma warning disable CS1591

public class LogValue_Tests {

    [Fact]
    public void Safe_PlainValue_IsUnchanged() {
        Assert.Equal("SipserReduceToCliqueStandard", LogValue.Safe("SipserReduceToCliqueStandard"));
    }

    [Fact]
    public void Safe_Null_IsWrittenNull() {
        Assert.Equal("(null)", LogValue.Safe(null));
    }

    [Fact]
    public void Safe_LineBreaks_AreEscapedSoNoForgedLineStarts() {
        string forged = "Solver\r\nfail: API.Program[0] forged entry\n";

        string safe = LogValue.Safe(forged);

        Assert.Equal("Solver\\r\\nfail: API.Program[0] forged entry\\n", safe);
        Assert.DoesNotContain('\n', safe);
        Assert.DoesNotContain('\r', safe);
    }

    [Fact]
    public void Safe_OtherControlCharacters_AreEscaped() {
        Assert.Equal("a\\u001b[31mb\\u0000c\\u0009d", LogValue.Safe("a\u001b[31mb\u0000c\td"));
    }

    [Fact]
    public void Safe_LongValue_IsCutWithEllipsis() {
        string safe = LogValue.Safe(new string('x', 500));

        Assert.Equal(new string('x', LogValue.DefaultMaxLength) + "...", safe);
    }

    [Fact]
    public void Safe_CustomLength_IsHonoured() {
        Assert.Equal("abc...", LogValue.Safe("abcdef", 3));
        Assert.Equal("abc", LogValue.Safe("abc", 3));
    }
}
