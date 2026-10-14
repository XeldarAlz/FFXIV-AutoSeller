using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AutoSeller.Core.Driver;

internal enum SellOpenPath : byte
{
    AgentOpenRetainerSell,
    PutUpForSaleCommand,
}

internal static unsafe class SellWindowOpener
{
    public const SellOpenPath PreferredPath = SellOpenPath.AgentOpenRetainerSell;

    private const string OpenRetainerSellSignature = "E8 ?? ?? ?? ?? EB ?? 48 83 BF ?? ?? ?? ?? ?? 74 ?? 8B CE";
    private const ulong PutUpForSaleCommand = 2;

    private static delegate* unmanaged<AgentRetainer*, InventoryType, ushort, void> openRetainerSell;
    private static bool signatureScanned;
    private static OpenAttempt reported;

    public static SellOpenPath PathFor(int attempt)
    {
        var preferredTurn = attempt % 2 == 0;
        var agentTurn = preferredTurn == (PreferredPath == SellOpenPath.AgentOpenRetainerSell);
        return agentTurn && AgentPathAvailable() ? SellOpenPath.AgentOpenRetainerSell : SellOpenPath.PutUpForSaleCommand;
    }

    public static bool Open(uint container, ushort slot, SellOpenPath path)
    {
        var agent = AgentRetainer.Instance();
        if (agent == null)
        {
            Report(new OpenAttempt(path, container, slot, false, false));
            return false;
        }

        var active = agent->IsAgentActive();
        Report(new OpenAttempt(path, container, slot, true, active));
        if (!active)
        {
            return false;
        }

        if (path == SellOpenPath.AgentOpenRetainerSell && AgentPathAvailable())
        {
            openRetainerSell(agent, (InventoryType)container, slot);
            return true;
        }

        agent->HandleCallback(slot, (InventoryType)container, 0, PutUpForSaleCommand);
        return true;
    }

    private static bool AgentPathAvailable()
    {
        if (signatureScanned)
        {
            return openRetainerSell != null;
        }

        signatureScanned = true;
        if (Svc.SigScanner.TryScanText(OpenRetainerSellSignature, out var address))
        {
            openRetainerSell = (delegate* unmanaged<AgentRetainer*, InventoryType, ushort, void>)address;
            RunLog.Info($"Sell window: AgentRetainer.OpenRetainerSell found at 0x{address:X}.");
            return true;
        }

        RunLog.Warning("Sell window: AgentRetainer.OpenRetainerSell was not found; bag slots open through the put-up-for-sale command.");
        return false;
    }

    private static void Report(in OpenAttempt attempt)
    {
        if (attempt == reported)
        {
            return;
        }

        reported = attempt;
        RunLog.Info($"Sell window: opening container {attempt.Container} slot {attempt.Slot} through {attempt.Path}; retainer agent present {attempt.AgentPresent}, active {attempt.AgentActive}.");
    }

    private readonly record struct OpenAttempt(SellOpenPath Path, uint Container, ushort Slot, bool AgentPresent, bool AgentActive);
}
