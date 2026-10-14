namespace AutoSeller;

public sealed partial class Configuration
{
    public bool AutoRetainerPostprocess { get; set; } = false;

    public bool AutoRetainerSellNowButton { get; set; } = true;

    public bool AutoRetainerAltsAreOwn { get; set; } = true;
}
