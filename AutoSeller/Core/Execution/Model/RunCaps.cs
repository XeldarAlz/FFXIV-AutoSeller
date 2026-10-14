using System.Text.Json.Serialization;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Execution.Model;

internal readonly record struct RunCaps(int MaxReprices, int MaxNewListings, long MaxVendoredValue)
{
    public const int DefaultMaxReprices = 80;
    public const int DefaultMaxNewListings = 100;
    public const long DefaultMaxVendoredValue = 20_000;

    public static RunCaps Default => new(DefaultMaxReprices, DefaultMaxNewListings, DefaultMaxVendoredValue);

    [JsonIgnore]
    public bool IsValid => MaxReprices >= 0 && MaxNewListings >= 0 && MaxVendoredValue >= 0;

    public bool Allows(WriteKind kind, in ValueState used, long value)
    {
        return kind switch
        {
            WriteKind.Reprice => used.Reprices < MaxReprices,
            WriteKind.List => used.NewListings < MaxNewListings,
            WriteKind.Vendor => value >= 0 && Gil.TryAdd(used.VendoredValue, value, out var total) && total <= MaxVendoredValue,
            _ => true,
        };
    }
}
