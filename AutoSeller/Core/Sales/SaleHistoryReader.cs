using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using AutoSeller.Core.Driver;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Game.Ops;
using AutoSeller.Core.Market;
using AutoSeller.Core.Sales.Model;
using Dalamud.Hooking;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoSeller.Core.Sales;

internal sealed unsafe class SaleHistoryReader : IDisposable
{
    public static readonly string WindowAddon = ChainWindows.NameOf(ChainWindow.RetainerHistory);

    private const string HistoryPacketSignature = "40 53 56 57 41 57 48 83 EC 38 48 8B F1";
    private const string HistoryPacketName = "the retainer sale history packet handler";
    private const int MaximumWindowValues = SaleHistoryWindowLayout.ValueCount + SaleHistoryWindowLayout.RowStride;

    private readonly Configuration configuration;
    private readonly int[] windowValues = new int[MaximumWindowValues];
    private readonly byte[] packet = new byte[SaleHistoryLayout.PacketLength];
    private readonly DiagnosticLog windowLog = new();
    private readonly DiagnosticLog packetLog = new();
    private readonly StringBuilder line = new();
    private Hook<HistoryPacketDelegate>? packetHook;
    private bool packetHookTried;
    private ulong armedRetainerId;
    private volatile bool packetCaptured;

    public SaleHistoryReader(Configuration configuration)
    {
        this.configuration = configuration;
    }

    private delegate nint HistoryPacketDelegate(nint first, nint packetData, nint third, nint fourth);

    public SaleHistorySource Source => configuration.SaleHistorySource;

    public void Arm(ulong retainerId)
    {
        packetCaptured = false;
        armedRetainerId = retainerId;
        if (Source == SaleHistorySource.Packet)
        {
            EnsurePacketHook();
        }
    }

    public void Disarm()
    {
        armedRetainerId = 0;
        packetCaptured = false;
    }

    public bool TryRead(ulong characterContentId, ulong retainerId, int sellerTaxPercent, DateTime nowUtc, out ImmutableArray<SaleRecord> sales)
    {
        sales = ImmutableArray<SaleRecord>.Empty;
        if (retainerId == 0 || retainerId != armedRetainerId)
        {
            return false;
        }

        return Source == SaleHistorySource.Packet
            ? packetCaptured && SaleHistoryParser.TryParse(packet, characterContentId, retainerId, sellerTaxPercent, out sales)
            : TryReadWindow(characterContentId, retainerId, sellerTaxPercent, nowUtc, out sales);
    }

    public void Dispose()
    {
        packetHook?.Dispose();
        packetHook = null;
    }

    private bool TryReadWindow(ulong characterContentId, ulong retainerId, int sellerTaxPercent, DateTime nowUtc, out ImmutableArray<SaleRecord> sales)
    {
        sales = ImmutableArray<SaleRecord>.Empty;
        if (!NpcInteraction.TryGetReadyAddon(WindowAddon, out var window) || window->AtkValues == null)
        {
            return false;
        }

        var count = Math.Min((int)window->AtkValuesCount, windowValues.Length);
        line.Clear().Append("Sale history: the window holds ").Append(window->AtkValuesCount).Append(" value(s):");
        for (var index = 0; index < count; index++)
        {
            ref readonly var value = ref window->AtkValues[index];
            windowValues[index] = AtkValueReads.Integer(value);
            line.Append(" [").Append(index).Append("] ").Append(value.Type);
            if (AtkValueReads.IsText(value))
            {
                line.Append(" '").Append(AtkValueReads.Text(value)).Append('\'');
            }
            else
            {
                line.Append(' ').Append(windowValues[index]);
            }
        }

        windowLog.Report(line.ToString());
        return SaleHistoryWindowParser.TryParse(windowValues.AsSpan(0, count), characterContentId, retainerId, sellerTaxPercent, nowUtc, out sales);
    }

    private void EnsurePacketHook()
    {
        if (packetHookTried)
        {
            return;
        }

        packetHookTried = true;
        if (!Svc.SigScanner.TryScanText(HistoryPacketSignature, out var address))
        {
            RunLog.Warning($"Sale history: {HistoryPacketName} was not found; the history cannot be read from its packet.");
            return;
        }

        packetHook = MarketHooks.FromAddress<HistoryPacketDelegate>(address, OnHistoryPacket, HistoryPacketName);
        MarketHooks.Enable(packetHook);
        RunLog.Info($"Sale history: {HistoryPacketName} hooked at 0x{address:X}.");
    }

    private nint OnHistoryPacket(nint first, nint packetData, nint third, nint fourth)
    {
        var result = packetHook!.Original(first, packetData, third, fourth);
        try
        {
            Capture(packetData);
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, "Sale history: the packet could not be copied");
        }

        return result;
    }

    private void Capture(nint packetData)
    {
        var armed = armedRetainerId;
        if (armed == 0 || packetData == 0)
        {
            return;
        }

        var data = new ReadOnlySpan<byte>((void*)packetData, SaleHistoryLayout.PacketLength);
        var retainerId = BinaryPrimitives.ReadUInt64LittleEndian(data[SaleHistoryLayout.RetainerIdOffset..]);
        packetLog.Report($"Sale history: a packet arrived for retainer {retainerId}; retainer {armed} is being read; first record item {BinaryPrimitives.ReadUInt32LittleEndian(data[SaleHistoryLayout.HeaderLength..])}.");
        if (retainerId != armed)
        {
            return;
        }

        data.CopyTo(packet);
        packetCaptured = true;
    }
}
