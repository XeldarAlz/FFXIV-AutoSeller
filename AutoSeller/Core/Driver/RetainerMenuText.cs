using AutoSeller.Core.Driver.Model;
using Dalamud.Game;
using ECommons.DalamudServices;
using AddonRow = Lumina.Excel.Sheets.Addon;

namespace AutoSeller.Core.Driver;

internal static class RetainerMenuText
{
    public const uint SellFromInventoryRow = 2_380;
    public const uint SellFromRetainerRow = 2_381;
    public const uint SaleHistoryRow = 2_382;
    public const uint QuitRow = 2_383;
    public const uint AdjustPriceRow = 930;
    public const uint AdjustPriceAlternateRow = 6_948;
    public const uint ReturnToRetainerRow = 958;
    public const uint ReturnToRetainerAlternateRow = 6_954;
    public const uint HaveRetainerSellRow = 5_480;

    private static readonly LanguageCachedText<PromptText> sellFromInventory = new(language => Read(language, SellFromInventoryRow));
    private static readonly LanguageCachedText<PromptText> sellFromRetainer = new(language => Read(language, SellFromRetainerRow));
    private static readonly LanguageCachedText<PromptText> saleHistory = new(language => Read(language, SaleHistoryRow));
    private static readonly LanguageCachedText<PromptText> quit = new(language => Read(language, QuitRow));
    private static readonly LanguageCachedText<PromptText> adjustPrice = new(language => Read(language, AdjustPriceRow));
    private static readonly LanguageCachedText<PromptText> adjustPriceAlternate = new(language => Read(language, AdjustPriceAlternateRow));
    private static readonly LanguageCachedText<PromptText> returnToRetainer = new(language => Read(language, ReturnToRetainerRow));
    private static readonly LanguageCachedText<PromptText> returnToRetainerAlternate = new(language => Read(language, ReturnToRetainerAlternateRow));
    private static readonly LanguageCachedText<PromptText> haveRetainerSell = new(language => Read(language, HaveRetainerSellRow));
    private static readonly LanguageCachedText<PromptText> gilLimit = new(language => Read(language, RetainerChain.GilLimitPromptRow));
    private static readonly LanguageCachedText<PromptText> gilLimitAlternate = new(language => Read(language, RetainerChain.GilLimitPromptAlternateRow));

    public static PromptText SellFromInventory() => sellFromInventory.Get();

    public static PromptText SellFromRetainer() => sellFromRetainer.Get();

    public static PromptText SaleHistory() => saleHistory.Get();

    public static PromptText Quit() => quit.Get();

    public static int FindAdjustPrice(ReadOnlySpan<string> entries) => Find(entries, IsAdjustPrice);

    public static int FindReturnToRetainer(ReadOnlySpan<string> entries) => Find(entries, IsReturnToRetainer);

    public static int FindHaveRetainerSell(ReadOnlySpan<string> entries) => Find(entries, IsHaveRetainerSell);

    public static bool IsAdjustPrice(string shown)
    {
        return adjustPrice.Get().Matches(shown) || adjustPriceAlternate.Get().Matches(shown);
    }

    public static bool IsReturnToRetainer(string shown)
    {
        return returnToRetainer.Get().Matches(shown) || returnToRetainerAlternate.Get().Matches(shown);
    }

    public static bool IsHaveRetainerSell(string shown)
    {
        return haveRetainerSell.Get().Matches(shown);
    }

    private static int Find(ReadOnlySpan<string> entries, Func<string, bool> matches)
    {
        for (var index = 0; index < entries.Length; index++)
        {
            if (matches(entries[index]))
            {
                return index;
            }
        }

        return PromptText.NoMatch;
    }

    public static uint PromptRowOf(string shown)
    {
        if (gilLimit.Get().Matches(shown))
        {
            return RetainerChain.GilLimitPromptRow;
        }

        return gilLimitAlternate.Get().Matches(shown) ? RetainerChain.GilLimitPromptAlternateRow : ChainStep.NoPrompt;
    }

    private static PromptText Read(ClientLanguage language, uint row)
    {
        try
        {
            var text = Svc.Data.GetExcelSheet<AddonRow>(language).GetRowOrDefault(row)?.Text.ExtractText(false, PromptText.MacroPlaceholder) ?? string.Empty;
            RunLog.Info($"Retainer menu: Addon row {row} reads '{text}' ({language}).");
            return PromptText.From(text);
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, $"Retainer menu: Addon row {row} could not be read ({language})");
            return default;
        }
    }
}
