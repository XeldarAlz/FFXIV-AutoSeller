using AutoSeller.Core.Driver;
using AutoSeller.Core.Driver.Model;
using Dalamud.Game;
using ECommons.DalamudServices;
using ECommons.ExcelServices.Sheets;

namespace AutoSeller.Core.Travel;

internal static class InnkeeperMenu
{
    public const uint RetireToRoomRow = 11;

    private static readonly Dictionary<string, LanguageCachedText<PromptText>> retireToRoom = new(StringComparer.Ordinal);

    public static PromptText RetireToRoom(in CityInn inn)
    {
        var sheet = inn.InnkeeperDialogueSheet;
        if (!retireToRoom.TryGetValue(sheet, out var text))
        {
            text = new LanguageCachedText<PromptText>(language => Read(language, sheet));
            retireToRoom[sheet] = text;
        }

        return text.Get();
    }

    private static PromptText Read(ClientLanguage language, string sheet)
    {
        try
        {
            var text = Svc.Data.GetExcelSheet<QuestDialogueText>(language, sheet).GetRowOrDefault(RetireToRoomRow)?.Value.ExtractText() ?? string.Empty;
            RunLog.Info($"Inn: {sheet} row {RetireToRoomRow} reads '{text}' ({language}).");
            return PromptText.From(text);
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, $"Inn: {sheet} row {RetireToRoomRow} could not be read ({language})");
            return default;
        }
    }
}
