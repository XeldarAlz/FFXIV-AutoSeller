using System.Globalization;
using System.Text;

namespace AutoSeller.Core.Pricing.Universalis;

internal static class UniversalisApi
{
    public const string BaseAddress = "https://universalis.app/";
    public const string RepositoryUrl = "https://github.com/XeldarAlz/FFXIV-AutoSeller";
    public const int MaximumIdsPerRequest = 100;

    private const string AggregatedPrefix = "api/v2/aggregated/";
    private const string TaxRatesPrefix = "api/v2/tax-rates?world=";
    private const int CharactersPerId = 7;

    public static string UserAgent(string version)
    {
        return string.Concat("AutoSeller/", version, " (+", RepositoryUrl, ")");
    }

    public static string AggregatedPath(ReferenceChunk chunk)
    {
        var segment = Uri.EscapeDataString(chunk.Scope.Segment);
        var ids = chunk.ItemIds;
        var builder = new StringBuilder(AggregatedPrefix.Length + segment.Length + 1 + ids.Length * CharactersPerId);
        builder.Append(AggregatedPrefix).Append(segment).Append('/');
        for (var index = 0; index < ids.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append(ids[index].ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    public static string TaxRatesPath(string world)
    {
        return string.Concat(TaxRatesPrefix, Uri.EscapeDataString(world));
    }
}
