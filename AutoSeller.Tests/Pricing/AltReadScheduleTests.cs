using AutoSeller.Core.Pricing.Model;

namespace AutoSeller.Tests.Pricing;

public sealed class AltReadScheduleTests
{
    private const long ReadAtMs = 100_000;

    private static readonly AltReadKey character = new(0x0040_0000_0000_0001, true);

    [Fact]
    public void TheBackgroundPlanReusesARecentReadOfTheSameCharacter()
    {
        Assert.False(AltReadSchedule.Due(false, character, character, ReadAtMs, ReadAtMs + AltReadSchedule.IntervalMs - 1));
        Assert.True(AltReadSchedule.Due(false, character, character, ReadAtMs, ReadAtMs + AltReadSchedule.IntervalMs));
    }

    [Fact]
    public void ACaptureThatAuthorizesWorkAlwaysReadsTheAltsAgain()
    {
        Assert.True(AltReadSchedule.Due(true, character, character, ReadAtMs, ReadAtMs + 1));
    }

    [Fact]
    public void AnotherCharacterOrAChangedSwitchReadsAtOnce()
    {
        Assert.True(AltReadSchedule.Due(false, character with { ContentId = 0x0040_0000_0000_0002 }, character, ReadAtMs, ReadAtMs + 1));
        Assert.True(AltReadSchedule.Due(false, character with { Wanted = false }, character, ReadAtMs, ReadAtMs + 1));
    }

    [Fact]
    public void TheFirstCaptureReads()
    {
        Assert.True(AltReadSchedule.Due(false, character, character, AltReadSchedule.NeverRead, ReadAtMs));
    }
}
