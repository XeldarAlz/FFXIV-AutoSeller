using System.Text;
using AutoSeller.Core.Characters;
using AutoSeller.Core.Sales.Model;
using Dalamud.Game.Chat;
using ECommons.DalamudServices;

namespace AutoSeller.Core.Sales;

internal sealed class SaleNoticeWatcher : IDisposable
{
    private const int MaximumParameters = 8;
    private const int NotAnInteger = -1;

    private readonly CharacterStore characters;
    private readonly SalesLedger ledger;
    private readonly DiagnosticLog rawLog = new();
    private readonly StringBuilder line = new();

    public SaleNoticeWatcher(CharacterStore characters, SalesLedger ledger)
    {
        this.characters = characters;
        this.ledger = ledger;
        Svc.Chat.LogMessage += OnLogMessage;
    }

    public void Dispose()
    {
        Svc.Chat.LogMessage -= OnLogMessage;
    }

    private void OnLogMessage(ILogMessage message)
    {
        var messageId = message.LogMessageId;
        if (messageId is not (SaleNoticeLayout.StackSaleMessageId or SaleNoticeLayout.SingleSaleMessageId))
        {
            return;
        }

        Span<int> parameters = stackalloc int[MaximumParameters];
        var count = Math.Min(message.ParameterCount, MaximumParameters);
        line.Clear().Append("Sale notice: LogMessage ").Append(messageId).Append(" with ").Append(message.ParameterCount).Append(" parameter(s):");
        for (var index = 0; index < count; index++)
        {
            parameters[index] = ReadParameter(message, index);
        }

        rawLog.Report(line.ToString());
        if (characters.Current is not { } data)
        {
            RunLog.Warning($"Sale notice: LogMessage {messageId} arrived with no character data loaded; it is not recorded.");
            return;
        }

        if (!SaleNoticeParser.TryRead(messageId, parameters[..count], out var notice))
        {
            RunLog.Warning($"Sale notice: LogMessage {messageId} could not be read as a sale; it is not recorded.");
            return;
        }

        var sale = SaleNoticeParser.ToSaleRecord(notice, DateTime.UtcNow, data.ContentId, data.SnapshotArray());
        ledger.Record(data.ContentId, [sale]);
    }

    private int ReadParameter(ILogMessage message, int index)
    {
        if (message.TryGetIntParameter(index, out var value))
        {
            line.Append(" [").Append(index).Append("] ").Append(value);
            return value;
        }

        if (message.TryGetStringParameter(index, out var text))
        {
            line.Append(" [").Append(index).Append("] '").Append(text.ExtractText()).Append('\'');
            return NotAnInteger;
        }

        line.Append(" [").Append(index).Append("] unreadable");
        return NotAnInteger;
    }
}
