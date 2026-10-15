using AutoSeller.Core.Execution.Model;

namespace AutoSeller.Tests.Execution;

public sealed class RefusalsTests
{
    [Theory]
    [InlineData((byte)SaleRejection.RepriceCapReached, (byte)RefusalKind.RepriceLimit)]
    [InlineData((byte)SaleRejection.ListingCapReached, (byte)RefusalKind.ListingLimit)]
    [InlineData((byte)SaleRejection.VendorValueCapReached, (byte)RefusalKind.VendorLimit)]
    public void EachRunLimitHasItsOwnKind(byte rejection, byte kind)
    {
        var refusal = Refusals.Of((SaleRejection)rejection);

        Assert.Equal((RefusalKind)kind, refusal);
        Assert.True(Refusals.IsLimit(refusal));
    }

    [Theory]
    [InlineData((byte)SaleRejection.BelowFloor, (byte)RefusalKind.BelowFloor)]
    [InlineData((byte)SaleRejection.OverStepLimit, (byte)RefusalKind.OverStepLimit)]
    [InlineData((byte)SaleRejection.NotEligible, (byte)RefusalKind.NotEligible)]
    [InlineData((byte)SaleRejection.PriceUnchanged, (byte)RefusalKind.PriceUnchanged)]
    [InlineData((byte)SaleRejection.ListingChanged, (byte)RefusalKind.Changed)]
    [InlineData((byte)SaleRejection.QuantityMismatch, (byte)RefusalKind.Changed)]
    [InlineData((byte)SaleRejection.VendorGear, (byte)RefusalKind.VendorRules)]
    [InlineData((byte)SaleRejection.NoVendorRule, (byte)RefusalKind.VendorRules)]
    [InlineData((byte)SaleRejection.ItemProtected, (byte)RefusalKind.Protected)]
    [InlineData((byte)SaleRejection.VendorProtected, (byte)RefusalKind.Protected)]
    [InlineData((byte)SaleRejection.DryRunChanged, (byte)RefusalKind.SettingsChanged)]
    [InlineData((byte)SaleRejection.RulesChanged, (byte)RefusalKind.SettingsChanged)]
    public void ARefusalThePlayerCanActOnIsNamed(byte rejection, byte kind)
    {
        var refusal = Refusals.Of((SaleRejection)rejection);

        Assert.Equal((RefusalKind)kind, refusal);
        Assert.False(Refusals.IsLimit(refusal));
    }

    [Theory]
    [InlineData((byte)SaleRejection.WrongRun)]
    [InlineData((byte)SaleRejection.StaleSearch)]
    [InlineData((byte)SaleRejection.WindowChainNotReady)]
    [InlineData((byte)SaleRejection.JournalUnresolved)]
    public void AnInternalRefusalStaysASafetyCheck(byte rejection)
    {
        Assert.Equal(RefusalKind.Safety, Refusals.Of((SaleRejection)rejection));
    }

    [Fact]
    public void OnlyTheThreeRunLimitsAreLimits()
    {
        var rejections = Enum.GetValues<SaleRejection>();
        var limits = 0;
        for (var index = 0; index < rejections.Length; index++)
        {
            limits += Refusals.IsLimit(Refusals.Of(rejections[index])) ? 1 : 0;
        }

        Assert.Equal(3, limits);
    }
}
