using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoSeller.Core.Rules.Model;

internal enum RulesImportProblem : byte
{
    None,
    Empty,
    TooLarge,
    Malformed,
    NewerVersion,
}

internal sealed record RulesDocument
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public List<ItemRule> SellList { get; init; } = [];

    public List<CategoryRule> CategoryRules { get; init; } = [];

    public List<uint> NeverSell { get; init; } = [];

    public List<uint> VendorList { get; init; } = [];

    public uint JunkThreshold { get; init; } = VendorSettings.DefaultJunkThreshold;
}

internal static class RulesTransfer
{
    public const int MaximumImportLength = 4_000_000;

    private static readonly JsonSerializerOptions options = new()
    {
        WriteIndented = true,
        RespectNullableAnnotations = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Export(
        IReadOnlyList<ItemRule> sellList,
        IReadOnlyList<CategoryRule> categoryRules,
        IReadOnlyList<uint> neverSell,
        IReadOnlyList<uint> vendorList,
        uint junkThreshold)
    {
        var document = new RulesDocument
        {
            SellList = WithoutRetainers(sellList),
            CategoryRules = [.. categoryRules],
            NeverSell = [.. neverSell],
            VendorList = [.. vendorList],
            JunkThreshold = junkThreshold,
        };
        return JsonSerializer.Serialize(document, options);
    }

    public static RulesImportProblem TryImport(string text, out RulesDocument? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return RulesImportProblem.Empty;
        }

        if (text.Length > MaximumImportLength)
        {
            return RulesImportProblem.TooLarge;
        }

        RulesDocument? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<RulesDocument>(text, options);
        }
        catch (JsonException)
        {
            return RulesImportProblem.Malformed;
        }

        if (parsed is null)
        {
            return RulesImportProblem.Malformed;
        }

        if (parsed.Version > RulesDocument.CurrentVersion)
        {
            return RulesImportProblem.NewerVersion;
        }

        RuleRepair.RepairSellList(parsed.SellList);
        RuleRepair.RepairCategoryRules(parsed.CategoryRules);
        RuleRepair.RepairIds(parsed.NeverSell);
        RuleRepair.RepairIds(parsed.VendorList);
        for (var index = 0; index < parsed.SellList.Count; index++)
        {
            parsed.SellList[index].PreferredRetainerId = 0;
        }

        document = parsed;
        return RulesImportProblem.None;
    }

    private static List<ItemRule> WithoutRetainers(IReadOnlyList<ItemRule> sellList)
    {
        var copies = new List<ItemRule>(sellList.Count);
        for (var index = 0; index < sellList.Count; index++)
        {
            var rule = sellList[index];
            copies.Add(new ItemRule
            {
                ItemId = rule.ItemId,
                Qualities = rule.Qualities,
                Floor = rule.Floor,
                Ceiling = rule.Ceiling,
                FixedPrice = rule.FixedPrice,
                LotSize = rule.LotSize,
                KeepCount = rule.KeepCount,
                ListingsToKeepUp = rule.ListingsToKeepUp,
                Paused = rule.Paused,
            });
        }

        return copies;
    }
}
