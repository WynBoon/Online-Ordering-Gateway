using Gateway.Application.Ports;

namespace Gateway.Application.Tests;

public class PosOrderResultTests
{
    [Fact]
    public void Truncate_leaves_short_strings_unchanged()
    {
        Assert.Equal("ok", PosOrderResult.Truncate("ok"));
    }

    [Fact]
    public void Truncate_caps_at_max_detail_length()
    {
        var input = new string('x', PosOrderResult.MaxDetailLength + 50);
        var truncated = PosOrderResult.Truncate(input);
        Assert.NotNull(truncated);
        Assert.Equal(PosOrderResult.MaxDetailLength, truncated!.Length);
    }

    [Fact]
    public void Ok_and_Fail_apply_truncate_to_detail()
    {
        var longDetail = new string('y', PosOrderResult.MaxDetailLength + 10);
        var ok = PosOrderResult.Ok("1", longDetail);
        var fail = PosOrderResult.Fail("pos_failure", "err", retryable: true, detail: longDetail);
        Assert.Equal(PosOrderResult.MaxDetailLength, ok.Detail!.Length);
        Assert.Equal(PosOrderResult.MaxDetailLength, fail.Detail!.Length);
    }
}
