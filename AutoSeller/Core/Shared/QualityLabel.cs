namespace AutoSeller.Core.Shared;

internal static class QualityLabel
{
    public const string High = "HQ";
    public const string Normal = "NQ";

    public static string Of(bool isHq)
    {
        return isHq ? High : Normal;
    }
}
