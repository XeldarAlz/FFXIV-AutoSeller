using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Tests.Execution;

public sealed class RunCapsTests
{
    [Fact]
    public void CapsInRangeStayAsTheyAre()
    {
        var caps = new RunCaps(40, 60, 5_000);

        Assert.Equal(caps, caps.Clamped());
        Assert.Equal(RunCaps.Default, RunCaps.Default.Clamped());
    }

    [Fact]
    public void NegativeCapsRiseToZero()
    {
        Assert.Equal(new RunCaps(0, 0, 0), new RunCaps(-1, -5, -10).Clamped());
    }

    [Fact]
    public void WriteCapsStopAtEverySlotOfEveryRetainer()
    {
        var clamped = new RunCaps(int.MaxValue, 201, long.MaxValue).Clamped();

        Assert.Equal(200, RunCaps.MaximumWritesPerRun);
        Assert.Equal(RunCaps.MaximumWritesPerRun, clamped.MaxReprices);
        Assert.Equal(RunCaps.MaximumWritesPerRun, clamped.MaxNewListings);
        Assert.Equal(MarketLimits.RetainerGilCap, clamped.MaxVendoredValue);
    }
}
