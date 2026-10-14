using AutoSeller.Core.Pricing.Model;

namespace AutoSeller;

public sealed partial class Configuration
{
    public PricingChoices Pricing { get; set; } = new();

    internal PricingSettings BuildPricingSettings() => Pricing.Build();

    private bool RepairPricingSettings()
    {
        if (Pricing is null)
        {
            Pricing = new PricingChoices();
            return true;
        }

        return Pricing.Repair();
    }
}
